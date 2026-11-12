//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Wallet RPC Client
//===============================================
using System.Globalization;
using System.Text.Json;

namespace Slithy_Tove;

// A simple balance and address snapshot for the open wallet.
internal sealed record WalletSnapshot(
    string Address,
    ulong BalanceAtomic,
    ulong UnlockedBalanceAtomic,
    long BlocksToUnlock,
    long LastProcessedBlockHeight,
    ulong ImmatureBalanceAtomic = 0,
    ulong PendingBalanceAtomic = 0)
{
    public decimal Balance => BalanceAtomic / 100_000_000m;
    public decimal UnlockedBalance => UnlockedBalanceAtomic / 100_000_000m;
    public decimal ImmatureBalance => ImmatureBalanceAtomic / 100_000_000m;
    public decimal PendingBalance => PendingBalanceAtomic / 100_000_000m;
}

// This tells the UI whether the wallet is still scanning blocks.
internal sealed record WalletScanStatus(bool Scanning, decimal Progress, long LastProcessedBlockHeight);

// A transfer is prepared first so the send dialog can show or confirm details.
internal sealed record PreparedTransfer(
    string TransactionHash,
    ulong FeeAtomic,
    string TransactionMetadata,
    string RecipientAddress = "",
    ulong AmountAtomic = 0,
    int InputCount = 0,
    bool IsConsolidation = false,
    bool ReusesChangeAddress = false);

internal sealed record TransferResult(string TransactionHash, ulong FeeAtomic);

internal sealed record AddressValidation(
    bool Valid,
    bool Integrated,
    bool Subaddress,
    string NetworkType);

internal sealed record WalletTransfer(
    string TransactionHash,
    string Direction,
    decimal Amount,
    decimal Fee,
    long Height,
    DateTimeOffset? Timestamp,
    string Address = "",
    long Confirmations = 0,
    string Category = "",
    bool Abandoned = false,
    int OutputIndex = -1);

internal sealed record WalletHistoryPage(IReadOnlyList<WalletTransfer> Entries, int Page, int PageSize, bool HasMore, int ScannedEntries);
internal sealed record WalletReceivingAddress(string Address, string Label, decimal ReceivedAmount, long Confirmations, bool Used);
internal sealed record WalletOutpoint(string TransactionHash, int OutputIndex);
internal sealed record WalletUnspentOutput(string TransactionHash, int OutputIndex, string Address, decimal Amount, long Confirmations, bool Spendable, bool Safe, string Label)
{
    public WalletOutpoint Outpoint => new(TransactionHash, OutputIndex);
}
internal sealed record WalletTransactionInput(string TransactionHash, int OutputIndex, bool Coinbase);
internal sealed record WalletTransactionOutput(int OutputIndex, string Address, decimal Amount, bool IsMine);
internal sealed record WalletTransactionDetails(string TransactionHash, long Confirmations, DateTimeOffset? Timestamp, decimal Fee, string BlockHash, bool Abandoned,
    IReadOnlyList<WalletTransactionInput> Inputs, IReadOnlyList<WalletTransactionOutput> Outputs, IReadOnlyList<WalletTransfer> WalletEntries);

// This class talks to the wallet RPC methods through the same node RPC transport.
// It tracks which wallet is open so the UI does not have to pass that around.
internal sealed partial class WalletRpcClient : IDisposable
{
    private static readonly TimeSpan LongWalletOperationTimeout = TimeSpan.FromMinutes(3);
    private readonly NodeRpcClient _rpc = new();
    private string _walletName = "";
    private string _currentAddress = "";
    private bool _walletEncrypted;

