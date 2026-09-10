//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Send Receive Test
//===============================================
using System.Diagnostics;

namespace Slithy_Tove;

// Self-test for creating wallets, mining spendable coins, and sending between wallets.
internal static class SendReceiveSelfTest
{
    public static async Task<bool> RunAsync()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slithy-send-receive-test-{Guid.NewGuid():N}");
        Process? process = null;
        string rpcPassword = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        Uri endpoint = new($"http://slithy:{rpcPassword}@127.0.0.1:42633");
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
                    "-rpcport=42633",
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

            using WalletRpcClient sender = new();
            using WalletRpcClient receiver = new();
            await sender.CreateWalletAsync(endpoint, "sender", "sender-password");
            await receiver.CreateWalletAsync(endpoint, "receiver", "receiver-password");

            WalletSnapshot senderSnapshot = await sender.GetSnapshotAsync(endpoint);
            WalletSnapshot receiverSnapshot = await receiver.GetSnapshotAsync(endpoint);

            await node.GenerateBlocksAsync(endpoint, senderSnapshot.Address, 101);
            senderSnapshot = await sender.GetSnapshotAsync(endpoint);
            if (senderSnapshot.UnlockedBalance < 9m)
            {
                return false;
            }

            PreparedTransfer prepared = await sender.PrepareTransferAsync(
                endpoint,
                receiverSnapshot.Address,
                100_000_000);
            TransferResult sent = await sender.RelayTransferAsync(endpoint, prepared);
            if (string.IsNullOrWhiteSpace(sent.TransactionHash) || prepared.FeeAtomic == 0 || sent.FeeAtomic != prepared.FeeAtomic)
            {
                return false;
            }

            await node.GenerateBlocksAsync(endpoint, senderSnapshot.Address, 1);
            receiverSnapshot = await receiver.GetSnapshotAsync(endpoint);

            // Back up a loaded, encrypted SQLite wallet after real transactions.
            // Reopen the snapshot and compare its spendable balance.
            string backupDirectory = Path.Combine(testRoot, "backup-wallet");
            Directory.CreateDirectory(backupDirectory);
            await receiver.BackupWalletAsync(endpoint, Path.Combine(backupDirectory, "wallet.dat"));
            await receiver.LockWalletOnlyAsync(endpoint);
            await receiver.CloseWalletAsync(endpoint);
            await receiver.OpenWalletAsync(endpoint, backupDirectory, "receiver-password");
            WalletSnapshot restoredBackup = await receiver.GetSnapshotAsync(endpoint);
            if (restoredBackup.UnlockedBalance != receiverSnapshot.UnlockedBalance) return false;

            await sender.CloseWalletAsync(endpoint);
            await receiver.CloseWalletAsync(endpoint);
            await node.StopDaemonAsync(endpoint);

            return receiverSnapshot.UnlockedBalance >= 1m;
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

