//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//          Wallet Compatibility Tools
//===============================================
using System.Text.Json;

namespace Slithy_Tove;

// Move old test wallets out of the wallet list after adding recovery words.
internal static class WalletCompatibilityTools
{
    public const string SeedMarkerFileName = ".slithy-seed-wallet.json";

    public static void MarkSeedWallet(string walletRoot, string walletName)
    {
        string walletDirectory = Path.Combine(walletRoot, walletName);
        Directory.CreateDirectory(walletDirectory);
        string markerPath = Path.Combine(walletDirectory, SeedMarkerFileName);
        File.WriteAllText(markerPath, JsonSerializer.Serialize(new
        {
            format = "slithy-seed-wallet",
            version = 1,
            createdUtc = DateTimeOffset.UtcNow
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static bool IsSeedWallet(string walletDirectory) =>
        File.Exists(Path.Combine(walletDirectory, SeedMarkerFileName));

    public static IReadOnlyList<string> ArchivePreSeedWallets(string walletRoot, string appDataRoot)
    {
        if (!Directory.Exists(walletRoot))
        {
            return [];
        }

        string[] oldWallets = Directory.EnumerateDirectories(walletRoot)
            .Where(path => File.Exists(Path.Combine(path, "wallet.dat")) && !IsSeedWallet(path))
            .ToArray();
        if (oldWallets.Length == 0)
        {
            return [];
        }

        string archiveRoot = Path.Combine(
            appDataRoot,
            "archived-pre-seed-wallets",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(archiveRoot);

        List<string> archivedNames = [];
        foreach (string walletPath in oldWallets)
        {
            string walletName = Path.GetFileName(walletPath);
            if (string.IsNullOrWhiteSpace(walletName))
            {
                continue;
            }

            string destination = Path.Combine(archiveRoot, walletName);
            try
            {
                Directory.Move(walletPath, destination);
                archivedNames.Add(walletName);
            }
            catch
            {
                // A still-running node can hold a wallet folder briefly. Startup
                // should continue and try again next time.
            }
        }

        return archivedNames;
    }
}
