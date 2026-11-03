using System.Text.Json;

namespace Slithy_Tove;

internal sealed partial class WalletRpcClient
{
    public const int MaximumConsolidationInputs = 200;
    private sealed class RecoveryWindowExceededException(string message) : InvalidOperationException(message);

    private async Task<(Dictionary<string, object> Options, bool ReusedChange)> FundingOptionsAsync(Uri endpoint, string wallet)
    {
        Dictionary<string, object> options = new()
        {
            ["include_unsafe"] = false, ["lockUnspents"] = false, ["conf_target"] = 6, ["estimate_mode"] = "conservative"
        };
        try { await EnsureRecoveryGenerationRangeAsync(endpoint, internalAddress: true); }
        catch (RecoveryWindowExceededException)
        {
            // Index zero stays inside the normal words-restore window. Show the reuse in confirmation.
            string address = await GetRecoverySafeReceivingAddressAsync(endpoint, wallet)
                ?? throw new InvalidOperationException("Back up this wallet file before arranging a deeper recovery range for change.");
            options["changeAddress"] = address;
            return (options, true);
        }
        EnsureSameWallet(wallet);
        return (options, false);
    }

    private async Task<string?> GetRecoverySafeReceivingAddressAsync(Uri endpoint, string wallet)
    {
        JsonElement descriptors = await _rpc.CallAsync(endpoint, wallet, "listdescriptors", new object[] { false }, LongWalletOperationTimeout);
        EnsureSameWallet(wallet);
        string? descriptor = descriptors.GetProperty("descriptors").EnumerateArray()
            .Where(item => ReadBoolean(item, "active") && !ReadBoolean(item, "internal"))
            .Select(item => ReadString(item, "desc")).FirstOrDefault(item => item.StartsWith("wpkh(", StringComparison.Ordinal) && item.Contains("/*", StringComparison.Ordinal));
        if (descriptor is null) return null;
        JsonElement addresses = await _rpc.CallAsync(endpoint, "", "deriveaddresses", new object[] { descriptor, new[] { 0, 0 } }, LongWalletOperationTimeout);
        EnsureSameWallet(wallet);
        string address = addresses[0].GetString()!;
        JsonElement own = await _rpc.CallAsync(endpoint, wallet, "getaddressinfo", new object[] { address });
        EnsureSameWallet(wallet);
        if (!ReadBoolean(own, "ismine")) throw new InvalidOperationException("The recovery-safe receiving address does not belong to this wallet.");
        return address;
    }

    public async Task<IReadOnlyList<WalletUnspentOutput>> GetSpendableOutputsAsync(Uri endpoint)
    {
        EnsureWalletOpen();
        string wallet = _walletName;
        JsonElement result = await _rpc.CallAsync(endpoint, wallet, "listunspent", new object[] { 1, 9999999, Array.Empty<string>(), false }, LongWalletOperationTimeout);
        List<WalletUnspentOutput> outputs = [];
        foreach (JsonElement item in result.EnumerateArray())
        {
            WalletUnspentOutput coin = new(ReadString(item, "txid"), checked((int)ReadInt64(item, "vout")), ReadString(item, "address"),
                ReadDecimal(item, "amount"), ReadInt64(item, "confirmations"), ReadBoolean(item, "spendable"), ReadBoolean(item, "safe"), ReadString(item, "label"));
            if (coin.Spendable && coin.Safe && coin.Confirmations > 0 && coin.Amount > 0) outputs.Add(coin);
        }
        EnsureSameWallet(wallet);
        return outputs.OrderByDescending(coin => coin.Confirmations).ThenBy(coin => coin.TransactionHash, StringComparer.Ordinal).ThenBy(coin => coin.OutputIndex).ToArray();
    }

    public Task<PreparedTransfer> PrepareTransferAsync(Uri endpoint, string address, ulong amountAtomic, IReadOnlyList<WalletOutpoint> selectedInputs)
    {
        if (amountAtomic == 0) throw new ArgumentOutOfRangeException(nameof(amountAtomic));
        return PrepareSelectedTransferAsync(endpoint, address, amountAtomic, selectedInputs, false);
    }

