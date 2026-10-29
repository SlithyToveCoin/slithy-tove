using System.Security.Cryptography;
using Slithy_Tove;

namespace Slithy.Linux;

// Runs against a fresh, disconnected regtest node. It cannot spend beta coins.
internal static class IntegrationSelfTest
{
    public static async Task<int> RunAsync()
    {
        string root = Path.Combine(Environment.GetEnvironmentVariable("SLITHY_TEST_OUTPUT") ?? Path.GetTempPath(), "slithy-gui-check-" + Guid.NewGuid().ToString("N"));
        string phase = "node startup";
        LinuxSettingsStore store = new(root);
        LinuxSettings settings = new();
        await using LinuxNodeHost host = new(store, settings, regtest: true);
        using NodeRpcClient rpc = new();
        using WalletRpcClient first = new(), second = new();
        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        try
        {
            await host.StartAsync();
            Uri endpoint = await host.GetEndpointAsync();
            phase = "chain check";
            var chain = await rpc.CallAsync(endpoint, "", "getblockchaininfo", Array.Empty<object>());
            Check(chain.GetProperty("chain").GetString() == "regtest", "disconnected regtest node");
            Check((await rpc.CallAsync(endpoint, "", "getconnectioncount", Array.Empty<object>())).GetInt32() == 0, "no public peer connections");
            phase = "second-instance refusal";
            await using (LinuxNodeHost duplicate = new(store, settings, regtest: true))
            {
                bool refused = false;
                try { await duplicate.StartAsync(); }
                catch (InvalidOperationException) { refused = true; }
                Check(refused && host.IsRunning && !duplicate.IsRunning, "second GUI cannot take over the running node");
            }
            phase = "wallet creation";
            await first.CreateSeedWalletAsync(endpoint, "mining-test", password);
            WalletSnapshot initial = await first.GetSnapshotAsync(endpoint);
            phase = "regtest rewards";
            // Creating the maturity fixture can exceed a normal RPC timeout on a busy CPU.
            await rpc.CallAsync(endpoint, "", "generatetoaddress", new object[] { 102, initial.Address, 50_000_000 }, TimeSpan.FromMinutes(3));
            WalletSnapshot funded = await first.GetSnapshotAsync(endpoint);
            Check(funded.Balance > 0 && funded.UnlockedBalance > 0, "mature and immature rewards");
            await first.LockWalletOnlyAsync(endpoint);
            Check((await first.GetSnapshotAsync(endpoint)).Balance == funded.Balance, "locked wallet still reports its balance");
            bool rejected = false;
            try { await first.UnlockWalletAsync(endpoint, "invalid-" + password, TimeSpan.FromMinutes(1)); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && await first.NeedsSpendingPasswordAsync(endpoint), "wrong password does not unlock spending");
            await first.UnlockWalletAsync(endpoint, password, TimeSpan.FromMinutes(1));
            phase = "recipient creation and payment";
            string words = await second.CreateSeedWalletAsync(endpoint, "receiver-test", password);
            WalletSnapshot receiver = await second.GetSnapshotAsync(endpoint);
            PreparedTransfer prepared = await first.PrepareTransferAsync(endpoint, receiver.Address, 100_000_000);
            TransferResult sent = await first.RelayTransferAsync(endpoint, prepared);
            Check(sent.TransactionHash == prepared.TransactionHash, "confirmed send broadcasts the prepared transaction");
            await rpc.GenerateBlocksAsync(endpoint, initial.Address, 1);
            Check((await second.GetSnapshotAsync(endpoint)).Balance == 1, "recipient sees one SLTHY");
            phase = "backup and recovery";
            string backup = Path.Combine(host.DataDirectory, "receiver-backup.dat");
            await second.BackupWalletAsync(endpoint, backup);
            Check(File.Exists(backup), "wallet backup");
            await second.CloseWalletAsync(endpoint);
            await second.RestoreWalletAsync(endpoint, "restored-test", password, words, 0);
            var restoredAddress = await rpc.CallAsync(endpoint, "restored-test", "getaddressinfo", new object[] { receiver.Address });
            Check(restoredAddress.GetProperty("ismine").GetBoolean(), "recovery words restore control of the original address");
            Check((await second.GetSnapshotAsync(endpoint)).Balance == 1, "recovery finds the payment");
            await second.CloseWalletAsync(endpoint);
            await rpc.CallAsync(endpoint, "", "restorewallet", new object[] { "file-restored-test", backup, false });
            await second.OpenWalletAsync(endpoint, "file-restored-test", password);
            var fileAddress = await rpc.CallAsync(endpoint, "file-restored-test", "getaddressinfo", new object[] { receiver.Address });
            Check(fileAddress.GetProperty("ismine").GetBoolean(), "wallet-file restore retains control of the original address");
            Check((await second.GetSnapshotAsync(endpoint)).Balance == 1, "wallet-file restore retains the payment");
            await first.LockWalletOnlyAsync(endpoint);
            await second.LockWalletOnlyAsync(endpoint);
            phase = "locked-wallet mining controls";
            for (int cycle = 1; cycle <= 2; cycle++)
            {
                await rpc.StartMiningAsync(endpoint, initial.Address, 1);
                Check((await rpc.GetMiningInfoAsync(endpoint)).Active, "locked wallet can start mining, cycle " + cycle);
                await rpc.StopMiningAsync(endpoint);
                Check(!(await rpc.GetMiningInfoAsync(endpoint)).Active, "locked wallet can stop mining, cycle " + cycle);
            }
            Check(await first.NeedsSpendingPasswordAsync(endpoint), "mining never unlocks spending keys");
            phase = "node restart and saved balances";
            decimal savedBalance = (await first.GetSnapshotAsync(endpoint)).Balance;
            await host.StopOwnedAsync();
            Check(!host.IsRunning, "owned node stops cleanly");
            await host.StartAsync();
            Uri restartedEndpoint = await host.GetEndpointAsync();
            Check(endpoint.UserInfo != restartedEndpoint.UserInfo, "restart creates a fresh RPC authentication cookie");
            endpoint = restartedEndpoint;
            await first.OpenWalletAsync(endpoint, "mining-test", password);
            await second.OpenWalletAsync(endpoint, "file-restored-test", password);
            Check((await first.GetSnapshotAsync(endpoint)).Balance == savedBalance, "mining-wallet balance survives node restart");
            Check((await second.GetSnapshotAsync(endpoint)).Balance == 1, "restored-wallet balance survives node restart");
            await first.LockWalletOnlyAsync(endpoint);
            await second.LockWalletOnlyAsync(endpoint);
            Console.WriteLine("PASS: local Linux wallet integration completed. No public-chain transactions.");
            return 0;
        }
        catch (Exception e)
        {
            // No RPC URL or request body is written here: they may contain a cookie or recovery material.
            Console.Error.WriteLine("FAIL: Linux integration check during " + phase + ": " + e.GetType().Name);
            if (e is TestAssertionException) Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine("Test data retained at " + root);
            return 1;
        }
        finally { await host.StopOwnedAsync(); }
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new TestAssertionException(label);
        Console.WriteLine("PASS: " + label);
    }

    private sealed class TestAssertionException(string label) : Exception(label);
}
