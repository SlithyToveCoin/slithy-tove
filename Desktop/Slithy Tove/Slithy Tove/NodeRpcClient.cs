//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Node RPC Client
//===============================================
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Slithy_Tove;

// Plain data returned by the Slithy node when the app asks for chain status.
internal sealed record NodeInfo(
    long Height,
    decimal Difficulty,
    string NetworkType,
    bool Synchronized,
    string Version,
    long TransactionPoolSize,
    long Target);

internal sealed record ChainProgress(long Height, long Headers);

// Plain data returned by the node when the app asks if mining is running.
internal sealed record MiningInfo(
    bool Active,
    ulong Speed,
    int Threads,
    string Address,
    string Algorithm,
    long Blocks);

internal sealed record PeerNetworkStatus(
    int Connections,
    int PeersAtOrAboveHeight,
    long HighestKnownPeerHeight,
    bool HasKnownPeerHeight);

// This class talks to slithyd over JSON-RPC.
// The UI should call this instead of building raw HTTP requests itself.
internal sealed class NodeRpcClient : IDisposable
{
    internal const string BetaGenesisHash = "000f888cdb70403cd5310d02d7983951ee146ac799485bca337a7b52c24643f2";

    internal static bool CanBootstrapBeta(string chain, long height, long headers, string hash, PeerNetworkStatus peers) =>
        chain == "test" && height == 0 && headers == 0 && hash == BetaGenesisHash &&
        peers.Connections > 0 && peers.HasKnownPeerHeight && peers.HighestKnownPeerHeight == 0 &&
        peers.PeersAtOrAboveHeight == peers.Connections;

