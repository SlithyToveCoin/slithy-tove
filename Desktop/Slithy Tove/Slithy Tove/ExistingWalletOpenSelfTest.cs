//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Existing Wallet Open Test
//===============================================
namespace Slithy_Tove;

internal static class ExistingWalletOpenSelfTest
{
    public static async Task<bool> RunAsync(string walletName, string password)
    {
        AppSettingsStore store = new();
        AppSettings settings = store.Load();
        LocalNodeManager localNode = new(message => Console.WriteLine(message));
        NodeRpcClient nodeClient = new();
        WalletRpcClient walletClient = new();
        Uri endpoint = BuildLocalNodeUri(settings);

        try
        {
            await localNode.StartAsync(settings);
            await WaitForNodeAsync(nodeClient, endpoint);
            await WaitForPeerHeadersAsync(nodeClient, endpoint);
            await nodeClient.TryFetchHeaderKnownBlocksAsync(endpoint, 250);

            NodeInfo nodeInfo = await nodeClient.GetInfoAsync(endpoint, TimeSpan.FromSeconds(5));
            await walletClient.OpenWalletAsync(endpoint, walletName, password, TimeSpan.FromMinutes(5));
            await walletClient.RefreshAsync(endpoint);
            WalletScanStatus scan = await walletClient.GetScanStatusAsync(endpoint);
            WalletSnapshot snapshot = await walletClient.GetSnapshotAsync(endpoint);

            Console.WriteLine($"wallet={walletName}");
            Console.WriteLine($"node_height={nodeInfo.Height}");
            Console.WriteLine($"wallet_scanning={scan.Scanning}");
            Console.WriteLine($"balance={snapshot.Balance:N8}");
            Console.WriteLine($"unlocked={snapshot.UnlockedBalance:N8}");
            Console.WriteLine($"address={snapshot.Address}");

            return !scan.Scanning;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"existing_wallet_open_test_failed={ex.Message}");
            return false;
        }
        finally
        {
            try
            {
                await walletClient.CloseWalletAsync(endpoint);
            }
            catch
            {
            }

            await localNode.StopAsync(endpoint);
            nodeClient.Dispose();
            walletClient.Dispose();
        }
    }

    private static async Task WaitForNodeAsync(NodeRpcClient nodeClient, Uri endpoint)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                await nodeClient.GetInfoAsync(endpoint, TimeSpan.FromSeconds(2));
                return;
            }
            catch
            {
                await Task.Delay(500);
            }
        }

        throw new TimeoutException("Local node did not answer RPC in time.");
    }

    private static async Task WaitForPeerHeadersAsync(NodeRpcClient nodeClient, Uri endpoint)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            ChainProgress progress = await nodeClient.GetChainProgressAsync(endpoint, TimeSpan.FromSeconds(3));
            if (progress.Headers > progress.Height)
            {
                return;
            }

            await Task.Delay(1000);
        }
    }

    private static Uri BuildLocalNodeUri(AppSettings settings)
    {
        string user = Uri.EscapeDataString(settings.RpcUser);
        string password = Uri.EscapeDataString(settings.RpcPassword);
        return new Uri($"http://{user}:{password}@127.0.0.1:{settings.LocalRpcPort}");
    }
}