    public async Task<bool> IsReadyAsync(Uri endpoint)
    {
        // A quick chain call is enough to know whether the RPC endpoint is reachable.
        try
        {
            await _rpc.CallAsync(endpoint, "", "getblockchaininfo", Array.Empty<object>());
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task CreateWalletAsync(Uri endpoint, string name, string password, TimeSpan? unlockFor = null)
    {
        // Create the wallet, remember its name, then unlock it if it has a password.
        await _rpc.CallAsync(endpoint, "", "createwallet", new object[]
        {
            name,
            false,
            false,
            password,
            false,
            true,
            false
        }, LongWalletOperationTimeout);
        _walletName = name;
        _currentAddress = "";
        _walletEncrypted = !string.IsNullOrEmpty(password);
        if (_walletEncrypted)
        {
            await UnlockWalletAsync(endpoint, password, unlockFor ?? TimeSpan.FromMinutes(10));
        }
    }

    public async Task<string> CreateSeedWalletAsync(Uri endpoint, string name, string password, TimeSpan? unlockFor = null)
    {
        // New launch wallets are deterministic. The recovery words rebuild the
        // same descriptors later if the user needs to restore the wallet.
        string recoveryWords = SlithySeedPhrase.CreateRecoveryWords();
        await CreateWalletFromRecoveryWordsAsync(
            endpoint,
            name,
            password,
            recoveryWords,
            scanFromStart: false,
            unlockFor);
        return recoveryWords;
    }

    public async Task OpenWalletAsync(Uri endpoint, string name, string password, TimeSpan? unlockFor = null)
    {
        // Loading a wallet can say "already loaded" if the daemon still has it open.
        // That is not a real error for the user, so the open flow continues.
        try
        {
            await _rpc.CallAsync(endpoint, "", "loadwallet", new object[] { name, false }, LongWalletOperationTimeout);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("already loaded", StringComparison.OrdinalIgnoreCase))
        {
            // Already available.
        }
        _walletName = name;
        _currentAddress = "";
        _walletEncrypted = await IsWalletEncryptedAsync(endpoint);
        if (_walletEncrypted)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("This wallet is encrypted. Enter the wallet password.");
            }
            await UnlockWalletAsync(endpoint, password, unlockFor ?? TimeSpan.FromMinutes(10));
        }
        else if (!string.IsNullOrEmpty(password))
        {
            await _rpc.CallAsync(endpoint, _walletName, "encryptwallet", new object[] { password }, LongWalletOperationTimeout);
            _walletEncrypted = true;
            await UnlockWalletAsync(endpoint, password, unlockFor ?? TimeSpan.FromMinutes(10));
        }
    }

    public async Task UnlockWalletAsync(Uri endpoint, string password, TimeSpan unlockFor)
    {
        // Unlocking is timed. After the time expires, spending needs the password again.
        EnsureWalletOpen();
        if (string.IsNullOrEmpty(password))
        {
            return;
        }
        int seconds = Math.Clamp((int)Math.Ceiling(unlockFor.TotalSeconds), 1, 86_400);
        try
        {
            await _rpc.CallAsync(
                endpoint,
                _walletName,
                "walletpassphrase",
                new object[] { password, seconds },
                LongWalletOperationTimeout);
            _walletEncrypted = true;
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("not encrypted", StringComparison.OrdinalIgnoreCase))
        {
            _walletEncrypted = false;
        }
    }

    public async Task LockWalletOnlyAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        EnsureWalletOpen();
        string wallet = _walletName;
        if (!_walletEncrypted && !await IsWalletEncryptedAsync(endpoint, cancellationToken))
        {
            return;
        }
        EnsureSameWallet(wallet);
        await _rpc.CallAsync(endpoint, wallet, "walletlock", Array.Empty<object>(), LongWalletOperationTimeout, cancellationToken);
        EnsureSameWallet(wallet);
        JsonElement locked = await _rpc.CallAsync(endpoint, wallet, "getwalletinfo", Array.Empty<object>(), LongWalletOperationTimeout, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSameWallet(wallet);
        if (!locked.TryGetProperty("unlocked_until", out JsonElement until) || until.GetInt64() != 0)
            throw new InvalidOperationException("Wallet spending lock could not be confirmed.");
        _walletEncrypted = true;
    }

