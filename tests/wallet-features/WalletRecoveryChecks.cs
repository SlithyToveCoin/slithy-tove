using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Slithy_Tove;

internal static class WalletRecoveryChecks
{
    public static async Task<int> RunAsync()
    {
        int passed = 0;
        foreach (string mode in new[] { "failure", "cancel", "lock-failure" })
        {
            using CancellationTokenSource cancel = new();
            using WalletRpcClient client = new();
            using RecoveryRpc replies = new(mode, cancel);
            NodeRpcClient transport = (NodeRpcClient)typeof(WalletRpcClient).GetField("_rpc", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
            typeof(NodeRpcClient).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(transport, new HttpClient(replies));
            Exception? failure = null;
            try
            {
                await client.RestoreWalletAsync(new("http://fixture:unused@127.0.0.1:1"), "fixture", "unused-fixture-password",
                    "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about", 0, cancellationToken: cancel.Token);
            }
            catch (Exception exception) { failure = exception; }
            bool expected = mode switch
            {
                "failure" => failure is InvalidOperationException && failure.Message.Contains("Spending is locked") && replies.UnlockedUntil == 0,
                "cancel" => failure is OperationCanceledException && failure.Message.Contains("Spending is locked") && replies.Aborts == 1 && replies.UnlockedUntil == 0,
                _ => failure is AggregateException && failure.Message.Contains("locking could not be confirmed")
            };
            if (!expected || replies.Locks != 1) throw new InvalidOperationException("Recovery " + mode + " did not handle its spending lock.");
            Console.WriteLine("PASS recovery " + mode + " reports and verifies its spending-lock outcome");
            passed++;
        }
        return passed;
    }

    private sealed class RecoveryRpc(string mode, CancellationTokenSource cancel) : HttpMessageHandler
    {
        private readonly TaskCompletionSource<bool> _abort = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Locks { get; private set; }
        public int Aborts { get; private set; }
        public long UnlockedUntil { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            string method = body.RootElement.GetProperty("method").GetString()!;
            object result;
            switch (method)
            {
                case "getblockchaininfo": result = new { chain = "regtest" }; break;
                case "getdescriptorinfo": result = new { checksum = "fixture" }; break;
                case "createwallet": result = new { name = "fixture" }; break;
                case "walletpassphrase": UnlockedUntil = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600; result = new { }; break;
                case "importdescriptors":
                    if (mode == "cancel") { cancel.Cancel(); await _abort.Task.WaitAsync(cancellationToken); }
                    result = new[] { new { success = false, error = new { message = "Fixture import failed" } } };
                    break;
                case "abortrescan": Aborts++; _abort.TrySetResult(true); result = true; break;
                case "walletlock":
                    Locks++;
                    if (mode == "lock-failure") throw new HttpRequestException("Fixture lock is unavailable");
                    UnlockedUntil = 0; result = new { }; break;
                case "getwalletinfo": result = new { unlocked_until = UnlockedUntil }; break;
                default: throw new Exception("Unexpected recovery fixture call: " + method);
            }
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { result, error = (object?)null, id = 1 }), Encoding.UTF8, "application/json") };
        }
    }
}
