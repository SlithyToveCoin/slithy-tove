using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Slithy_Tove;

internal static class WalletMockChecks
{
    public static async Task<int> RunAsync()
    {
        int passed = 0;
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            passed++;
            Console.WriteLine("PASS " + label);
        }
        using WalletRpcClient client = new();
        using HistoryRpc replies = new();
        typeof(WalletRpcClient).GetField("_walletName", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, "fixture");
        NodeRpcClient transport = (NodeRpcClient)typeof(WalletRpcClient).GetField("_rpc", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
        typeof(NodeRpcClient).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(transport, new HttpClient(replies));
        Uri endpoint = new("http://fixture:unused@127.0.0.1:1");
        WalletHistoryPage result = await client.GetHistoryPageAsync(endpoint, search: "old-address");
        Check(result.Entries.Count == 1 && result.Entries[0].OutputIndex == 0 && replies.Batches > 4, "search reaches an entry beyond four RPC windows");
        replies.Overlap = true;
        string csv = await client.ExportHistoryCsvAsync(endpoint);
        Check(csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length == 2506, "duplicate entries between RPC windows are exported once");
        replies.Overlap = false;
        replies.Batches = 0;
        replies.InsertBetweenBatches = true;
        bool changed = false;
        try { await client.ExportHistoryCsvAsync(endpoint); }
        catch (InvalidOperationException exception) { changed = exception.Message.Contains("history changed"); }
        Check(changed, "insertion between RPC windows rejects a mixed history export");
        replies.InsertBetweenBatches = false;
        replies.Changed = false;
        replies.DelayHistory = true;
        using CancellationTokenSource stop = new(TimeSpan.FromMilliseconds(50));
        bool cancelled = false;
        try { await client.GetHistoryPageAsync(endpoint, cancellationToken: stop.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "history cancellation returns during an in-flight read");
        replies.DelayHistory = false;
        replies.NextIndex = 10000;
        Check((await client.CreateReceivingAddressAsync(endpoint)).Address == "fixture-new-address", "receiving index10000 is within the recovery window");
        replies.NextIndex = 10001;
        bool guarded = false;
        try { await client.CreateReceivingAddressAsync(endpoint); }
        catch (InvalidOperationException exception) { guarded = exception.Message.Contains("recovery window"); }
        Check(guarded && replies.Generated == 1, "receiving index10001 is rejected before key generation");
        bool changeGuarded = false;
        try { await client.PrepareTransferAsync(endpoint, "destination", 1); }
        catch (InvalidOperationException exception) { changeGuarded = exception.Message.Contains("does not belong"); }
        Check(changeGuarded, "change index10001 cannot fall back to an unowned address");
        replies.OwnsFallback = true;
        typeof(WalletRpcClient).GetField("_currentAddress", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, "");
        WalletSnapshot snapshot = await client.GetSnapshotAsync(endpoint);
        Check(snapshot.Balance == 7 && snapshot.Address == "fixture-index-zero" && replies.Generated == 1,
            "deep wallet with an empty address book displays balance without generating an address");
        replies.NextIndex = 0;
        typeof(WalletRpcClient).GetField("_currentAddress", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, "");
        WalletSnapshot freshSnapshot = await client.GetSnapshotAsync(endpoint);
        WalletReceivingAddress nextAddress = await client.CreateReceivingAddressAsync(endpoint);
        Check(freshSnapshot.Address != nextAddress.Address && replies.Generated == 3,
            "fresh wallet snapshot reserves its address before the next address is requested");
        return passed;
    }

    private sealed class HistoryRpc : HttpMessageHandler
    {
        public int Batches { get; set; }
        public bool Overlap { get; set; }
        public bool InsertBetweenBatches { get; set; }
        public bool Changed { get; set; }
        public bool DelayHistory { get; set; }
        public int NextIndex { get; set; }
        public int Generated { get; private set; }
        public bool OwnsFallback { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using JsonDocument json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            string method = json.RootElement.GetProperty("method").GetString()!;
            JsonElement parameters = json.RootElement.GetProperty("params");
            object result;
            if (method == "listtransactions")
            {
                if (DelayHistory) await Task.Delay(1000, cancellationToken);
                Batches++;
                if (InsertBetweenBatches && Batches == 2) Changed = true;
                int offset = parameters[2].GetInt32();
                List<object> entries = Enumerable.Range(0, 2505).Reverse().Skip(offset).Take(500).Reverse()
                    .Select(index => (object)new { txid = index.ToString("x64"), vout = index, category = "receive", amount = 1m,
                        address = index == 0 ? "old-address" : "ordinary-address", confirmations = 8, blockheight = 1, time = 100 + index }).ToList();
                // Repeating a boundary entry simulates offset movement without hiding older entries.
                if (Overlap && offset > 0 && entries.Count > 0) entries.Add(new { txid = (2505 - offset).ToString("x64"), vout = 2505 - offset,
                    category = "receive", amount = 1m, address = "ordinary-address", confirmations = 8, blockheight = 1, time = 100 + 2505 - offset });
                result = entries;
            }
            else if (method == "getwalletinfo") result = new { txcount = Changed ? 2506 : 2505, lastprocessedblock = new { hash = "stable-chain-tip" } };
            else if (method == "listdescriptors") result = new { descriptors = new[]
            {
                new { active = true, @internal = false, desc = "wpkh(public/0/*)", next_index = NextIndex },
                new { active = true, @internal = true, desc = "wpkh(public/1/*)", next_index = NextIndex }
            } };
            else if (method == "getnewaddress") { Generated++; result = Generated == 1 ? "fixture-new-address" : "fixture-new-address-" + Generated; }
            else if (method == "validateaddress") result = new { isvalid = true, scriptPubKey = "fixture-script" };
            else if (method == "deriveaddresses") result = new[] { "fixture-index-zero" };
            else if (method == "getaddressinfo") result = new { ismine = OwnsFallback };
            else if (method == "listreceivedbyaddress" || method == "listlabels") result = Array.Empty<object>();
            else if (method == "getbalances") result = new { mine = new { trusted = 7m, untrusted_pending = 0m, immature = 0m } };
            else throw new InvalidOperationException("Unexpected fixture call: " + method);
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { result, error = (object?)null, id = 1 }), Encoding.UTF8, "application/json") };
        }
    }
}
