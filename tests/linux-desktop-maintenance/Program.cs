using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Slithy.Linux;

if (!OperatingSystem.IsLinux()) { Console.Error.WriteLine("Run these tests on Linux with the reviewed slithyd path."); return 2; }
if (args.Length == 3 && args[0] == "--leave-node")
{
    var childStore = new LinuxSettingsStore(args[1]);
    var childHost = new LinuxNodeHost(childStore, new LinuxSettings(), regtest: true, binaryPath: args[2]);
    await childHost.StartAsync();
    await File.WriteAllTextAsync(Path.Combine(args[1], "ready"), "ready");
    await Task.Delay(Timeout.Infinite);
    return 0;
}
if (args.Length != 1 || !Path.IsPathFullyQualified(args[0]) || !File.Exists(args[0])) return 2;
string native = args[0];
string root = Path.Combine(Path.GetTempPath(), "slithy-maintenance-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Console.WriteLine("PASS: " + message); }
async Task Refuses(Func<Task> action, string message)
{
    bool refused = false;
    try { await action(); }
    catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Net.Http.HttpRequestException or JsonException) { refused = true; }
    Check(refused, message);
}

try
{
    string package = Path.Combine(root, "extracted");
    Directory.CreateDirectory(Path.Combine(package, "Binaries"));
    Directory.CreateDirectory(Path.Combine(package, "Assets"));
    var entries = new List<DesktopInstallFile>();
    foreach (string name in new[] { "slithy-desktop", "slithy-desktop.dll", "Binaries/slithyd", "Assets/slithy-icon-256.png", "LICENSE", "NOTICE" })
    {
        string path = Path.Combine(package, name);
        if (name == "slithy-desktop.dll") File.Copy(Assembly.GetExecutingAssembly().Location, path);
        else await File.WriteAllTextAsync(path, "test program file: " + name);
        entries.Add(new(name, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path))), name is "slithy-desktop" or "Binaries/slithyd"));
    }
    string inventory = Path.Combine(package, "install-files.json");
    string inventoryText = JsonSerializer.Serialize(entries);
    await File.WriteAllTextAsync(inventory, inventoryText);
    await File.WriteAllTextAsync(Path.Combine(package, "wallet.dat"), "unlisted source wallet must not be copied");
    string apps = Path.Combine(root, "programs"), desktopData = Path.Combine(root, "desktop-data");
    Directory.CreateDirectory(Path.Combine(desktopData, "wallets"));
    string canary = Path.Combine(desktopData, "wallets", "wallet.dat");
    await File.WriteAllTextAsync(canary, "preserve wallet");
    var installer = new DesktopInstallService(apps, desktopData);
    DesktopInstallPlan plan = await installer.InspectAsync(package);
    Check(plan.Files.Length == 6, "graphical installer inspects the complete inventory");
    string target = await installer.InstallAsync(plan);
    Check(File.Exists(Path.Combine(target, "slithy-desktop")), "program copied to its managed version folder");
    Check(!File.Exists(Path.Combine(target, "wallet.dat")), "unlisted nearby wallet is not copied");
    Check((File.GetUnixFileMode(Path.Combine(target, "slithy-desktop")) & UnixFileMode.UserExecute) != 0, "installed executable mode");
    string launcher = Path.Combine(desktopData, "applications/io.slithy.Tove.desktop");
    Check((await File.ReadAllTextAsync(launcher)).Contains("[Desktop Action Uninstall]"), "application menu includes uninstall action");
    Check(await installer.InstallAsync(plan) == target, "identical package installation is repeatable");
    await File.WriteAllTextAsync(Path.Combine(target, "my-note.txt"), "preserve extra file");
    await installer.UninstallAsync(target);
    Check(!File.Exists(launcher) && !File.Exists(Path.Combine(target, "slithy-desktop")), "uninstall removes program and shortcut");
    Check(await File.ReadAllTextAsync(canary) == "preserve wallet", "uninstall preserves wallet data");
    Check(await File.ReadAllTextAsync(Path.Combine(target, "my-note.txt")) == "preserve extra file", "uninstall preserves unlisted files");
    await Refuses(() => installer.UninstallAsync(package), "uninstall refuses extracted download folders");
    await File.AppendAllTextAsync(Path.Combine(package, "NOTICE"), "changed");
    await Refuses(() => installer.InspectAsync(package), "modified package is rejected");
    await File.WriteAllTextAsync(Path.Combine(package, "NOTICE"), "test program file: NOTICE");
    await File.WriteAllTextAsync(inventory, inventoryText.Replace("LICENSE", "../outside"));
    await Refuses(() => installer.InspectAsync(package), "traversal inventory is rejected");
    await File.WriteAllTextAsync(inventory, inventoryText);
    File.Delete(Path.Combine(package, "NOTICE")); File.CreateSymbolicLink(Path.Combine(package, "NOTICE"), canary);
    await Refuses(() => installer.InspectAsync(package), "symlink package is rejected");
    File.Delete(Path.Combine(package, "NOTICE")); await File.WriteAllTextAsync(Path.Combine(package, "NOTICE"), "test program file: NOTICE");
    Directory.CreateDirectory(Path.Combine(root, "native-check/Binaries"));
    File.Copy(native, Path.Combine(root, "native-check/Binaries/slithyd"));
    File.SetUnixFileMode(Path.Combine(root, "native-check/Binaries/slithyd"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    await DesktopInstallService.CheckNativeDependenciesAsync(Path.Combine(root, "native-check"));
    Check(true, "bundled daemon dependency probe runs version check without a node");

    string nodeRoot = Path.Combine(root, "orphan-fixture"); Directory.CreateDirectory(nodeRoot);
    var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    start.ArgumentList.Add("--leave-node"); start.ArgumentList.Add(nodeRoot); start.ArgumentList.Add(native);
    using var child = Process.Start(start) ?? throw new Exception("Test child failed to start.");
    Task<string> childOutput = child.StandardOutput.ReadToEndAsync(), childError = child.StandardError.ReadToEndAsync();
    var watch = Stopwatch.StartNew();
    while (!File.Exists(Path.Combine(nodeRoot, "ready")))
    {
        if (child.HasExited) throw new Exception("Test child exited: " + await childError);
        if (watch.Elapsed > TimeSpan.FromSeconds(100)) { child.Kill(); throw new Exception("Test child timeout."); }
        await Task.Delay(100);
    }
    child.Kill(); await child.WaitForExitAsync();
    var store = new LinuxSettingsStore(nodeRoot);
    string receiptPath = Path.Combine(store.DataDirectory, "isolated-regtest/gui-node-owner.json");
    string receiptText = await File.ReadAllTextAsync(receiptPath);
    var receipt = JsonNode.Parse(receiptText)!;
    int orphanPid = receipt["ProcessId"]!.GetValue<int>();
    Check(Directory.Exists($"/proc/{orphanPid}"), "crashed GUI leaves its disposable native node running");
    async Task TryStart(Func<Task<bool>>? approval = null)
    {
        await using var host = new LinuxNodeHost(store, new LinuxSettings(), regtest: true, binaryPath: native) { ConfirmOrphanRecoveryAsync = approval };
        await host.StartAsync();
    }
    try
    {
        await Refuses(() => TryStart(), "recovery without approval is refused");
        Check(Directory.Exists($"/proc/{orphanPid}"), "refused recovery leaves previous node running");
        foreach (string field in new[] { "StartTicks", "ExecutableHash" })
        {
            var corrupt = JsonNode.Parse(receiptText)!;
            corrupt[field] = field == "StartTicks" ? "0" : new string('0', 64);
            await File.WriteAllTextAsync(receiptPath, corrupt.ToJsonString());
            await Refuses(() => TryStart(() => Task.FromResult(true)), "recovery refuses changed " + field);
            Check(Directory.Exists($"/proc/{orphanPid}"), "identity mismatch does not stop node");
        }
        var wrongFolder = JsonNode.Parse(receiptText)!;
        var wrongArguments = wrongFolder["Arguments"]!.AsArray();
        for (int i = 0; i < wrongArguments.Count; i++)
            if (wrongArguments[i]!.GetValue<string>().StartsWith("-datadir=", StringComparison.Ordinal)) wrongArguments[i] = "-datadir=/unrelated";
        await File.WriteAllTextAsync(receiptPath, wrongFolder.ToJsonString());
        await Refuses(() => TryStart(() => Task.FromResult(true)), "recovery refuses another data folder");
        Check(Directory.Exists($"/proc/{orphanPid}"), "wrong data folder leaves node running");
        await File.WriteAllTextAsync(receiptPath, receiptText);
        string cookiePath = receipt["Arguments"]!.AsArray().Select(x => x!.GetValue<string>()).Single(x => x.StartsWith("-rpccookiefile=", StringComparison.Ordinal))[15..];
        File.SetUnixFileMode(cookiePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);
        await Refuses(() => TryStart(() => Task.FromResult(true)), "recovery refuses a public authentication cookie");
        File.SetUnixFileMode(cookiePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        string originalCookie = await File.ReadAllTextAsync(cookiePath);
        await Refuses(() => TryStart(async () => { await File.WriteAllTextAsync(cookiePath, "invalid:fixture-only"); return true; }), "recovery rechecks authentication after approval");
        await File.WriteAllTextAsync(cookiePath, originalCookie);
        Check(Directory.Exists($"/proc/{orphanPid}"), "failed authentication does not stop node");
        bool asked = false;
        await TryStart(() => { asked = true; return Task.FromResult(true); });
        Check(asked, "verified orphan recovery asks the user");
        Check(!File.Exists(receiptPath), "clean shutdown removes the replacement ownership record");
        bool alive = Directory.Exists($"/proc/{orphanPid}") && !File.ReadAllText($"/proc/{orphanPid}/stat").Contains(") Z ");
        Check(!alive, "approved recovery cleanly stops the previous node");
        await using (var damagedReceipt = new LinuxNodeHost(store, new LinuxSettings(), regtest: true, binaryPath: native))
        {
            await damagedReceipt.StartAsync();
            await File.WriteAllTextAsync(receiptPath, "{invalid-fixture");
            await damagedReceipt.StopOwnedAsync();
            Check(!damagedReceipt.IsRunning && File.Exists(receiptPath), "damaged recovery record does not prevent clean shutdown");
        }
        File.Delete(receiptPath);
        await TryStart();
        Check(!File.Exists(receiptPath), "shutdown releases the instance lock despite a damaged record");
    }
    finally
    {
        // This PID came from the test child in the temporary regtest data folder.
        if (Directory.Exists($"/proc/{orphanPid}"))
        {
            string command = System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync($"/proc/{orphanPid}/cmdline"));
            if (command.Contains(nodeRoot, StringComparison.Ordinal))
            {
                await File.WriteAllTextAsync(receiptPath, receiptText);
                try { await TryStart(() => Task.FromResult(true)); } catch { }
            }
        }
    }
    Console.WriteLine($"PASS: {checks} Linux installer and crash-recovery checks. No installed wallets were used.");
    return 0;
}
finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
