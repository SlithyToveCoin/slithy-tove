//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Mining RPC Test
//===============================================
using System.Diagnostics;

namespace Slithy_Tove;

// Self-test for mining RPC calls against a temporary local regtest node.
internal static class MiningRpcSelfTest
{
    public static async Task<bool> RunAsync()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slithy-mining-client-test-{Guid.NewGuid():N}");
        Process? process = null;
        string rpcPassword = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        Uri endpoint = new($"http://slithy:{rpcPassword}@127.0.0.1:42631");
        try
        {
            Directory.CreateDirectory(testRoot);
            string executable = Path.Combine(AppContext.BaseDirectory, "Binaries", "slithyd.exe");
            if (!File.Exists(executable))
            {
                return false;
            }

            process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = string.Join(' ',
                    "-regtest",
                    $"-datadir=\"{testRoot}\"",
                    "-server=1",
                    "-listen=0",
                    "-connect=0",
                    "-rpcbind=127.0.0.1",
                    "-rpcallowip=127.0.0.1",
                    "-rpcport=42631",
                    "-rpcuser=slithy",
                    $"-rpcpassword={rpcPassword}",
                    "-fallbackfee=0.0001"),
                CreateNoWindow = true,
                UseShellExecute = false
            });
            if (process is null)
            {
                return false;
            }

            using NodeRpcClient node = new();
            for (int attempt = 0; attempt < 40; attempt++)
            {
                if (process.HasExited) throw new InvalidOperationException("The disposable test node exited before the check finished.");
                try
                {
                    await node.GetInfoAsync(endpoint);
                    break;
                }
                catch
                {
                    await Task.Delay(250);
                }
            }

            using WalletRpcClient wallet = new();
            await wallet.CreateWalletAsync(endpoint, "mining-test", "mining-test-password");
            WalletSnapshot snapshot = await wallet.GetSnapshotAsync(endpoint);
            await node.StartMiningAsync(endpoint, snapshot.Address, 1);
            WalletSnapshot mined = snapshot;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                await Task.Delay(250);
                mined = await wallet.GetSnapshotAsync(endpoint);
                if (mined.Balance >= 9.00000000m)
                {
                    break;
                }
            }
            await node.StopMiningAsync(endpoint);
            IReadOnlyList<WalletTransfer> transfers = await wallet.GetTransfersAsync(endpoint);
            await node.StopDaemonAsync(endpoint);
            return mined.Balance >= 9.00000000m && transfers.Count > 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Disposable node test failed: {ex.Message}");
            return false;
        }
        finally
        {
            if (process is { HasExited: false })
            {
                try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (TimeoutException)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
            }
            process?.Dispose();
            TryDeleteTemp(testRoot);
        }
    }

    private static void TryDeleteTemp(string path)
    {
        string resolved = Path.GetFullPath(path);
        string allowed = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if ((resolved + Path.DirectorySeparatorChar).StartsWith(
                allowed, StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(resolved))
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try { Directory.Delete(resolved, recursive: true); return; }
                catch (IOException) when (attempt < 9) { Thread.Sleep(200); }
                catch (UnauthorizedAccessException) when (attempt < 9) { Thread.Sleep(200); }
            }
        }
    }
}

