using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Slithy.Linux;
using Slithy_Tove;

if (!OperatingSystem.IsLinux() || args.Length != 3)
{
    Console.Error.WriteLine("Run on Linux with: PACKAGE.tar.gz OLD_GUI_FOLDER RESULTS_FOLDER");
    return 2;
}
string archive = Path.GetFullPath(args[0]), previous = Path.GetFullPath(args[1]), results = Path.GetFullPath(args[2]);
Match archiveName = Regex.Match(Path.GetFileName(archive), @"^Slithy-Tove-Linux-GUI-(?<version>\d+\.\d+\.\d+(?:-beta\.\d+)?)-x64\.tar\.gz$", RegexOptions.CultureInvariant);
if (!archiveName.Success)
{
    Console.Error.WriteLine("Use the release archive filename, including its version.");
    return 2;
}
string candidateVersion = archiveName.Groups["version"].Value;
Directory.CreateDirectory(results);
string root = Path.Combine(Path.GetTempPath(), "slithy-package-update-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(root, "config"));
Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(root, "data"));
Environment.SetEnvironmentVariable("XDG_CACHE_HOME", Path.Combine(root, "cache"));
Environment.SetEnvironmentVariable("SLITHY_NODE_BIN", null);
// The GUI's treasury HTTP request cannot reach a public service during this check.
Environment.SetEnvironmentVariable("HTTP_PROXY", "http://127.0.0.1:1");
Environment.SetEnvironmentVariable("HTTPS_PROXY", "http://127.0.0.1:1");
Environment.SetEnvironmentVariable("NO_PROXY", "127.0.0.1,localhost");
var store = new LinuxSettingsStore();
var settings = new LinuxSettings { AutoCheckUpdates = false, MinimizeToTrayOnClose = false, AutoLockEnabled = false, LastWalletName = "upgrade-fixture" };
string terms = File.ReadAllText(Path.Combine(previous, "licenses/BETA-TERMS.txt"));
settings.TermsAcceptedVersion = "1.0:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(terms)));
store.Save(settings);
using var rpc = new NodeRpcClient();
await using var oldHost = new LinuxNodeHost(store, settings, regtest: true, binaryPath: Path.Combine(previous, "Binaries/slithyd"));
var processes = new RegtestProcesses();
int checks = 0;
void Check(bool passed, string label)
{
    if (!passed) throw new InvalidOperationException(label);
    checks++; Console.WriteLine("PASS: " + label);
}
async Task Reject(Func<Task> action, string label)
{
    bool refused = false;
    try { await action(); }
    catch (Exception exception) when (exception is IOException or OperationCanceledException or InvalidOperationException or CryptographicException) { refused = true; }
    Check(refused, label);
}
Uri? current = null;
try
{
    string apps = Path.Combine(root, "apps");
    Directory.CreateDirectory(apps);
    string launcher = Path.Combine(LinuxGuiUpdatePaths.DesktopDataDirectory, "applications/io.slithy.Tove.desktop");
    string icon = Path.Combine(LinuxGuiUpdatePaths.DesktopDataDirectory, "icons/hicolor/256x256/apps/io.slithy.Tove.png");
    Directory.CreateDirectory(Path.GetDirectoryName(launcher)!);
    Directory.CreateDirectory(Path.GetDirectoryName(icon)!);
    File.WriteAllText(launcher, $"[Desktop Entry]\nName=Slithy Tove\nExec=\"{previous}/slithy-desktop\"\nIcon=io.slithy.Tove\n");
    File.Copy(Path.Combine(previous, "Assets/slithy-icon-256.png"), icon);
    byte[] oldLauncher = File.ReadAllBytes(launcher), oldIcon = File.ReadAllBytes(icon);

    await oldHost.StartAsync();
    current = await oldHost.GetEndpointAsync();
    Check((await rpc.CallAsync(current, "", "getblockchaininfo", Array.Empty<object>())).GetProperty("chain").GetString() == "regtest", "old installed native node uses isolated regtest");
    Check((await rpc.CallAsync(current, "", "getconnectioncount", Array.Empty<object>())).GetInt32() == 0, "old node has no public peers");
    string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    await rpc.CallAsync(current, "", "createwallet", new object[] { "upgrade-fixture", false, false, password });
    await rpc.CallAsync(current, "upgrade-fixture", "walletpassphrase", new object[] { password, 60 });
    string address = (await rpc.CallAsync(current, "upgrade-fixture", "getnewaddress", Array.Empty<object>())).GetString()!;
    await rpc.CallAsync(current, "", "generatetoaddress", new object[] { 102, address, 50_000_000 }, TimeSpan.FromMinutes(3));
    await rpc.CallAsync(current, "upgrade-fixture", "walletlock", Array.Empty<object>());
    JsonElement balance = await rpc.CallAsync(current, "upgrade-fixture", "getbalances", Array.Empty<object>());
    Check(balance.GetProperty("mine").GetProperty("trusted").GetDecimal() > 0, "old wallet has spendable rewards");
    Check(balance.GetProperty("mine").GetProperty("immature").GetDecimal() > 0, "old wallet has immature rewards");

    using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    byte[] package = await File.ReadAllBytesAsync(archive);
    var manifest = new LinuxGuiUpdateManifest(candidateVersion,
        "https://slithy.io/downloads/linux/" + Path.GetFileName(archive),
        Convert.ToHexString(SHA256.HashData(package)), "Disposable package integration test", DateTimeOffset.UtcNow.AddMinutes(-1),
        LinuxGuiUpdateService.Platform, LinuxGuiUpdateService.Network, LinuxGuiUpdateService.Genesis);
    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(manifest);
    byte[] envelope = JsonSerializer.SerializeToUtf8Bytes(new
    {
        payloadBase64 = Convert.ToBase64String(payload),
        signatureBase64 = Convert.ToBase64String(key.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
    });
    using var updates = new LinuxGuiUpdateService(LinuxGuiUpdatePaths.CacheDirectory,
        new FixtureHttp(envelope, package, manifest.PackageUrl), key.ExportSubjectPublicKeyInfoPem());
    Check((await updates.CheckAsync("0.1.43-beta.1")) == manifest, "signed fixture offers real candidate to the preceding version");
    using (var cancel = new CancellationTokenSource())
    {
        var progress = new InlineProgress(_ => cancel.Cancel());
        await Reject(() => updates.DownloadAsync(manifest, progress, cancel.Token), "mid-download cancellation refuses activation");
    }
    Check(oldHost.IsRunning && File.ReadAllBytes(launcher).SequenceEqual(oldLauncher), "cancelled download leaves old node and launcher running");
    Check(!Directory.EnumerateFiles(LinuxGuiUpdatePaths.CacheDirectory, "*.download").Any(), "cancelled download removes its partial archive");
    string downloaded = await updates.DownloadAsync(manifest);
    var installer = new LinuxGuiUpdateInstaller(apps, LinuxGuiUpdatePaths.DesktopDataDirectory, processes);
    PreparedLinuxGuiUpdate staged = await installer.StageAsync(manifest, downloaded);
    updates.RemoveCachedDownload(downloaded);
    Check(File.ReadAllBytes(Path.Combine(staged.Directory, "Binaries/slithyd")).SequenceEqual(File.ReadAllBytes(Path.Combine(previous, "Binaries/slithyd"))), "candidate preserves reviewed native binary");
    Check(File.Exists(Path.Combine(staged.Directory, "slithy-desktop.dll")) && oldHost.IsRunning, "real candidate archive stages while the old node runs");
    using (var cancel = new CancellationTokenSource())
    {
        cancel.Cancel();
        await Reject(() => installer.ActivateAndLaunchAsync(staged, () => throw new Exception("must not stop"), cancel.Token), "cancelled activation never stops the old node");
    }
    Check(oldHost.IsRunning, "old node remains available after activation cancellation");
    processes.FailBeforeLaunch = true;
    await Reject(() => installer.ActivateAndLaunchAsync(staged, oldHost.StopOwnedAsync), "replacement launch failure is reported");
    current = null;
    Check(File.ReadAllBytes(launcher).SequenceEqual(oldLauncher) && File.ReadAllBytes(icon).SequenceEqual(oldIcon), "failed replacement restores prior launcher and icon");
    await oldHost.StartAsync();
    current = await oldHost.GetEndpointAsync();
    await rpc.CallAsync(current, "", "loadwallet", new object[] { "upgrade-fixture" });
    Check((await rpc.CallAsync(current, "upgrade-fixture", "getbalances", Array.Empty<object>())).GetRawText() == balance.GetRawText(), "old wallet balance survives failed update and restart");
    processes.FailBeforeLaunch = false;
    await installer.ActivateAndLaunchAsync(staged, async () =>
    {
        await oldHost.StopOwnedAsync(); current = null;
        Check(!oldHost.IsRunning, "owned old node completes shutdown before the replacement GUI starts");
    });
    Check(processes.Child is { HasExited: false }, "real replacement GUI remains open after ready/proceed handoff");
    current = await ReadNewEndpoint(store, rpc);
    Check((await rpc.CallAsync(current, "", "getblockchaininfo", Array.Empty<object>())).GetProperty("chain").GetString() == "regtest", "replacement GUI starts an isolated regtest node");
    Check((await rpc.CallAsync(current, "", "getconnectioncount", Array.Empty<object>())).GetInt32() == 0, "replacement node has no public peers");
    JsonElement wallets = await rpc.CallAsync(current, "", "listwallets", Array.Empty<object>());
    if (!wallets.EnumerateArray().Any(item => item.GetString() == "upgrade-fixture"))
        await rpc.CallAsync(current, "", "loadwallet", new object[] { "upgrade-fixture" });
    Check((await rpc.CallAsync(current, "upgrade-fixture", "getbalances", Array.Empty<object>())).GetRawText() == balance.GetRawText(), "spendable and immature wallet balances survive the complete update");
    Check((await rpc.CallAsync(current, "upgrade-fixture", "getaddressinfo", new object[] { address })).GetProperty("ismine").GetBoolean(), "replacement retains the original wallet address");
    Check((await rpc.CallAsync(current, "upgrade-fixture", "getwalletinfo", Array.Empty<object>())).GetProperty("unlocked_until").GetInt64() == 0, "wallet stays encrypted and locked after replacement");
    Check(!(await rpc.GetMiningInfoAsync(current)).Active, "replacement does not start mining automatically");
    Check(store.Load().LastWalletName == "upgrade-fixture" && !store.Load().AutoLockEnabled, "wallet selection and timeout preferences survive update");
    Check(File.ReadAllText(launcher).Contains(staged.Directory + "/slithy-desktop"), "desktop launcher points to the verified candidate");
    Check(File.Exists(Path.Combine(previous, "slithy-desktop")), "previous version remains available");
    await File.WriteAllTextAsync(Path.Combine(results, "package-upgrade-result.json"), JsonSerializer.Serialize(new
    {
        passed = checks, candidateSha256 = manifest.Sha256.ToLowerInvariant(),
        boundary = "Production download/staging/activation and node ownership code; test process adapter adds --regtest to the actual unchanged GUI executable. Temporary signing key and in-memory HTTP fixture; no production trust changes."
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"PASS: {checks} packaged GUI upgrade checks. No public-chain wallets or transactions.");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine("FAIL: packaged GUI upgrade: " + error.GetType().Name + ": " + error.Message);
    Console.Error.WriteLine("Disposable test data: " + root);
    return 1;
}
finally
{
    if (current != null)
    {
        try { await rpc.StopDaemonAsync(current); } catch { }
        string data = Path.Combine(store.DataDirectory, "isolated-regtest");
        for (int attempt = 0; attempt < 180 && Directory.Exists(data) && Directory.EnumerateFiles(data, "gui-rpc-*.cookie").Any(); attempt++)
            await Task.Delay(250);
    }
    await oldHost.StopOwnedAsync();
    await processes.StopTestChildAsync();
    // Preserve the disposable profile for inspection. No secrets from it are copied to the report.
}

static async Task<Uri> ReadNewEndpoint(LinuxSettingsStore store, NodeRpcClient rpc)
{
    string data = Path.Combine(store.DataDirectory, "isolated-regtest");
    for (int attempt = 0; attempt < 160; attempt++)
    {
        foreach (string path in Directory.EnumerateFiles(data, "gui-rpc-*.cookie"))
        {
            try
            {
                string[] parts = (await File.ReadAllTextAsync(path)).Trim().Split(':', 2);
                if (parts.Length != 2) continue;
                var endpoint = new Uri($"http://{Uri.EscapeDataString(parts[0])}:{Uri.EscapeDataString(parts[1])}@127.0.0.1:54436/");
                await rpc.CallAsync(endpoint, "", "getblockchaininfo", Array.Empty<object>(), TimeSpan.FromSeconds(1));
                return endpoint;
            }
            catch (Exception error) when (error is IOException or HttpRequestException or InvalidOperationException or OperationCanceledException) { }
        }
        await Task.Delay(250);
    }
    throw new TimeoutException("Replacement node did not become ready.");
}

sealed class FixtureHttp(byte[] envelope, byte[] package, string packageUrl) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        byte[] content = request.RequestUri?.AbsoluteUri == packageUrl ? package : envelope;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
    }
}
sealed class InlineProgress(Action<int> report) : IProgress<int> { public void Report(int value) => report(value); }

sealed class RegtestProcesses : ILinuxGuiUpdateProcesses
{
    public bool FailBeforeLaunch;
    public Process? Child;
    public Task ValidateAsync(string directory, CancellationToken token) => new LinuxGuiUpdateProcesses().ValidateAsync(directory, token);

    public async Task LaunchAsync(string directory, string version, CancellationToken token)
    {
        if (FailBeforeLaunch) throw new IOException("Disposable launch failure fixture.");
        string handoff = Path.Combine(LinuxGuiUpdatePaths.CacheDirectory, "handoff-" + Guid.NewGuid().ToString("N"));
        LinuxGuiUpdatePaths.EnsureDirectory(handoff);
        string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await File.WriteAllTextAsync(Path.Combine(handoff, "request.json"), JsonSerializer.Serialize(new
        { Token = secret, Directory = directory, Version = version, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30) }), token);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        Child = Process.Start(new ProcessStartInfo(Path.Combine(directory, "slithy-desktop"))
        {
            UseShellExecute = false, WorkingDirectory = directory,
            ArgumentList = { "--regtest", "--update-handoff", handoff },
            RedirectStandardOutput = true, RedirectStandardError = true
        }) ?? throw new IOException("Could not start the candidate GUI.");
        Child.OutputDataReceived += (_, _) => { }; Child.ErrorDataReceived += (_, _) => { };
        Child.BeginOutputReadLine(); Child.BeginErrorReadLine();
        while (!File.Exists(Path.Combine(handoff, "ready.json")))
        {
            if (Child.HasExited) throw new IOException("Candidate exited before window readiness.");
            await Task.Delay(100, deadline.Token);
        }
        using JsonDocument ready = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(handoff, "ready.json"), deadline.Token));
        if (ready.RootElement.GetProperty("Token").GetString() != secret || ready.RootElement.GetProperty("ProcessId").GetInt32() != Child.Id)
            throw new IOException("Wrong replacement identity.");
        string pending = Path.Combine(handoff, ".proceed");
        await File.WriteAllTextAsync(pending, ready.RootElement.GetRawText(), deadline.Token);
        File.Move(pending, Path.Combine(handoff, "proceed.json"));
        while (Directory.Exists(handoff)) await Task.Delay(100, deadline.Token);
    }

    public async Task StopTestChildAsync()
    {
        if (Child is not { HasExited: false }) return;
        using Process? signal = Process.Start(new ProcessStartInfo("/bin/kill")
        { UseShellExecute = false, ArgumentList = { "-TERM", Child.Id.ToString() } });
        if (signal != null) await signal.WaitForExitAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Child.WaitForExitAsync(timeout.Token);
        Child.Dispose();
    }
}