    public async Task<PreparedTransfer> PrepareConsolidationAsync(Uri endpoint, IReadOnlyList<WalletOutpoint> selectedInputs)
    {
        EnsureWalletOpen();
        string wallet = _walletName;
        if (selectedInputs.Count is < 2 or > MaximumConsolidationInputs)
            throw new ArgumentException("Choose between 2 and 200 confirmed outputs for this consolidation batch. This app batch size is not a limit on your address.", nameof(selectedInputs));
        // Validate the selection before consuming a new receiving address.
        _ = await ResolveSelectedInputsAsync(endpoint, selectedInputs);
        EnsureSameWallet(wallet);
        WalletReceivingAddress destination = await CreateReceivingAddressAsync(endpoint, "Consolidation");
        EnsureSameWallet(wallet);
        return await PrepareSelectedTransferAsync(endpoint, destination.Address, 0, selectedInputs, true);
    }

    private async Task<WalletUnspentOutput[]> ResolveSelectedInputsAsync(Uri endpoint, IReadOnlyList<WalletOutpoint> selectedInputs)
    {
        if (selectedInputs.Count == 0) throw new ArgumentException("Select at least one confirmed output.", nameof(selectedInputs));
        HashSet<string> selected = new(StringComparer.Ordinal);
        foreach (WalletOutpoint input in selectedInputs)
        {
            ValidateTransactionHash(input.TransactionHash);
            if (input.OutputIndex < 0 || !selected.Add(OutpointKey(input))) throw new ArgumentException("Selected outputs contain an invalid or repeated outpoint.", nameof(selectedInputs));
        }
        Dictionary<string, WalletUnspentOutput> available = (await GetSpendableOutputsAsync(endpoint)).ToDictionary(coin => OutpointKey(coin.Outpoint), StringComparer.Ordinal);
        return selected.Select(key => available.TryGetValue(key, out WalletUnspentOutput? coin) ? coin :
            throw new InvalidOperationException("A selected output is no longer confirmed and spendable. Refresh the coin list and review your selection.")).ToArray();
    }

    private static string OutpointKey(WalletOutpoint input) => input.TransactionHash.ToLowerInvariant() + ":" + input.OutputIndex;

    private async Task<PreparedTransfer> PrepareSelectedTransferAsync(Uri endpoint, string address, ulong amountAtomic,
        IReadOnlyList<WalletOutpoint> selectedInputs, bool consolidation)
    {
        EnsureWalletOpen();
        string wallet = _walletName;
        WalletUnspentOutput[] coins = await ResolveSelectedInputsAsync(endpoint, selectedInputs);
        EnsureSameWallet(wallet);
        decimal total = coins.Sum(coin => coin.Amount);
        decimal amount = consolidation ? total : amountAtomic / 100_000_000m;
        if (amount > total) throw new InvalidOperationException("The selected outputs do not cover that amount and its fee.");
        JsonElement validation = await _rpc.CallAsync(endpoint, "", "validateaddress", new object[] { address });
        EnsureSameWallet(wallet);
        if (!ReadBoolean(validation, "isvalid")) throw new ArgumentException("Enter a valid address for this network.", nameof(address));
        string recipientScript = ReadString(validation, "scriptPubKey");
        if (recipientScript.Length == 0) throw new InvalidOperationException("The node did not return the receiving script.");
        Dictionary<string, object> options;
        bool reusedChange = false;
        if (consolidation)
        {
            JsonElement own = await _rpc.CallAsync(endpoint, wallet, "getaddressinfo", new object[] { address });
            if (!ReadBoolean(own, "ismine")) throw new InvalidOperationException("The consolidation destination does not belong to this wallet.");
            options = new() { ["include_unsafe"] = false, ["lockUnspents"] = false, ["conf_target"] = 6, ["estimate_mode"] = "conservative" };
        }
        else (options, reusedChange) = await FundingOptionsAsync(endpoint, wallet);
        EnsureSameWallet(wallet);
        object[] inputs = coins.Select(coin => (object)new { txid = coin.TransactionHash, vout = coin.OutputIndex }).ToArray();
        JsonElement raw = await _rpc.CallAsync(endpoint, "", "createrawtransaction", new object[] { inputs, new Dictionary<string, decimal> { [address] = amount } });
        EnsureSameWallet(wallet);
        options["add_inputs"] = false;
        if (consolidation) options["subtractFeeFromOutputs"] = new[] { 0 };
        JsonElement funded = await _rpc.CallAsync(endpoint, wallet, "fundrawtransaction", new object[] { raw.GetString()!, options });
        EnsureSameWallet(wallet);
        decimal fee = ReadDecimal(funded, "fee");
        if (fee <= 0 || fee >= total) throw new InvalidOperationException("The calculated fee is invalid or consumes the selected amount.");
        JsonElement signed = await _rpc.CallAsync(endpoint, wallet, "signrawtransactionwithwallet", new object[] { ReadString(funded, "hex") });
        EnsureSameWallet(wallet);
        if (!ReadBoolean(signed, "complete")) throw new InvalidOperationException("The wallet could not sign all selected inputs.");
        string hex = ReadString(signed, "hex");
        JsonElement decoded = await _rpc.CallAsync(endpoint, "", "decoderawtransaction", new object[] { hex });
        EnsureSameWallet(wallet);
        string[] actualInputs = decoded.GetProperty("vin").EnumerateArray()
            .Select(input => OutpointKey(new(ReadString(input, "txid"), checked((int)ReadInt64(input, "vout"))))).ToArray();
        HashSet<string> expectedInputs = coins.Select(coin => OutpointKey(coin.Outpoint)).ToHashSet(StringComparer.Ordinal);
        if (actualInputs.Length != expectedInputs.Count || !expectedInputs.SetEquals(actualInputs))
            throw new InvalidOperationException("The prepared payment did not use exactly the outputs you selected.");
        decimal expectedAmount = consolidation ? total - fee : amount;
        await VerifyPreparedOutputsAsync(endpoint, wallet, decoded, recipientScript, expectedAmount, total, fee, consolidation);
        EnsureSameWallet(wallet);
        return new(ReadString(decoded, "txid"), ToAtomic(fee), hex, address, ToAtomic(expectedAmount), actualInputs.Length, consolidation, reusedChange);
    }

