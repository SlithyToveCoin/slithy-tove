using System.Security.Cryptography;
using System.Diagnostics;
using Slithy_Tove;

if (args.Contains("--self-test-health-marker"))
{
    string testRoot = Path.Combine(Path.GetTempPath(), "slithy-health-marker-" + Guid.NewGuid().ToString("N"));
    string updates = Path.Combine(testRoot, "updates");
    string pendingPath = UpdateInstaller.CreatePendingUpdate(updates, "0.1.39", "0.1.40", Path.Combine(testRoot, "app", "Slithy Tove.exe"));
    PendingUpdate markerPending = UpdateInstaller.ReadPending(updates)!;
    string marker = Path.Combine(updates, "healthy-update.txt");
    File.WriteAllText(marker, "stale marker");
    using FileStream locked = new(marker, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    Task writer = Task.Run(() =>
    {
        Thread.Sleep(250);
        locked.Dispose();
        UpdateInstaller.MarkCurrentVersionHealthy(updates, new Version(0, 1, 40));
    });
    var wait = typeof(UpdateInstaller).GetMethod("WaitForHealthyMarker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
    bool healthy = (bool)wait.Invoke(null, new object[] { updates, markerPending.HealthToken, Process.GetCurrentProcess() })!;
    writer.GetAwaiter().GetResult();
    if (!healthy || File.ReadAllText(marker) != markerPending.HealthToken) throw new Exception("Locked health marker was not retried.");
    if (Directory.GetFiles(updates, "*.tmp").Length != 0) throw new Exception("Temporary health marker was left behind.");
    Directory.Delete(testRoot, recursive: true);
    Console.WriteLine("PASS: busy startup marker is retried and the complete token is published atomically.");
    return 0;
}

// This fixture has no wallet, node, network or GUI startup path.
if (args.Length == 4 && args[0] == "--apply-update")
    return UpdateInstaller.Apply(args[1], args[2], int.Parse(args[3]));
if (args.Any(a => a.StartsWith("--updated-from=")))
{
    if (File.Exists(Path.Combine(AppContext.BaseDirectory, "fail-startup"))) return 1;
    string updates = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixture-update-dir.txt"));
    UpdateInstaller.MarkCurrentVersionHealthy(updates, typeof(UpdateInstaller).Assembly.GetName().Version!);
    return 0;
}
if (args.Contains("--update-rolled-back"))
{
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "rollback-started"), "yes");
    return 0;
}

if (args.Length != 3) throw new ArgumentException("Expected fixture root, package and success|rollback.");
string root = Path.GetFullPath(args[0]);
if (!root.Contains("security-fixes") || !root.Contains("roundtrip")) throw new ArgumentException("Use the isolated roundtrip test directory.");
string install = Path.Combine(root, "installed");
string updatesDirectory = Path.Combine(root, "state", "updates");
string oldWallet = Path.Combine(root, "state", "networks", "previous-beta", "wallets", "wallet.dat");
Directory.CreateDirectory(Path.GetDirectoryName(oldWallet)!);
byte[] walletFixture = System.Text.Encoding.UTF8.GetBytes("disposable previous wallet fixture");
File.WriteAllBytes(oldWallet, walletFixture);
if (Directory.Exists(install)) throw new IOException("Use a fresh fixture root.");
Directory.CreateDirectory(install);
foreach (string file in Directory.EnumerateFiles(AppContext.BaseDirectory))
    File.Copy(file, Path.Combine(install, Path.GetFileName(file)));
File.WriteAllText(Path.Combine(install, "fixture-update-dir.txt"), updatesDirectory);
File.WriteAllText(Path.Combine(install, "old-marker"), "old file");
string package = Path.GetFullPath(args[1]);
using FileStream input = File.OpenRead(package);
string hash = Convert.ToHexString(SHA256.HashData(input));
input.Dispose();
string pending = UpdateInstaller.CreatePendingUpdate(updatesDirectory, "0.1.0", "0.2.0", Path.Combine(install, "Slithy Tove.exe"), hash);
// Run from a separate runtime folder, just as the desktop handoff does.
string helperDirectory = Path.Combine(root, "helper");
string helperExe = UpdateInstaller.CreateHelperCopy(install, helperDirectory, "Slithy Tove.exe");
ProcessStartInfo start = new(helperExe) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = helperDirectory, RedirectStandardOutput = true, RedirectStandardError = true };
foreach (string argument in new[] { "--apply-update", package, pending, int.MaxValue.ToString() })
    start.ArgumentList.Add(argument);
using Process helper = Process.Start(start) ?? throw new Exception("Helper failed to start.");
Task<string> helperOutput = helper.StandardOutput.ReadToEndAsync();
Task<string> helperError = helper.StandardError.ReadToEndAsync();
if (!helper.WaitForExit(60000)) { helper.Kill(entireProcessTree: true); throw new Exception("Helper timed out."); }
int result = helper.ExitCode;
File.WriteAllText(Path.Combine(root, "helper-output.log"), helperOutput.GetAwaiter().GetResult() + helperError.GetAwaiter().GetResult());
if (!File.ReadAllBytes(oldWallet).SequenceEqual(walletFixture)) throw new Exception("Update changed the previous wallet fixture.");
if (args[2] == "success")
{
    if (result != 0 || !File.Exists(Path.Combine(install, "introduced-marker"))) throw new Exception("Installer update failed.");
    Console.WriteLine("PASS: EXE installer used the requested folder and replacement startup was healthy.");
}
else
{
    if (result == 0 || File.Exists(Path.Combine(install, "introduced-marker")) || File.Exists(Path.Combine(install, "fail-startup")) || !File.Exists(Path.Combine(install, "old-marker")))
        throw new Exception("Installer rollback did not restore the previous file set.");
    for (int i = 0; i < 50 && !File.Exists(Path.Combine(install, "rollback-started")); i++) Thread.Sleep(100);
    if (!File.Exists(Path.Combine(install, "rollback-started"))) throw new Exception("Restored app did not start.");
    Console.WriteLine("PASS: failed EXE update restored the prior file set and restarted the old application.");
}
return 0;