    public async Task<bool> NeedsSpendingPasswordAsync(Uri endpoint)
    {
        EnsureWalletOpen();
        JsonElement info = await _rpc.CallAsync(endpoint, _walletName, "getwalletinfo", Array.Empty<object>());
        return info.TryGetProperty("unlocked_until", out JsonElement until) &&
               until.GetInt64() <= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public async Task ChangePasswordAsync(Uri endpoint, string oldPassword, string newPassword)
    {
        EnsureWalletOpen();
        await _rpc.CallAsync(endpoint, _walletName, "walletpassphrasechange", new object[]
        {
            oldPassword,
            newPassword
        });
        _walletEncrypted = true;
    }

    public async Task RestoreWalletAsync(
        Uri endpoint,
        string name,
        string password,
        string recoveryWords,
        ulong restoreHeight,
        int recoveryRangeEnd = SlithySeedPhrase.DefaultRecoveryRangeEnd,
        CancellationToken cancellationToken = default)
    {
        // Restore scans from the start of the test chain. The current chain is
        // still small, so this is safer than asking users for a restore height.
        await CreateWalletFromRecoveryWordsAsync(
            endpoint,
            name,
            password,
            recoveryWords,
            scanFromStart: true,
            unlockFor: TimeSpan.FromMinutes(10),
            recoveryRangeEnd,
            cancellationToken);
    }

    private async Task CreateWalletFromRecoveryWordsAsync(
        Uri endpoint,
        string name,
        string password,
        string recoveryWords,
        bool scanFromStart,
        TimeSpan? unlockFor,
        int recoveryRangeEnd = SlithySeedPhrase.DefaultRecoveryRangeEnd,
        CancellationToken cancellationToken = default)
    {
        SlithySeedPhrase.ValidateRecoveryRange(recoveryRangeEnd);
        cancellationToken.ThrowIfCancellationRequested();
        JsonElement chain = await _rpc.CallAsync(endpoint, "", "getblockchaininfo", Array.Empty<object>());
        string network = chain.GetProperty("chain").GetString() ?? throw new InvalidOperationException("Node did not identify its network.");
        string[] descriptors = SlithySeedPhrase.BuildDescriptors(recoveryWords, network);
        string[] checkedDescriptors =
        [
            await GetCheckedDescriptorAsync(endpoint, descriptors[0]),
            await GetCheckedDescriptorAsync(endpoint, descriptors[1])
        ];

        await _rpc.CallAsync(endpoint, "", "createwallet", new object[]
        {
            name,
            false,
            true,
            password,
            false,
            true,
            false
        }, LongWalletOperationTimeout);
        _walletName = name;
        _currentAddress = "";
        _walletEncrypted = !string.IsNullOrEmpty(password);
        if (_walletEncrypted)
        {
            await UnlockWalletAsync(endpoint, password, unlockFor ?? TimeSpan.FromMinutes(10));
        }

        try
        {
            Task<JsonElement> importTask = _rpc.CallAsync(
                endpoint,
                _walletName,
                "importdescriptors",
                new object[]
                {
                    SlithySeedPhrase.BuildImportRequests(checkedDescriptors, scanFromStart, recoveryRangeEnd)
                },
                TimeSpan.FromMinutes(15));
            JsonElement importResult;
            try { importResult = await importTask.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Stop the scan, then let the import release its locks before locking spending.
                try { await _rpc.CallAsync(endpoint, _walletName, "abortrescan", Array.Empty<object>(), TimeSpan.FromSeconds(15)); }
                catch { /* The import may still be deriving keys or may already have finished. */ }
                try { await importTask.WaitAsync(TimeSpan.FromSeconds(30)); }
                catch { /* The outer failure path still attempts and verifies the wallet lock. */ }
                throw;
            }
            SlithySeedPhrase.ThrowIfImportFailed(importResult);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception importError)
        {
            // A failed import must not leave an encrypted wallet unlocked.
            try { await LockWalletOnlyAsync(endpoint); }
            catch (Exception lockError)
            {
                throw new AggregateException("Wallet import failed, and locking could not be confirmed.", importError, lockError);
            }
            const string recoveryMessage = "The wallet was created, but setup or its recovery scan did not finish. Spending is locked. Open this wallet again, check the scan and make a wallet-file backup before using it.";
            if (importError is OperationCanceledException && cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(recoveryMessage, importError, cancellationToken);
            throw new InvalidOperationException(recoveryMessage, importError);
        }
    }

    private async Task<string> GetCheckedDescriptorAsync(Uri endpoint, string descriptor)
    {
        JsonElement result = await _rpc.CallAsync(
            endpoint,
            "",
            "getdescriptorinfo",
            new object[] { descriptor },
            LongWalletOperationTimeout);
        string checksum = ReadString(result, "checksum");
        return string.IsNullOrWhiteSpace(checksum)
            ? descriptor
            : $"{descriptor}#{checksum}";
    }

    public async Task CloseWalletAsync(Uri endpoint)
    {
        // Unload the wallet from the node if possible, then clear local state either way.
        if (string.IsNullOrWhiteSpace(_walletName))
        {
            return;
        }

        try
        {
            await _rpc.CallAsync(endpoint, "", "unloadwallet", new object[] { _walletName, false });
        }
        catch
        {
            // Closing is best effort because the daemon may already be shutting down.
        }
        _walletName = "";
        _currentAddress = "";
        _walletEncrypted = false;
    }

    public async Task RefreshAsync(Uri endpoint, long? startHeight = null)
    {
        // Ask Core to rescan the loaded wallet before reading the displayed balance.
        // This catches blocks found while the wallet was closed or mid-startup.
        EnsureWalletOpen();
        try
        {
            object parameters = startHeight is >= 0
                ? new object[] { startHeight.Value }
                : Array.Empty<object>();
            await _rpc.CallAsync(endpoint, _walletName, "rescanblockchain", parameters, LongWalletOperationTimeout);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("already in progress", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("currently rescanning", StringComparison.OrdinalIgnoreCase))
        {
            // The caller waits for scan status before it reads the balance.
        }
    }

    public Task SetDaemonAsync(Uri endpoint, Uri daemonUri) => Task.CompletedTask;

    public async Task<IReadOnlyList<string>> ListWalletNamesAsync(Uri endpoint)
    {
        // The wallet list is used by the open-wallet screen.
        JsonElement result = await _rpc.CallAsync(endpoint, "", "listwalletdir", Array.Empty<object>());
        if (!result.TryGetProperty("wallets", out JsonElement wallets) ||
            wallets.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<string> names = [];
        foreach (JsonElement wallet in wallets.EnumerateArray())
        {
            string name = ReadString(wallet, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }
        }

        return names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToArray();
    }

    public void UseKnownAddress(string address)
    {
        if (!string.IsNullOrWhiteSpace(address))
        {
            _currentAddress = address;
        }
    }

    public async Task<WalletScanStatus> GetScanStatusAsync(Uri endpoint)
    {
        // The node reports scanning as false, true, or an object with progress.
        EnsureWalletOpen();
        JsonElement info = await _rpc.CallAsync(endpoint, _walletName, "getwalletinfo", Array.Empty<object>());
        if (!info.TryGetProperty("scanning", out JsonElement scanning))
        {
            return new WalletScanStatus(false, 1m, ReadLastProcessedBlockHeight(info));
        }

        return scanning.ValueKind switch
        {
            JsonValueKind.False => new WalletScanStatus(false, 1m, ReadLastProcessedBlockHeight(info)),
            JsonValueKind.True => new WalletScanStatus(true, 0m, ReadLastProcessedBlockHeight(info)),
            JsonValueKind.Object => new WalletScanStatus(
                true,
                scanning.TryGetProperty("progress", out JsonElement progress) &&
                progress.TryGetDecimal(out decimal value)
                    ? Math.Clamp(value, 0m, 1m)
                    : 0m,
                ReadLastProcessedBlockHeight(info)),
            _ => new WalletScanStatus(false, 1m, ReadLastProcessedBlockHeight(info))
        };
    }

    public async Task<WalletSnapshot> GetSnapshotAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        // Reuse a known receiving address so opening a restored wallet does not consume a key.
        EnsureWalletOpen();
        cancellationToken.ThrowIfCancellationRequested();
        string wallet = _walletName;
        if (string.IsNullOrWhiteSpace(_currentAddress))
        {
            IReadOnlyList<WalletReceivingAddress> addresses = await GetReceivingAddressesAsync(endpoint, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSameWallet(wallet);
            _currentAddress = addresses.FirstOrDefault()?.Address ?? "";
            if (_currentAddress.Length == 0)
            {
                try { _currentAddress = (await CreateReceivingAddressAsync(endpoint, cancellationToken: cancellationToken)).Address; }
                catch (RecoveryWindowExceededException)
                {
                    _currentAddress = await GetRecoverySafeReceivingAddressAsync(endpoint, wallet, cancellationToken)
                        ?? throw new InvalidOperationException("No receiving address is available. Back up this wallet file before changing its recovery range.");
                }
            }
            EnsureSameWallet(wallet);
        }

        JsonElement balanceResult = await _rpc.CallAsync(
            endpoint, wallet, "getbalances", Array.Empty<object>(), cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSameWallet(wallet);
        JsonElement mine = balanceResult.GetProperty("mine");
        decimal trusted = ReadDecimal(mine, "trusted");
        decimal immature = ReadDecimal(mine, "immature");
        decimal pending = ReadDecimal(mine, "untrusted_pending");
        ulong trustedAtomic = ToAtomic(trusted);
        ulong totalAtomic = ToAtomic(trusted + pending + immature);
        long blocksToUnlock = immature > 0 ? -1 : 0; // -1 means immature; no invented countdown.
        long lastProcessedBlockHeight = 0;
        if (balanceResult.TryGetProperty("lastprocessedblock", out JsonElement block) &&
            block.ValueKind == JsonValueKind.Object)
        {
            lastProcessedBlockHeight = ReadInt64(block, "height");
        }

        return new WalletSnapshot(
            _currentAddress,
            totalAtomic,
            trustedAtomic,
            blocksToUnlock,
            lastProcessedBlockHeight,
            ToAtomic(immature),
            ToAtomic(pending));
    }

    public async Task<AddressValidation> ValidateAddressAsync(Uri endpoint, string address)
    {
        JsonElement result = await _rpc.CallAsync(
            endpoint, "", "validateaddress", new object[] { address });
        return new AddressValidation(
            ReadBoolean(result, "isvalid"),
            false,
            false,
            ReadBoolean(result, "isvalid") ? "test" : "");
    }

    public async Task<PreparedTransfer> PrepareTransferAsync(
        Uri endpoint, string address, ulong amountAtomic)
    {
        EnsureWalletOpen();
        if (amountAtomic == 0) throw new ArgumentOutOfRangeException(nameof(amountAtomic));
        string wallet = _walletName;
        JsonElement validation = await _rpc.CallAsync(endpoint, "", "validateaddress", new object[] { address });
        EnsureSameWallet(wallet);
        string recipientScript = ReadString(validation, "scriptPubKey");
        if (!ReadBoolean(validation, "isvalid") || recipientScript.Length == 0)
            throw new ArgumentException("Enter a valid address for this network.", nameof(address));
        (Dictionary<string, object> options, bool reusedChange) = await FundingOptionsAsync(endpoint, wallet);
        EnsureSameWallet(wallet);
        decimal amount = amountAtomic / 100_000_000m;
        JsonElement raw = await _rpc.CallAsync(endpoint, "", "createrawtransaction",
            new object[] { Array.Empty<object>(), new Dictionary<string, decimal> { [address] = amount } });
        EnsureSameWallet(wallet);
        JsonElement funded = await _rpc.CallAsync(endpoint, wallet, "fundrawtransaction",
            new object[] { raw.GetString()!, options });
        EnsureSameWallet(wallet);
        decimal fee = funded.GetProperty("fee").GetDecimal();
        if (fee <= 0) throw new InvalidOperationException("The node returned an invalid transaction fee.");
        JsonElement signed = await _rpc.CallAsync(endpoint, wallet, "signrawtransactionwithwallet",
            new object[] { funded.GetProperty("hex").GetString()! });
        EnsureSameWallet(wallet);
        if (!signed.GetProperty("complete").GetBoolean())
            throw new InvalidOperationException("The wallet could not sign the complete transaction.");
        string hex = signed.GetProperty("hex").GetString()!;
        JsonElement decoded = await _rpc.CallAsync(endpoint, "", "decoderawtransaction", new object[] { hex });
        EnsureSameWallet(wallet);
        // Resolve amounts in one wallet call, including safe unconfirmed change.
        JsonElement unspent = await _rpc.CallAsync(endpoint, wallet, "listunspent", new object[] { 0, 9999999, Array.Empty<string>(), false }, LongWalletOperationTimeout);
        EnsureSameWallet(wallet);
        Dictionary<string, decimal> available = unspent.EnumerateArray().Where(coin => ReadBoolean(coin, "spendable") && ReadBoolean(coin, "safe"))
            .ToDictionary(coin => OutpointKey(new(ReadString(coin, "txid"), checked((int)ReadInt64(coin, "vout")))), coin => ReadDecimal(coin, "amount"), StringComparer.Ordinal);
        JsonElement[] inputs = decoded.GetProperty("vin").EnumerateArray().ToArray();
        HashSet<string> used = new(StringComparer.Ordinal);
        decimal inputTotal = 0;
        foreach (JsonElement input in inputs)
        {
            string key = OutpointKey(new(ReadString(input, "txid"), checked((int)ReadInt64(input, "vout"))));
            if (!used.Add(key) || !available.TryGetValue(key, out decimal value) || value <= 0)
                throw new InvalidOperationException("A prepared input is no longer available. Refresh the wallet and prepare the payment again.");
            inputTotal += value;
        }
        if (inputs.Length == 0 || fee >= inputTotal) throw new InvalidOperationException("The prepared payment has invalid inputs or fees.");
        await VerifyPreparedOutputsAsync(endpoint, wallet, decoded, recipientScript, amount, inputTotal, fee, false);
        EnsureSameWallet(wallet);
        return new PreparedTransfer(decoded.GetProperty("txid").GetString()!, ToAtomic(fee), hex,
            address, amountAtomic, inputs.Length, false, reusedChange);
    }

    public async Task<bool> HasTransactionAsync(Uri endpoint, string transactionId)
    {
        EnsureWalletOpen();
        try
        {
            JsonElement transaction = await _rpc.CallAsync(endpoint, _walletName, "gettransaction", new object[] { transactionId });
            return ReadString(transaction, "txid") == transactionId;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Invalid or non-wallet transaction", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
    }

    public async Task<TransferResult> RelayTransferAsync(Uri endpoint, PreparedTransfer prepared)
    {
        EnsureWalletOpen();
        // Broadcast the exact signed transaction shown in the confirmation.
        // Retrying these same bytes cannot create a second payment.
        try
        {
            JsonElement result = await _rpc.CallAsync(endpoint, "", "sendrawtransaction",
                new object[] { prepared.TransactionMetadata });
            return new TransferResult(result.GetString() ?? prepared.TransactionHash, prepared.FeeAtomic);
        }
        catch (Exception original)
        {
            try
            {
                if (await HasTransactionAsync(endpoint, prepared.TransactionHash))
                    return new TransferResult(prepared.TransactionHash, prepared.FeeAtomic);
            }
            catch { /* Keep the original submission failure for diagnosis. */ }
            throw new InvalidOperationException(
                $"The send result is unknown. Check transaction {prepared.TransactionHash} before sending again.", original);
        }
    }

    public async Task BackupWalletAsync(Uri endpoint, string destinationFile)
    {
        EnsureWalletOpen();
        if (File.Exists(destinationFile)) throw new IOException("Backup file already exists.");
        // Core takes a consistent database snapshot, even while peers send updates.
        await _rpc.CallAsync(endpoint, _walletName, "backupwallet",
            new object[] { Path.GetFullPath(destinationFile) }, LongWalletOperationTimeout);
        if (!File.Exists(destinationFile)) throw new IOException("The node did not create the wallet backup.");
    }

    public Task<string> GetTransactionProofAsync(
        Uri endpoint,
        string transactionHash,
        string address,
        string message) =>
        throw new NotSupportedException(
            "Transaction proofs are not available in this wallet yet.");

    public async Task<IReadOnlyList<WalletTransfer>> GetTransfersAsync(Uri endpoint)
    {
        // Recent transactions feed the activity tab. Newest items are returned first.
        EnsureWalletOpen();
        JsonElement result = await _rpc.CallAsync(
            endpoint, _walletName, "listtransactions", new object[] { "*", 20, 0, true });
        List<WalletTransfer> transfers = [];
        if (result.ValueKind != JsonValueKind.Array)
        {
            return transfers;
        }

        foreach (JsonElement item in result.EnumerateArray())
        {
            transfers.Add(ParseTransfer(item));
        }

        return transfers
            .OrderByDescending(transfer => transfer.Timestamp ?? DateTimeOffset.MinValue)
            .ThenByDescending(transfer => transfer.Height)
            .ToArray();
    }

    private void EnsureWalletOpen()
    {
        // Almost every wallet operation needs a selected wallet name.
        if (string.IsNullOrWhiteSpace(_walletName))
        {
            throw new InvalidOperationException("Open or create a wallet first.");
        }
    }

    private async Task<bool> IsWalletEncryptedAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        EnsureWalletOpen();
        JsonElement info = await _rpc.CallAsync(endpoint, _walletName, "getwalletinfo", Array.Empty<object>(), cancellationToken: cancellationToken);
        return info.TryGetProperty("unlocked_until", out _);
    }

    private static ulong ToAtomic(decimal amount) =>
        // Slithy uses 8 decimal places here, so 1 SLTHY is 100,000,000 atomic units.
        checked((ulong)Math.Round(amount * 100_000_000m, MidpointRounding.AwayFromZero));

    private static string ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) ? value.ToString() : "";

    private static long ReadInt64(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long result) ? result : 0;

    private static decimal ReadDecimal(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetDecimal(out decimal result) ? result : 0;

    private static bool ReadBoolean(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;

    private static long ReadLastProcessedBlockHeight(JsonElement walletInfo)
    {
        if (!walletInfo.TryGetProperty("lastprocessedblock", out JsonElement block) ||
            block.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return ReadInt64(block, "height");
    }

    public void Dispose() => _rpc.Dispose();
}

