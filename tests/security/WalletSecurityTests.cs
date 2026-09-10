using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Slithy_Tove;
using Slithy.Cryptography;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("Passed: " + message);
}

// This published BIP39 test phrase has no funds. Never use it for a real wallet.
const string words = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
Check(RecoveryWords.Normalize(words.ToUpperInvariant()) == words, "recovery normalization");
bool rejected = false;
try { RecoveryWords.Normalize(string.Join(' ', Enumerable.Repeat("abandon", 12))); }
catch (InvalidOperationException) { rejected = true; }
Check(rejected, "invalid recovery checksum rejected");
Check(SlithySeedPhrase.BuildDescriptors(words, "test").SequenceEqual(RecoveryWords.Descriptors(words, "testnet")), "shared Windows and Linux test-network descriptors");
Check(SlithySeedPhrase.BuildDescriptors(words, "main").SequenceEqual(RecoveryWords.Descriptors(words, "mainnet")), "shared Windows and Linux live-network descriptors");
Check(RecoveryWords.Descriptors(words, "main")[0].StartsWith("wpkh(xprv") && RecoveryWords.Descriptors(words, "test")[0].StartsWith("wpkh(tprv"), "network-specific key encoding");
Check(TreasuryStatusCalculator.FromChainHeight(0).Accrued == 0, "genesis treasury is zero");
Check(TreasuryStatusCalculator.FromChainHeight(1).Accrued == 1, "first mined treasury reward");
Check(TreasuryStatusCalculator.FromChainHeight(1_051_200).Accrued == 1_051_199.5m, "treasury halving boundary");
PeerNetworkStatus genesisPeers = new(1, 1, 0, true);
using JsonDocument connecting = JsonDocument.Parse("[{\"version\":0,\"startingheight\":-1,\"synced_blocks\":-1}]");
PeerNetworkStatus unknownPeers = NodeRpcClient.ParsePeerNetworkStatus(connecting.RootElement, 0);
Check(!unknownPeers.HasKnownPeerHeight, "unfinished handshake has no known height");
string? unknownMessage = NodeRpcClient.MiningPeerProblem(unknownPeers, 0);
Check(unknownMessage is not null && unknownMessage.Contains("unknown") && !unknownMessage.Contains("at block 0"), "unknown peer is not described as block zero");
Check(NodeRpcClient.MiningPeerProblem(new(0, 0, 0, false), 0)!.Contains("No Slithy peers"), "offline message identifies missing peers");
Check(NodeRpcClient.MiningPeerProblem(genesisPeers, 0) is null, "known genesis peer permits mining");
using JsonDocument staleAfterMining = JsonDocument.Parse("""[{"version":70016,"startingheight":4,"synced_headers":4,"synced_blocks":4}]""");
PeerNetworkStatus staleCounters = NodeRpcClient.ParsePeerNetworkStatus(staleAfterMining.RootElement, 94);
Check(NodeRpcClient.MiningPeerProblem(staleCounters, 94) is null, "restart at block 94 allows stale peer counters at block 4");
Check(NodeRpcClient.MiningPeerProblem(staleCounters, 4) is null, "mining starts before this computer advances the chain");
Check(NodeRpcClient.MiningPeerProblem(staleCounters, 120) is null, "repeated mining restarts allow locally advanced blocks");
using JsonDocument aheadAfterMining = JsonDocument.Parse("""[{"version":70016,"startingheight":4,"synced_headers":95,"synced_blocks":4}]""");
Check(NodeRpcClient.MiningPeerProblem(NodeRpcClient.ParsePeerNetworkStatus(aheadAfterMining.RootElement, 94), 94) is not null, "newer peer headers still block mining until local sync");
Check(NodeRpcClient.MiningPeerProblem(new(0, 0, 0, false), 94) is not null, "disconnected miner cannot restart");
Check(NodeRpcClient.MiningPeerProblem(new(1, 0, 0, false), 94) is not null, "unknown peers cannot authorize restart");
using JsonDocument ahead = JsonDocument.Parse("[{\"version\":70016,\"startingheight\":0,\"synced_blocks\":-1,\"synced_headers\":4}]");
Check(NodeRpcClient.ParsePeerNetworkStatus(ahead.RootElement, 0).HighestKnownPeerHeight == 4, "peer headers prevent stale genesis bootstrap");
Check(NodeRpcClient.CanBootstrapBeta("test", 0, 0, NodeRpcClient.BetaGenesisHash, genesisPeers), "known beta genesis with a zero-height peer can bootstrap");
Check(!NodeRpcClient.CanBootstrapBeta("test", 0, 1, NodeRpcClient.BetaGenesisHash, genesisPeers), "pending headers prevent genesis bootstrap");
Check(!NodeRpcClient.CanBootstrapBeta("test", 0, 0, "old-genesis", genesisPeers), "old beta cannot bootstrap");
Check(!NodeRpcClient.CanBootstrapBeta("test", 0, 0, NodeRpcClient.BetaGenesisHash, new(0, 0, 0, false)), "offline genesis cannot bootstrap");
Check(!NodeRpcClient.CanBootstrapBeta("test", 0, 0, NodeRpcClient.BetaGenesisHash, new(1, 1, 4, true)), "higher peer chain prevents genesis bootstrap");
Check(!NodeRpcClient.CanBootstrapBeta("main", 0, 0, NodeRpcClient.BetaGenesisHash, genesisPeers), "live network never uses beta bootstrap");

