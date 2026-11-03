using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Slithy_Tove;

internal static class WalletPaymentChecks
{
    private static readonly Uri Endpoint = new("http://fixture:unused@127.0.0.1:1");
    private static string Hash(int value) => value.ToString("x64");

    public static async Task<int> RunAsync()
    {
        int passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            passed++;
            Console.WriteLine("PASS " + name);
        }
        async Task Probe(string mode, bool selected = false)
        {
            using WalletRpcClient client = new();
            using PaymentRpc replies = new(client, mode);
            typeof(WalletRpcClient).GetField("_walletName", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, "fixture");
            NodeRpcClient transport = (NodeRpcClient)typeof(WalletRpcClient).GetField("_rpc", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
            typeof(NodeRpcClient).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(transport, new HttpClient(replies));
            bool rejected = false;
            try
            {
                WalletOutpoint[] inputs = mode == "duplicate" ? [new(Hash(1), 0), new(Hash(1), 0)] :
                    mode == "spent" ? [new(Hash(99), 0)] : [new(Hash(1), 0)];
                PreparedTransfer result = selected ? await client.PrepareTransferAsync(Endpoint, "recipient", 100_000_000, inputs) :
                    await client.PrepareTransferAsync(Endpoint, "recipient", 100_000_000);
                if (mode == "good") Check(result.AmountAtomic == 100_000_000 && result.FeeAtomic == 10_000_000 && result.InputCount == 1 && result.TransactionMetadata == "signed", "ordinary preview matches signed amount, fee and input count");
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException) { rejected = true; }
            if (mode != "good") Check(rejected, "payment rejects " + mode);
            Check(replies.Broadcasts == 0, "preparing " + mode + " does not broadcast");
            if (mode == "switch") Check(replies.Signatures == 0, "wallet switch after funding prevents signing");
        }
        await Probe("good");
        foreach (string mode in new[] { "recipient-script", "recipient-amount", "foreign-change", "fee-mismatch", "duplicate-decoded", "switch" }) await Probe(mode);
        foreach (string mode in new[] { "added-input", "duplicate", "spent" }) await Probe(mode, true);
        return passed;
    }

    private sealed class PaymentRpc(WalletRpcClient client, string mode) : HttpMessageHandler
    {
        public int Broadcasts { get; private set; }
        public int Signatures { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            string method = body.RootElement.GetProperty("method").GetString()!;
            JsonElement args = body.RootElement.GetProperty("params");
            if (method == "sendrawtransaction") Broadcasts++;
            if (method == "signrawtransactionwithwallet") Signatures++;
            if (method == "fundrawtransaction")
            {
                if (mode == "added-input" && (!args[1].TryGetProperty("add_inputs", out JsonElement add) || add.GetBoolean()))
                    throw new Exception("Coin control did not forbid extra inputs.");
                if (mode == "switch") typeof(WalletRpcClient).GetField("_walletName", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, "another-wallet");
            }
            object result = method switch
            {
                "validateaddress" => new { isvalid = true, scriptPubKey = "recipient-script" },
                "listdescriptors" => new { descriptors = Array.Empty<object>() },
                "createrawtransaction" => "raw",
                "fundrawtransaction" => new { hex = "funded", fee = mode == "fee-mismatch" ? 0.2m : 0.1m },
                "signrawtransactionwithwallet" => new { hex = "signed", complete = true },
                "getaddressinfo" => new { ismine = mode != "foreign-change" },
                "listunspent" => new[] { new { txid = Hash(1), vout = 0, amount = 2m, confirmations = 2, spendable = true, safe = true, address = "own" } },
                "decoderawtransaction" => new { txid = Hash(8),
                    vin = mode is "added-input" or "duplicate-decoded" ? new[] { new { txid = Hash(1), vout = 0 }, new { txid = Hash(mode == "added-input" ? 2 : 1), vout = 0 } } : new[] { new { txid = Hash(1), vout = 0 } },
                    vout = new[] { new { value = mode == "recipient-amount" ? 0.9m : 1m, scriptPubKey = new { hex = mode == "recipient-script" ? "wrong-script" : "recipient-script", address = "recipient" } },
                        new { value = 0.9m, scriptPubKey = new { hex = "change-script", address = "change" } } } },
                _ => throw new Exception("Unexpected payment fixture call: " + method)
            };
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { result, error = (object?)null, id = 1 }), Encoding.UTF8, "application/json") };
        }
    }
}
