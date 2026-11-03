using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Slithy_Tove;

internal sealed partial class WalletRpcClient
{
    public const int MaximumHistoryExportBytes = 64 * 1024 * 1024;
    private sealed record HistoryCursor(string Wallet, string Revision)
    {
        public int Scanned { get; set; }
    }

    private async Task<string> HistoryRevisionAsync(Uri endpoint, string wallet, CancellationToken cancellationToken)
    {
        JsonElement info = await _rpc.CallAsync(endpoint, wallet, "getwalletinfo", Array.Empty<object>(), TimeSpan.FromSeconds(15)).WaitAsync(cancellationToken);
        if (info.TryGetProperty("scanning", out JsonElement scanning) && scanning.ValueKind is not (JsonValueKind.False or JsonValueKind.Null))
            throw new InvalidOperationException("The wallet is still scanning blocks. Wait for the scan to finish before reading or exporting its full history.");
        string hash = info.TryGetProperty("lastprocessedblock", out JsonElement block) ? ReadString(block, "hash") : "";
        return ReadInt64(info, "txcount").ToString(CultureInfo.InvariantCulture) + ":" + hash;
    }

    private async Task<HistoryCursor> StartHistoryAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        EnsureWalletOpen();
        string wallet = _walletName;
        return new(wallet, await HistoryRevisionAsync(endpoint, wallet, cancellationToken));
    }

    private async Task CheckHistoryAsync(Uri endpoint, HistoryCursor cursor, CancellationToken cancellationToken)
    {
        EnsureSameWallet(cursor.Wallet);
        if (await HistoryRevisionAsync(endpoint, cursor.Wallet, cancellationToken) != cursor.Revision)
            throw new InvalidOperationException("Wallet history changed while it was being read. Refresh and try again so the page or export uses one chain snapshot.");
    }

    public async Task<WalletHistoryPage> GetHistoryPageAsync(Uri endpoint, int page = 0, int pageSize = 50,
        string search = "", string direction = "all", string status = "all", CancellationToken cancellationToken = default)
    {
        if (page < 0 || pageSize is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(page));
        int skipMatches = checked(page * pageSize);
        List<WalletTransfer> entries = [];
        int matches = 0;
        HistoryCursor cursor = await StartHistoryAsync(endpoint, cancellationToken);
        await foreach (WalletTransfer transfer in ReadHistoryAsync(endpoint, search, direction, status, cursor, cancellationToken))
        {
            if (matches++ < skipMatches) continue;
            if (entries.Count == pageSize)
            {
                await CheckHistoryAsync(endpoint, cursor, cancellationToken);
                return new(entries, page, pageSize, true, cursor.Scanned);
            }
            entries.Add(transfer);
        }
        await CheckHistoryAsync(endpoint, cursor, cancellationToken);
        return new(entries, page, pageSize, false, cursor.Scanned);
    }

    public async Task<string> ExportHistoryCsvAsync(Uri endpoint, string search = "", string direction = "all",
        string status = "all", CancellationToken cancellationToken = default)
    {
        StringBuilder text = new(WalletHistoryCsv.Header);
        int bytes = Encoding.UTF8.GetByteCount(WalletHistoryCsv.Header);
        HistoryCursor cursor = await StartHistoryAsync(endpoint, cancellationToken);
        await foreach (WalletTransfer transfer in ReadHistoryAsync(endpoint, search, direction, status, cursor, cancellationToken))
        {
            string row = WalletHistoryCsv.Row(transfer);
            bytes = checked(bytes + Encoding.UTF8.GetByteCount(row));
            if (bytes > MaximumHistoryExportBytes) throw new InvalidOperationException("The CSV would exceed 64 MiB. Narrow the search or status filter and export again.");
            text.Append(row);
        }
        await CheckHistoryAsync(endpoint, cursor, cancellationToken);
        return text.ToString();
    }

    private async IAsyncEnumerable<WalletTransfer> ReadHistoryAsync(Uri endpoint, string search, string direction,
        string status, HistoryCursor cursor, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        EnsureWalletOpen();
        string wallet = cursor.Wallet;
        string wantedDirection = direction.Trim().ToLowerInvariant();
        string wantedStatus = status.Trim().ToLowerInvariant();
        if (wantedDirection is not ("all" or "in" or "out" or "mining" or "self")) throw new ArgumentException("Unknown history direction.", nameof(direction));
        if (wantedStatus is not ("all" or "confirmed" or "pending" or "conflicted")) throw new ArgumentException("Unknown confirmation filter.", nameof(status));
        string needle = search.Trim();
        Dictionary<string, bool> ownership = new(StringComparer.Ordinal);
        HashSet<string> entriesSeen = new(StringComparer.Ordinal);
        const int batch = 500;
        for (int offset = 0; ; offset = checked(offset + batch))
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSameWallet(wallet);
            JsonElement result = await _rpc.CallAsync(endpoint, wallet, "listtransactions", new object[] { "*", batch, offset, true }, TimeSpan.FromSeconds(30)).WaitAsync(cancellationToken);
            if (result.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("The node returned an invalid wallet history.");
            await CheckHistoryAsync(endpoint, cursor, cancellationToken);
            // Core returns each window oldest first. Reversing preserves its entry ordering.
            foreach (JsonElement item in result.EnumerateArray().Reverse())
            {
                cancellationToken.ThrowIfCancellationRequested();
                cursor.Scanned++;
                WalletTransfer row = ParseTransfer(item);
                string identity = row.TransactionHash + ":" + row.OutputIndex + ":" + row.Category + ":" + row.Address;
                if (!entriesSeen.Add(identity)) continue;
                if (row.Category == "send" && !string.IsNullOrEmpty(row.Address))
                {
                    if (!ownership.TryGetValue(row.Address, out bool own))
                    {
                        JsonElement info = await _rpc.CallAsync(endpoint, wallet, "getaddressinfo", new object[] { row.Address }, TimeSpan.FromSeconds(15)).WaitAsync(cancellationToken);
                        ownership[row.Address] = own = ReadBoolean(info, "ismine");
                    }
                    if (own) row = row with { Direction = "Self" };
                }
                if (wantedDirection != "all" && wantedDirection != (row.Direction switch { "Sent" => "out", "Self" => "self", "Mined" => "mining", _ => "in" })) continue;
                string rowStatus = row.Confirmations < 0 || row.Abandoned || row.Category == "orphan" ? "conflicted" : row.Confirmations > 0 ? "confirmed" : "pending";
                if (wantedStatus != "all" && wantedStatus != rowStatus) continue;
                if (needle.Length != 0 && !new[] { row.TransactionHash, row.Address, row.Direction, row.Category, ReadString(item, "label"), row.Amount.ToString(CultureInfo.InvariantCulture) }
                    .Any(value => value.Contains(needle, StringComparison.OrdinalIgnoreCase))) continue;
                EnsureSameWallet(wallet);
                yield return row;
            }
            if (result.GetArrayLength() < batch) yield break;
        }
    }

    private static WalletTransfer ParseTransfer(JsonElement item, JsonElement? transaction = null)
    {
        JsonElement meta = transaction ?? item;
        string category = ReadString(item, "category");
        string direction = category switch { "send" => "Sent", "generate" or "immature" or "orphan" => "Mined", _ => "Received" };
        long time = ReadInt64(meta, "time");
        return new(ReadString(meta, "txid"), direction, Math.Abs(ReadDecimal(item, "amount")), Math.Abs(ReadDecimal(item, "fee")),
            ReadInt64(meta, "blockheight"), SafeTimestamp(time), ReadString(item, "address"), ReadInt64(meta, "confirmations"), category, ReadBoolean(item, "abandoned"),
            item.TryGetProperty("vout", out JsonElement index) ? index.GetInt32() : -1);
    }

    private static DateTimeOffset? SafeTimestamp(long seconds) => seconds is > 0 and <= 253402300799 ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;

    private void EnsureSameWallet(string expected)
    {
        if (!string.Equals(expected, _walletName, StringComparison.Ordinal)) throw new InvalidOperationException("The open wallet changed. Try again with the wallet now shown.");
    }

    public async Task<WalletTransactionDetails> GetTransactionDetailsAsync(Uri endpoint, string transactionHash, CancellationToken cancellationToken = default)
    {
        EnsureWalletOpen();
        ValidateTransactionHash(transactionHash);
        string wallet = _walletName;
        cancellationToken.ThrowIfCancellationRequested();
        JsonElement transaction = await _rpc.CallAsync(endpoint, wallet, "gettransaction", new object[] { transactionHash, true, true }, TimeSpan.FromSeconds(30)).WaitAsync(cancellationToken);
        EnsureSameWallet(wallet);
        JsonElement decoded = transaction.TryGetProperty("decoded", out JsonElement value) ? value :
            await _rpc.CallAsync(endpoint, "", "decoderawtransaction", new object[] { ReadString(transaction, "hex") }, TimeSpan.FromSeconds(30)).WaitAsync(cancellationToken);
        List<WalletTransactionInput> inputs = [];
        foreach (JsonElement input in decoded.GetProperty("vin").EnumerateArray())
            inputs.Add(new(ReadString(input, "txid"), checked((int)ReadInt64(input, "vout")), input.TryGetProperty("coinbase", out _)));
        List<WalletTransactionOutput> outputs = [];
        Dictionary<string, bool> ownership = new(StringComparer.Ordinal);
        foreach (JsonElement output in decoded.GetProperty("vout").EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSameWallet(wallet);
            string address = ReadString(output.GetProperty("scriptPubKey"), "address");
            bool own = false;
            if (address.Length > 0 && !ownership.TryGetValue(address, out own))
            {
                JsonElement info = await _rpc.CallAsync(endpoint, wallet, "getaddressinfo", new object[] { address }, TimeSpan.FromSeconds(15)).WaitAsync(cancellationToken);
                ownership[address] = own = ReadBoolean(info, "ismine");
            }
            outputs.Add(new(checked((int)ReadInt64(output, "n")), address, ReadDecimal(output, "value"), own));
        }
        WalletTransfer[] entries = transaction.GetProperty("details").EnumerateArray().Select(item => ParseTransfer(item, transaction)).ToArray();
        EnsureSameWallet(wallet);
        return new(transactionHash, ReadInt64(transaction, "confirmations"), SafeTimestamp(ReadInt64(transaction, "time")), Math.Abs(ReadDecimal(transaction, "fee")),
            ReadString(transaction, "blockhash"), entries.Any(item => item.Abandoned), inputs, outputs, entries);
    }

    private static void ValidateTransactionHash(string hash)
    {
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character))) throw new ArgumentException("A transaction ID must contain 64 hexadecimal characters.", nameof(hash));
    }
}