using WalletRpcClient wallet = new();
typeof(WalletRpcClient).GetField("_walletName", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(wallet, "fixture");
NodeRpcClient transport = (NodeRpcClient)typeof(WalletRpcClient).GetField("_rpc", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wallet)!;
FixtureRpc replies = new();
HttpClient http = new(replies);
typeof(NodeRpcClient).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(transport, http);
Uri endpoint = new("http://fixture:dummy@127.0.0.1:1");
WalletSnapshot snapshot = await wallet.GetSnapshotAsync(endpoint);
Check(snapshot.Balance == 12 && snapshot.UnlockedBalance == 1, "untrusted receipts are not spendable");
Check(snapshot.BlocksToUnlock == -1, "unknown maturity is not a fixed countdown");
PreparedTransfer prepared = await wallet.PrepareTransferAsync(endpoint, "fixture-destination", 100_000_000);
Check(prepared.FeeAtomic == 1234 && prepared.TransactionMetadata == "fixture-signed", "approval contains the actual fee and signed bytes");
TransferResult sent = await wallet.RelayTransferAsync(endpoint, prepared);
Check(sent.TransactionHash == prepared.TransactionHash && replies.SendCalls == 1, "lost send response reconciled without a second broadcast");
replies.ReconciliationAvailable = false;
bool unknown = false;
try { await wallet.RelayTransferAsync(endpoint, prepared); }
catch (InvalidOperationException error) { unknown = error.Message.Contains("unknown") && error.Message.Contains("fixture-txid"); }
Check(unknown, "unconfirmed payment is reported as unknown");
Check(replies.PreparedCount == 1, "retry never constructs another payment");
Console.WriteLine("Wallet security regression tests passed. No network sockets or real wallets were used.");
// Optional read-only check against a running local node. Credentials stay in the environment.
string? readinessEndpoint = Environment.GetEnvironmentVariable("SLITHY_READINESS_RPC");
if (readinessEndpoint is not null)
{
    Uri local = new(readinessEndpoint);
    if (!local.IsLoopback) throw new InvalidOperationException("Readiness probe requires a local node.");
    using NodeRpcClient node = new();
    NodeInfo info = await node.GetInfoAsync(local, TimeSpan.FromSeconds(5));
    PeerNetworkStatus peers = await node.GetPeerNetworkStatusAsync(local, info.Height);
    Check(info.Synchronized && NodeRpcClient.MiningPeerProblem(peers, info.Height) is null,
        $"read-only local readiness: block {info.Height}, connection counter {peers.HighestKnownPeerHeight}");
}

sealed class FixtureRpc : HttpMessageHandler
{
    public int SendCalls { get; private set; }
    public int PreparedCount { get; private set; }
    public bool ReconciliationAvailable { get; set; } = true;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        string method = body.RootElement.GetProperty("method").GetString()!;
        JsonElement args = body.RootElement.GetProperty("params");
        if (method == "sendrawtransaction")
        {
            if (args[0].GetString() != "fixture-signed") throw new Exception("Signed payment changed");
            SendCalls++;
            throw new HttpRequestException("Fixture lost the response after accepting the transaction");
        }
        if (method == "gettransaction" && !ReconciliationAvailable) throw new HttpRequestException("Fixture is unavailable");
        if (method == "createrawtransaction") PreparedCount++;
        object result = method switch
        {
            "getnewaddress" => "fixture-address",
            "getbalances" => new { mine = new { trusted = 1, untrusted_pending = 2, immature = 9 } },
            "createrawtransaction" => "fixture-unsigned",
            "fundrawtransaction" => new { hex = "fixture-funded", fee = 0.00001234m },
            "signrawtransactionwithwallet" => new { hex = "fixture-signed", complete = true },
            "decoderawtransaction" or "gettransaction" => new { txid = "fixture-txid" },
            _ => throw new Exception("Unexpected fixture RPC: " + method)
        };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { result, error = (object?)null, id = 1 }), Encoding.UTF8, "application/json") };
    }
}