    private readonly HttpClient _httpClient = new()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };
    private int _requestId;

    public async Task<NodeInfo> GetInfoAsync(Uri nodeUri, TimeSpan? timeout = null)
    {
        // The node reports chain, network, and mempool details through separate RPC calls.
        JsonElement blockchain = await CallAsync(nodeUri, "", "getblockchaininfo", Array.Empty<object>(), timeout);
        JsonElement network = await CallAsync(nodeUri, "", "getnetworkinfo", Array.Empty<object>(), timeout);
        JsonElement mempool = await CallAsync(nodeUri, "", "getmempoolinfo", Array.Empty<object>(), timeout);

        string chain = ReadString(blockchain, "chain");
        bool synchronized = !ReadBoolean(blockchain, "initialblockdownload") &&
                            ReadInt64(blockchain, "blocks") >= ReadInt64(blockchain, "headers");
        // An aged genesis can trigger Core's tip-age heuristic before the first block exists.
        // Bootstrap this specific beta when every connected peer also reports height zero.
        if (chain == "test" && ReadInt64(blockchain, "blocks") == 0 &&
            ReadInt64(blockchain, "headers") == 0 && ReadString(blockchain, "bestblockhash") == BetaGenesisHash)
        {
            PeerNetworkStatus peers = await GetPeerNetworkStatusAsync(nodeUri, 0);
            synchronized = CanBootstrapBeta(chain, 0, 0, ReadString(blockchain, "bestblockhash"), peers);
        }

        return new NodeInfo(
            ReadInt64(blockchain, "blocks"),
            Math.Max(0, ReadDecimal(blockchain, "difficulty")),
            chain,
            synchronized,
            ReadString(network, "subversion").Trim('/'),
            ReadInt64(mempool, "size"),
            120);
    }

    public Task StopDaemonAsync(Uri nodeUri) =>
        CallWithoutResultAsync(nodeUri, "", "stop", Array.Empty<object>());

    public async Task StartMiningAsync(Uri nodeUri, string address, int threads)
    {
        // This starts mining on the nodeUri machine. The desktop app should pass
        // the local 127.0.0.1 node here, not an official hosted node.
        await CallAsync(nodeUri, "", "startcpumining", new object[] { address, Math.Max(1, threads) });
    }

    public async Task GenerateBlocksAsync(Uri nodeUri, string address, int blocks)
    {
        await CallAsync(nodeUri, "", "generatetoaddress", new object[] { Math.Max(1, blocks), address, 50_000_000 });
    }

    public Task StopMiningAsync(Uri nodeUri) =>
        CallWithoutResultAsync(nodeUri, "", "stopcpumining", Array.Empty<object>());

    public async Task<ChainProgress> GetChainProgressAsync(Uri nodeUri, TimeSpan? timeout = null)
    {
        JsonElement blockchain = await CallAsync(nodeUri, "", "getblockchaininfo", Array.Empty<object>(), timeout);
        return new ChainProgress(
            ReadInt64(blockchain, "blocks"),
            ReadInt64(blockchain, "headers"));
    }

    public async Task<string> GetBlockHashAsync(Uri nodeUri, long height, TimeSpan? timeout = null)
    {
        JsonElement result = await CallAsync(
            nodeUri,
            "",
            "getblockhash",
            new object[] { height },
            timeout);
        return result.GetString() ?? "";
    }

    public async Task<bool> TryFetchHeaderKnownBlocksAsync(Uri nodeUri, int maxBlocks)
    {
        bool fetchedAny = false;
        for (int attempt = 0; attempt < Math.Max(1, maxBlocks); attempt++)
        {
            JsonElement chain = await CallAsync(nodeUri, "", "getblockchaininfo", Array.Empty<object>(), TimeSpan.FromSeconds(5));
            long blocks = ReadInt64(chain, "blocks");
            long headers = ReadInt64(chain, "headers");
            if (blocks >= headers)
            {
                return fetchedAny;
            }

            string nextHash = await FindHeaderHashAtHeightAsync(nodeUri, blocks + 1);
            if (string.IsNullOrWhiteSpace(nextHash))
            {
                return fetchedAny;
            }

            JsonElement peers = await CallAsync(nodeUri, "", "getpeerinfo", Array.Empty<object>(), TimeSpan.FromSeconds(5));
            bool requested = false;
            foreach (JsonElement peer in peers.EnumerateArray())
            {
                long peerId = ReadInt64(peer, "id");
                try
                {
                    await CallAsync(nodeUri, "", "getblockfrompeer", new object[] { nextHash, peerId }, TimeSpan.FromSeconds(5));
                    requested = true;
                    fetchedAny = true;
                    break;
                }
                catch
                {
                    // Try the next peer.
                }
            }

            if (!requested)
            {
                return fetchedAny;
            }

            await Task.Delay(350);
        }

        return fetchedAny;
    }

    public async Task<MiningInfo> GetMiningInfoAsync(Uri nodeUri)
    {
        JsonElement mining = await CallAsync(
            nodeUri,
            "",
            "getcpumininginfo",
            Array.Empty<object>(),
            TimeSpan.FromSeconds(5));

        return new MiningInfo(
            ReadBoolean(mining, "active"),
            (ulong)Math.Max(0, ReadInt64(mining, "speed")),
            (int)Math.Max(0, ReadInt64(mining, "threads")),
            ReadString(mining, "address"),
            ReadString(mining, "algorithm"),
            ReadInt64(mining, "blocks"));
    }

    public async Task<PeerNetworkStatus> GetPeerNetworkStatusAsync(Uri nodeUri, long localHeight)
    {
        JsonElement peers = await CallAsync(
            nodeUri,
            "",
            "getpeerinfo",
            Array.Empty<object>(),
            TimeSpan.FromSeconds(5));

        return ParsePeerNetworkStatus(peers, localHeight);
    }

    internal static string? MiningPeerProblem(PeerNetworkStatus peers, long localHeight)
    {
        if (peers.Connections == 0)
            return "No Slithy peers are connected. Check your internet connection and selected network.";
        if (!peers.HasKnownPeerHeight)
            return "The local node has not established a usable Slithy peer connection. Peer block heights are unknown. " +
                "Check that your app and the nodes use the same beta network. A reset candidate cannot mine with nodes still running the previous beta.";
        // These counters track blocks known through this connection. They can
        // stay behind when this computer mines and sends the new blocks.
        // GetInfoAsync checks our chain sync before this guard runs.
        if (peers.HighestKnownPeerHeight <= localHeight)
            return null;
        return $"Your node is at block {localHeight:N0}. A connected peer has announced block {peers.HighestKnownPeerHeight:N0}. " +
            "Mining is paused while your node catches up.";
    }

    internal static PeerNetworkStatus ParsePeerNetworkStatus(JsonElement peers, long localHeight)
    {
        int connections = 0;
        int peersAtHeight = 0;
        long highestPeerHeight = 0;
        bool hasKnownPeerHeight = false;
        foreach (JsonElement peer in peers.EnumerateArray())
        {
            connections++;
            if (ReadInt64(peer, "version") <= 0) continue;
            long peerHeight = ReadKnownPeerHeight(peer);
            if (peer.TryGetProperty("synced_headers", out JsonElement headers) &&
                headers.TryGetInt64(out long headerHeight) && headerHeight >= 0)
                peerHeight = Math.Max(peerHeight, headerHeight);
            // Before the first block, Core may leave synced_blocks at -1.
            // A completed version handshake still reports the peer's starting height.
            if (localHeight == 0 && ReadInt64(peer, "version") > 0 &&
                peer.TryGetProperty("startingheight", out JsonElement starting) &&
                starting.TryGetInt64(out long startingHeight) && startingHeight >= 0)
            {
                peerHeight = Math.Max(peerHeight, startingHeight);
                peerHeight = Math.Max(peerHeight, ReadInt64(peer, "synced_headers"));
            }
            if (peerHeight < 0)
            {
                continue;
            }

            hasKnownPeerHeight = true;
            highestPeerHeight = Math.Max(highestPeerHeight, peerHeight);
            if (peerHeight >= localHeight)
            {
                peersAtHeight++;
            }
        }

        return new PeerNetworkStatus(connections, peersAtHeight, highestPeerHeight, hasKnownPeerHeight);
    }

    internal async Task<JsonElement> CallAsync(
        Uri nodeUri,
        string walletName,
        string method,
        object parameters,
        TimeSpan? timeout = null)
    {
        // All Slithy Core RPC commands use the same JSON-RPC envelope:
        // method name, parameters, and a request id.
        int id = Interlocked.Increment(ref _requestId);
        string requestJson = JsonSerializer.Serialize(new
        {
            jsonrpc = "1.0",
            id = id.ToString(),
            method,
            @params = parameters
        });

        using StringContent requestContent = new(requestJson, Encoding.UTF8, "application/json");
        using HttpRequestMessage request = new(HttpMethod.Post, BuildRpcUri(nodeUri, walletName))
        {
            Content = requestContent
        };
        request.Headers.Authorization = BuildAuthorization(nodeUri);

        using CancellationTokenSource cancellation = new(timeout ?? TimeSpan.FromSeconds(30));
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellation.Token);
        string responseJson = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            response.EnsureSuccessStatusCode();
        }

        using JsonDocument document = JsonDocument.Parse(responseJson);
        JsonElement root = document.RootElement;
        // JSON-RPC can return HTTP 200 and still include an RPC error object.
        if (root.TryGetProperty("error", out JsonElement error) &&
            error.ValueKind != JsonValueKind.Null)
        {
            string message = error.TryGetProperty("message", out JsonElement value)
                ? value.GetString() ?? "RPC operation failed."
                : "RPC operation failed.";
            throw new InvalidOperationException(message);
        }
        response.EnsureSuccessStatusCode();

        return root.GetProperty("result").Clone();
    }

    private async Task CallWithoutResultAsync(Uri nodeUri, string walletName, string method, object parameters)
        => _ = await CallAsync(nodeUri, walletName, method, parameters);

    private async Task<string> FindHeaderHashAtHeightAsync(Uri nodeUri, long height)
    {
        JsonElement tips = await CallAsync(nodeUri, "", "getchaintips", Array.Empty<object>(), TimeSpan.FromSeconds(5));
        string hash = "";
        long bestHeaderHeight = -1;
        foreach (JsonElement tip in tips.EnumerateArray())
        {
            string status = ReadString(tip, "status");
            long tipHeight = ReadInt64(tip, "height");
            if (status.Equals("headers-only", StringComparison.OrdinalIgnoreCase) &&
                tipHeight >= height &&
                tipHeight > bestHeaderHeight)
            {
                bestHeaderHeight = tipHeight;
                hash = ReadString(tip, "hash");
            }
        }

        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        int inspected = 0;
        while (!string.IsNullOrWhiteSpace(hash) && inspected++ < 100 && DateTime.UtcNow < deadline)
        {
            JsonElement header = await CallAsync(nodeUri, "", "getblockheader", new object[] { hash }, TimeSpan.FromSeconds(5));
            long headerHeight = ReadInt64(header, "height");
            if (headerHeight == height)
            {
                return ReadString(header, "hash");
            }

            if (headerHeight < height)
            {
                break;
            }

            hash = ReadString(header, "previousblockhash");
        }

        return "";
    }

    private static Uri BuildRpcUri(Uri nodeUri, string walletName)
    {
        // Bitcoin-style wallet RPC uses /wallet/name for wallet-specific calls.
        string path = string.IsNullOrWhiteSpace(walletName)
            ? "/"
            : $"/wallet/{Uri.EscapeDataString(walletName)}";
        return new Uri(nodeUri, path);
    }

    private static AuthenticationHeaderValue BuildAuthorization(Uri nodeUri)
    {
        // Credentials can be embedded in the URI, like http://user:password@host:port.
        string user = "";
        string password = "";
        if (!string.IsNullOrWhiteSpace(nodeUri.UserInfo))
        {
            string[] parts = nodeUri.UserInfo.Split(':', 2);
            user = Uri.UnescapeDataString(parts[0]);
            if (parts.Length > 1)
            {
                password = Uri.UnescapeDataString(parts[1]);
            }
        }

        string token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{user}:{password}"));
        return new AuthenticationHeaderValue("Basic", token);
    }

    private static string ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) ? value.GetString() ?? "Unknown" : "Unknown";

    private static long ReadInt64(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long result) ? result : 0;

    private static long ReadKnownPeerHeight(JsonElement root)
    {
        if (root.TryGetProperty("blocks", out JsonElement blocks) &&
            blocks.TryGetInt64(out long blockHeight) &&
            blockHeight >= 0)
        {
            return blockHeight;
        }

        if (root.TryGetProperty("synced_blocks", out JsonElement syncedBlocks) &&
            syncedBlocks.TryGetInt64(out long syncedHeight) &&
            syncedHeight >= 0)
        {
            return syncedHeight;
        }

        return -1;
    }

    private static decimal ReadDecimal(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetDecimal(out decimal result) ? result : 0;

    private static bool ReadBoolean(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;

    public void Dispose() => _httpClient.Dispose();
}