    private async Task VerifyPreparedOutputsAsync(Uri endpoint, string wallet, JsonElement decoded,
        string recipientScript, decimal expectedAmount, decimal inputTotal, decimal fee, bool consolidation)
    {
        JsonElement[] outputs = decoded.GetProperty("vout").EnumerateArray().ToArray();
        JsonElement[] recipients = outputs.Where(output => ReadString(output.GetProperty("scriptPubKey"), "hex") == recipientScript).ToArray();
        if (outputs.Any(output => ReadDecimal(output, "value") <= 0) || recipients.Length != 1 || ReadDecimal(recipients[0], "value") != expectedAmount || inputTotal - outputs.Sum(output => ReadDecimal(output, "value")) != fee)
            throw new InvalidOperationException("The prepared payment amount or fee does not match its preview.");
        if (consolidation && outputs.Length != 1) throw new InvalidOperationException("Consolidation must create one wallet-owned output.");
        foreach (JsonElement change in outputs.Where(output => ReadString(output.GetProperty("scriptPubKey"), "hex") != recipientScript))
        {
            string changeAddress = ReadString(change.GetProperty("scriptPubKey"), "address");
            JsonElement own = await _rpc.CallAsync(endpoint, wallet, "getaddressinfo", new object[] { changeAddress });
            EnsureSameWallet(wallet);
            if (!ReadBoolean(own, "ismine")) throw new InvalidOperationException("The change output does not belong to this wallet.");
        }
        EnsureSameWallet(wallet);
    }

