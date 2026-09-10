//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Wallet RPC Transport Test
//===============================================
using System.Diagnostics;

namespace Slithy_Tove;

// Self-test that proves wallet RPC commands can travel through the app's RPC wrapper.
internal static class WalletRpcTransportSelfTest
{
    public static async Task<bool> RunAsync()
    {
        // Uses a temporary regtest node and wallet, then checks wallet creation and balance calls.
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slithy-wallet-client-test-{Guid.NewGuid():N}");
        Process? process = null;
        string rpcPassword = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        Uri endpoint = new($"http://slithy:{rpcPassword}@127.0.0.1:42629");
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
                    "-rpcport=42629",
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
                return false;
            }

            await client.CreateWalletAsync(endpoint, "transport-test", "transport-test-password");
            WalletSnapshot snapshot = await client.GetSnapshotAsync(endpoint);
            await client.LockWalletOnlyAsync(endpoint);
            await client.UnlockWalletAsync(endpoint, "transport-test-password", TimeSpan.FromMinutes(1));
            AddressValidation valid = await client.ValidateAddressAsync(endpoint, snapshot.Address);
            string invalidAddress = snapshot.Address[..^1] +
                                    (snapshot.Address[^1] == '1' ? '2' : '1');
            AddressValidation invalid = await client.ValidateAddressAsync(endpoint, invalidAddress);
            await client.CloseWalletAsync(endpoint);
            await client.CreateWalletAsync(endpoint, "upgrade-test", "");
            await client.CloseWalletAsync(endpoint);
            await client.OpenWalletAsync(endpoint, "upgrade-test", "upgrade-test-password");
            await client.LockWalletOnlyAsync(endpoint);
            await client.UnlockWalletAsync(endpoint, "upgrade-test-password", TimeSpan.FromMinutes(1));
            await client.CloseWalletAsync(endpoint);

            using NodeRpcClient node = new();
            await node.StopDaemonAsync(endpoint);
            return snapshot.Address.StartsWith("rslithy", StringComparison.Ordinal) &&
                   valid.Valid &&
                   !invalid.Valid;
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

