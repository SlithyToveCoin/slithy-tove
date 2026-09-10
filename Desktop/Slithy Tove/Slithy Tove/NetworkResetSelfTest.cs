using System.Text.Json;

namespace Slithy_Tove;

internal static class NetworkResetSelfTest
{
    public static bool Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "slithy-network-reset-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "config"));
            AppSettings old = new()
            {
                NetworkResetId = "old-beta", LastWalletName = "same-name",
                PendingSendHex = "old-test-payment", MineToCustomAddress = true,
                CustomMiningAddress = "old-test-address"
            };
            old.WalletBalanceCache["same-name"] = new() { BalanceAtomic = 85500000000 };
            string oldSettings = Path.Combine(root, "config", "settings.json");
            File.WriteAllText(oldSettings, JsonSerializer.Serialize(old));
            foreach (string legacy in new[] { "network", "beta-testnet", "alpha-testnet" })
            {
                Directory.CreateDirectory(Path.Combine(root, legacy, "wallets"));
                File.WriteAllText(Path.Combine(root, legacy, "wallets", "same-name"), "old-wallet-fixture");
            }
            Dictionary<string, byte[]> before = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.ReadAllBytes);
            AppSettingsStore store = new(root);
            AppSettings fresh = store.Load();
            if (fresh.LastWalletName != "" || fresh.PendingSendHex != "" ||
                fresh.WalletBalanceCache.Count != 0 || fresh.MineToCustomAddress ||
                fresh.CustomMiningAddress != "") return false;
            if (!store.NetworkDataDirectory.EndsWith(AppSettings.CurrentNetworkResetId, StringComparison.Ordinal)) return false;
            fresh.LastWalletName = "new-beta-wallet";
            store.Save(fresh);
            if (store.Load().LastWalletName != fresh.LastWalletName) return false;
            if (before.Any(file => !File.ReadAllBytes(file.Key).SequenceEqual(file.Value))) return false;
            Console.WriteLine("Beta reset passed: old files unchanged, fresh balances and payments, repeat startup retains the new wallet.");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Beta reset test failed: " + ex.Message);
            return false;
        }
        finally
        {
            // This exact directory was created above for disposable fixtures.
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