    public async Task<IReadOnlyList<WalletReceivingAddress>> GetReceivingAddressesAsync(Uri endpoint)
    {
        EnsureWalletOpen();
        string wallet = _walletName;
        JsonElement result = await _rpc.CallAsync(endpoint, wallet, "listreceivedbyaddress", new object?[] { 0, true, false, null, true }, LongWalletOperationTimeout);
        // The receive address book avoids one RPC per address on a large restored wallet.
        HashSet<string> owned = new(StringComparer.Ordinal);
        JsonElement labels = await _rpc.CallAsync(endpoint, wallet, "listlabels", new object[] { "receive" });
        foreach (JsonElement label in labels.EnumerateArray())
        {
            JsonElement entries = await _rpc.CallAsync(endpoint, wallet, "getaddressesbylabel", new object[] { label.GetString()! }, LongWalletOperationTimeout);
            foreach (JsonProperty entry in entries.EnumerateObject())
                if (ReadString(entry.Value, "purpose") == "receive") owned.Add(entry.Name);
        }
        List<WalletReceivingAddress> addresses = [];
        foreach (JsonElement item in result.EnumerateArray())
        {
            string address = ReadString(item, "address");
            if (!owned.Contains(address)) continue;
            bool used = item.TryGetProperty("txids", out JsonElement ids) && ids.GetArrayLength() > 0;
            addresses.Add(new(address, ReadString(item, "label"), ReadDecimal(item, "amount"), ReadInt64(item, "confirmations"), used));
        }
        if (_currentAddress.Length > 0 && addresses.All(item => item.Address != _currentAddress))
        {
            JsonElement info = await _rpc.CallAsync(endpoint, wallet, "getaddressinfo", new object[] { _currentAddress });
            if (ReadBoolean(info, "ismine") && !ReadBoolean(info, "ischange")) addresses.Add(new(_currentAddress, "", 0, 0, false));
        }
        EnsureSameWallet(wallet);
        return addresses.OrderByDescending(item => item.Address == _currentAddress).ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Address, StringComparer.Ordinal).ToArray();
    }

    public async Task<WalletReceivingAddress> CreateReceivingAddressAsync(Uri endpoint, string label = "")
    {
        EnsureWalletOpen();
        if (label.Length > 128 || label.Any(char.IsControl)) throw new ArgumentException("Use a label of up to 128 characters without control characters.", nameof(label));
        string wallet = _walletName;
        await EnsureRecoveryGenerationRangeAsync(endpoint, internalAddress: false);
        EnsureSameWallet(wallet);
        JsonElement result = await _rpc.CallAsync(endpoint, wallet, "getnewaddress", new object[] { label, "bech32" }, LongWalletOperationTimeout);
        EnsureSameWallet(wallet);
        string address = result.GetString() ?? throw new InvalidOperationException("The wallet did not return a receiving address.");
        _currentAddress = address;
        return new(address, label, 0, 0, false);
    }

    private async Task EnsureRecoveryGenerationRangeAsync(Uri endpoint, bool internalAddress)
    {
        string wallet = _walletName;
        JsonElement result = await _rpc.CallAsync(endpoint, wallet, "listdescriptors", new object[] { false }, LongWalletOperationTimeout);
        EnsureSameWallet(wallet);
        foreach (JsonElement descriptor in result.GetProperty("descriptors").EnumerateArray())
        {
            if (!ReadBoolean(descriptor, "active") || ReadBoolean(descriptor, "internal") != internalAddress) continue;
            string expression = ReadString(descriptor, "desc");
            // The GUI issues bech32 addresses; do not impose this guard on unrelated descriptor types.
            if (!expression.StartsWith("wpkh(", StringComparison.Ordinal) || !expression.Contains("/*", StringComparison.Ordinal)) continue;
            long next = descriptor.TryGetProperty("next_index", out JsonElement index) ? index.GetInt64() : ReadInt64(descriptor, "next");
            if (next > SlithySeedPhrase.DefaultRecoveryRangeEnd)
                throw new RecoveryWindowExceededException("This wallet has reached the app's 10,000-index recovery window. Back up the wallet file and arrange a deeper recovery scan before creating another receiving or change address. This is an app safeguard, not an address or blockchain limit.");
        }
    }

    public async Task<bool> CheckRecoveryWordsAsync(Uri endpoint, string words)
    {
        EnsureWalletOpen();
        string wallet = _walletName;
        JsonElement chain = await _rpc.CallAsync(endpoint, "", "getblockchaininfo", Array.Empty<object>());
        string[] descriptors = SlithySeedPhrase.BuildDescriptors(words, ReadString(chain, "chain"));
        HashSet<string> candidates = new(StringComparer.Ordinal);
        foreach (string descriptor in descriptors)
        {
            JsonElement info = await _rpc.CallAsync(endpoint, "", "getdescriptorinfo", new object[] { descriptor });
            candidates.Add(ReadString(info, "descriptor"));
        }
        JsonElement current = await _rpc.CallAsync(endpoint, wallet, "listdescriptors", new object[] { false }, LongWalletOperationTimeout);
        HashSet<string> actual = current.GetProperty("descriptors").EnumerateArray().Where(item => ReadBoolean(item, "active"))
            .Select(item => ReadString(item, "desc")).ToHashSet(StringComparer.Ordinal);
        EnsureSameWallet(wallet);
        return candidates.Count == 2 && candidates.IsSubsetOf(actual);
    }
}
