using System.Text.Json;

namespace Slithy.Linux;

internal static class LinuxRuntimeSelfTest
{
    public static async Task<int> RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "slithy-gui-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            LinuxSettingsStore store = new(root);
            LinuxSettings settings = store.Load();
            Check(settings.AutoLockEnabled && settings.AutoLockMinutes == 10, "Secure defaults");
            Check(settings.AutoCheckUpdates, "Update checks start enabled");
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Check(settings.ShouldAutoLock(now.AddMinutes(-11), now), "Inactivity timeout");
            settings.AutoLockEnabled = false;
            settings.AutoCheckUpdates = false;
            Check(!settings.ShouldAutoLock(now.AddDays(-1), now), "Optional inactivity timeout");
            settings.AutoLockMinutes = -5;
            settings.PublicNodePreference = "untrusted.example";
            settings.LastWalletName = "test wallet";
            store.Save(settings);
            LinuxSettings reloaded = store.Load();
            Check(!reloaded.AutoLockEnabled && reloaded.AutoLockMinutes == 1, "Persist and clamp timeout");
            Check(!reloaded.AutoCheckUpdates, "Remember disabled update checks");
            Check(reloaded.PublicNodePreference == "auto" && reloaded.LastWalletName == "test wallet", "Validate preferences");
            string json = File.ReadAllText(store.SettingsPath);
            Check(!json.Contains("password", StringComparison.OrdinalIgnoreCase), "No password setting");
            if (OperatingSystem.IsLinux())
            {
                Check(File.GetUnixFileMode(store.SettingsPath) == LinuxSettingsStore.PrivateFile, "Private settings permissions");
                Check(File.GetUnixFileMode(store.ConfigDirectory) == LinuxSettingsStore.PrivateDirectory, "Private directory permissions");
            }
            await using LinuxNodeHost host = new(store, settings, regtest: true);
            IReadOnlyList<string> args = host.BuildArguments(Path.Combine(root, "node.conf"));
            Check(args.Contains("-connect=0") && args.Contains("-dnsseed=0"), "Regtest isolation");
            Check(!args.Any(a => a.StartsWith("-maxtipage=")), "Beta tip-age exception stays out of regtest");
            Check(args.Contains("-rpcbind=127.0.0.1") && args.Contains("-listen=0"), "Loopback RPC");
            Check(args.Contains("-natpmp=0") && !args.Any(a => a.StartsWith("-upnp=")), "Supported native port mapping option");
            Check(args.All(a => !a.StartsWith("-rpcpassword")), "Cookie authentication");
            Check(args.All(a => !a.StartsWith("-gen=") && !a.StartsWith("-startcpumining")), "No automatic mining");
            Check(host.DataDirectory != store.DataDirectory && host.WalletDirectory.StartsWith(host.DataDirectory), "Separate regtest data");
            Check(LinuxNodeHost.StartupDiagnostic("secret-token-placeholder") == null, "Unrecognized startup output stays private");
            Check(LinuxNodeHost.StartupDiagnostic("error while loading shared libraries: private/path.so")?.Contains("private/path") == false, "Startup diagnostics exclude arbitrary output");
            Check(LinuxNodeHost.StartupDiagnostic("version GLIBC_2.38 not found")?.Contains("newer Linux") == true, "C library compatibility error");
            Check(LinuxNodeHost.StartupDiagnostic("Error: Disk space is too low!")?.Contains("disk space") == true, "Disk startup error");
            await using LinuxNodeHost beta = new(store, settings);
            IReadOnlyList<string> betaArgs = beta.BuildArguments(Path.Combine(root, "beta.conf"));
            Check(betaArgs.Contains("-testnet") && betaArgs.Contains("-maxtipage=2147483647"), "Quiet beta startup matches Windows");
            Check(betaArgs.Contains("-deprecatedrpc=startingheight"), "Beta handshake heights");
            Check(betaArgs.Count(a => a.StartsWith("-addnode=")) == 3, "Beta seed fallback");
            try { await host.GetEndpointAsync(); throw new Exception("Stopped node exposed RPC"); }
            catch (InvalidOperationException) { }
            if (OperatingSystem.IsLinux())
            {
                await using LinuxNodeHost missing = new(store, settings, regtest: true, binaryPath: Path.Combine(root, "missing-slithyd"));
                try { await missing.StartAsync(); throw new Exception("Missing binary was accepted"); }
                catch (FileNotFoundException) { }
                using FileStream checkLock = new(Path.Combine(missing.DataDirectory, "gui-instance.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                Check(!missing.IsRunning, "Failed startup releases its instance lock");
            }
            File.WriteAllText(store.SettingsPath, "{ broken");
            try { store.Load(); throw new Exception("Invalid settings were silently discarded"); }
            catch (InvalidOperationException) { }
            Console.WriteLine("PASS Linux settings, timeout, private paths and node isolation");
            return 0;
        }
        finally
        {
            // This directory was created above with a unique name for this test.
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Runtime self-test failed: " + name);
    }
}
