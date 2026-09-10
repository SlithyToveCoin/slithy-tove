//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Seed Wallet Test
//===============================================
using System.Diagnostics;

namespace Slithy_Tove;

// Self-test that proves recovery words rebuild the same wallet address.
internal static class SeedWalletSelfTest
{
    public static async Task<bool> RunAsync()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slithy-seed-wallet-test-{Guid.NewGuid():N}");
        Process? process = null;
        string rpcPassword = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        Uri endpoint = new($"http://slithy:{rpcPassword}@127.0.0.1:42634");
        try
        {
            Directory.CreateDirectory(testRoot);
            string executable = Path.Combine(AppContext.BaseDirectory, "Binaries", "slithyd.exe");
            if (!File.Exists(executable))
            {
                Console.WriteLine("slithyd.exe was not found beside the app.");
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
                    "-rpcport=42634",
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

            using WalletRpcClient client = new();
            bool ready = false;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                if (process.HasExited) throw new InvalidOperationException("The disposable test node exited before the check finished.");
                if (await client.IsReadyAsync(endpoint))
                {
                    ready = true;
                    break;
                }
                await Task.Delay(250);
            }
            if (!ready)
            {
                Console.WriteLine("Temporary Slithy node did not become ready.");
                return false;
            }

            string words = await client.CreateSeedWalletAsync(endpoint, "seed-original", "seed-password");
            WalletSnapshot original = await client.GetSnapshotAsync(endpoint);
            await client.CloseWalletAsync(endpoint);

            await client.RestoreWalletAsync(endpoint, "seed-restored", "seed-password", words, 0);
            WalletSnapshot restored = await client.GetSnapshotAsync(endpoint);
            await client.CloseWalletAsync(endpoint);

            using NodeRpcClient node = new();
            await node.StopDaemonAsync(endpoint);

            bool passed = original.Address == restored.Address &&
                          original.Address.StartsWith("rslithy", StringComparison.Ordinal);
            Console.WriteLine(passed
                ? "Seed wallet restore passed."
                : $"Seed wallet restore failed: {original.Address} != {restored.Address}");
            return passed;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Seed wallet restore failed: {ex.Message}");
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