internal static class WalletHistoryCsv
{
    public const string Header = "Time (UTC),Transaction ID,Output index,Direction,Wallet entry address,Amount (SLTHY),Transaction fee (SLTHY; may repeat per entry),Confirmations,Category,Abandoned\r\n";

    public static string Export(IEnumerable<WalletTransfer> transfers) => Header + string.Concat(transfers.Select(Row));

    internal static string Row(WalletTransfer transfer) => string.Join(',', new[]
    {
        transfer.Timestamp?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) ?? "", transfer.TransactionHash, transfer.OutputIndex.ToString(CultureInfo.InvariantCulture), transfer.Direction, transfer.Address,
        transfer.Amount.ToString("0.########", CultureInfo.InvariantCulture), transfer.Fee.ToString("0.########", CultureInfo.InvariantCulture),
        transfer.Confirmations.ToString(CultureInfo.InvariantCulture), transfer.Category, transfer.Abandoned ? "yes" : "no"
    }.Select(Escape)) + "\r\n";

    internal static string Escape(string value)
    {
        // Quoting alone does not stop spreadsheets from evaluating a formula.
        string trimmed = value.TrimStart();
        if (value.Any(character => character is '\r' or '\n' or '\t') || (trimmed.Length > 0 && trimmed[0] is '=' or '+' or '-' or '@')) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
