//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Wallet Backup Tools
//===============================================
namespace Slithy_Tove;

// Small file helpers for Slithy wallet folder backups.
internal static class WalletBackupTools
{
    public static bool TryFindBackupWalletDirectory(
        string selectedDirectory,
        out string walletDirectory,
        out string walletName)
    {
        walletDirectory = "";
        walletName = "";
        if (File.Exists(Path.Combine(selectedDirectory, "wallet.dat")))
        {
            if (!WalletCompatibilityTools.IsSeedWallet(selectedDirectory))
            {
                return false;
            }
            walletDirectory = selectedDirectory;
            walletName = Path.GetFileName(selectedDirectory);
            return !string.IsNullOrWhiteSpace(walletName);
        }

        string[] childWallets = Directory.EnumerateDirectories(selectedDirectory)
            .Where(path => File.Exists(Path.Combine(path, "wallet.dat")) &&
                           WalletCompatibilityTools.IsSeedWallet(path))
            .ToArray();
        if (childWallets.Length != 1)
        {
            return false;
        }

        walletDirectory = childWallets[0];
        walletName = Path.GetFileName(walletDirectory);
        return !string.IsNullOrWhiteSpace(walletName);
    }

    public static void RestoreBackup(
        string sourceWalletDirectory,
        string destinationDirectory,
        string walletName)
    {
        if (!File.Exists(Path.Combine(sourceWalletDirectory, "wallet.dat")))
        {
            throw new FileNotFoundException("wallet.dat was not found in the backup folder.");
        }
        Directory.CreateDirectory(destinationDirectory);
        CopyDirectory(sourceWalletDirectory, Path.Combine(destinationDirectory, walletName));
    }

    public static bool RunSelfTest()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"slithy-wallet-restore-test-{Guid.NewGuid():N}");
        try
        {
            string sourceParent = Path.Combine(root, "backup");
            string sourceWallet = Path.Combine(sourceParent, "restoretest");
            string destination = Path.Combine(root, "wallets");
            Directory.CreateDirectory(sourceWallet);
            File.WriteAllText(Path.Combine(sourceWallet, "wallet.dat"), "wallet data");
            File.WriteAllText(Path.Combine(sourceWallet, "metadata.txt"), "metadata");
            WalletCompatibilityTools.MarkSeedWallet(sourceParent, "restoretest");

            if (!TryFindBackupWalletDirectory(
                    sourceParent,
                    out string foundDirectory,
                    out string foundName))
            {
                return false;
            }
            if (foundDirectory != sourceWallet || foundName != "restoretest")
            {
                return false;
            }

            RestoreBackup(foundDirectory, destination, foundName);
            string restoredWallet = Path.Combine(destination, foundName, "wallet.dat");
            return File.Exists(restoredWallet) &&
                   File.ReadAllText(restoredWallet) == "wallet data";
        }
        catch
        {
            return false;
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (string directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
        }
        foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDirectory, file);
            File.Copy(file, Path.Combine(destinationDirectory, relative), overwrite: false);
        }
    }
}
