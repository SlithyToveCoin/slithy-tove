using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Slithy.Linux;

internal static class LinuxGuiUpdateSelfTest
{
    private static int _checks;
    private const string Version = "0.1.44-beta.1";
    private static readonly string Prefix = "slithy-tove-linux-gui-" + Version;

    public static async Task<int> RunAsync()
    {
        _checks = 0;
        string root = Path.Combine(Path.GetTempPath(), "slithy-update-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string publicKey = key.ExportSubjectPublicKeyInfoPem();
        try
        {
            byte[] package = Archive();
            LinuxGuiUpdateManifest manifest = Manifest(package);
            byte[] envelope = Sign(key, manifest);
            Check(LinuxGuiUpdateService.VerifyManifest(envelope, publicKey) == manifest, "valid signed GUI manifest");
            Check(LinuxGuiUpdateService.VerifyManifest(new byte[] { 0xef, 0xbb, 0xbf }.Concat(envelope).ToArray(), publicKey) == manifest, "UTF-8 release envelope");
            using ECDsa other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            Reject(() => LinuxGuiUpdateService.VerifyManifest(envelope, other.ExportSubjectPublicKeyInfoPem()), "wrong signing key");
            byte[] tampered = envelope.ToArray(); tampered[30] = tampered[30] == (byte)'A' ? (byte)'B' : (byte)'A';
            Reject(() => LinuxGuiUpdateService.VerifyManifest(tampered, publicKey), "modified signed payload");
            foreach (LinuxGuiUpdateManifest invalid in new[]
            {
                manifest with { Platform = "linux-x64" },
                manifest with { Platform = "windows-x64" },
                manifest with { NetworkResetId = "new-network" },
                manifest with { Genesis = new string('0', 64) },
                manifest with { PackageUrl = manifest.PackageUrl.Replace("https:", "http:") },
                manifest with { PackageUrl = manifest.PackageUrl + "?redirect=elsewhere" },
                manifest with { PackageUrl = manifest.PackageUrl.Replace("slithy.io", "slithy.io.example") },
                manifest with { PackageUrl = manifest.PackageUrl.Replace("Slithy-Tove-Linux-GUI-", "slithy-linux-") },
                manifest with { Sha256 = "missing" },
                manifest with { PublishedAt = DateTimeOffset.UtcNow.AddDays(3) },
                manifest with { Version = "../next" },
            }) Reject(() => LinuxGuiUpdateService.VerifyManifest(Sign(key, invalid), publicKey), "signed wrong app, network, URL, hash, date or version");
            string payload = JsonSerializer.Serialize(manifest);
            payload = payload.TrimEnd('}') + ",\"platform\":\"linux-gui-x64\"}";
            Reject(() => LinuxGuiUpdateService.VerifyManifest(SignBytes(key, Encoding.UTF8.GetBytes(payload)), publicKey), "duplicate signed fields");
            foreach (string invalid in new[] { "v0.1.44", "0.1", "01.1.44", "0.1.44-testnet", "0.1.44+other", "0.1.44-beta.-1", "999999999999999999.1.1", "0.1.44\n" })
                Reject(() => LinuxGuiVersion.Parse(invalid), "invalid GUI version");
            Check(LinuxGuiVersion.Parse("0.1.44-beta.10").CompareTo(LinuxGuiVersion.Parse("0.1.44-beta.2")) > 0, "numeric beta version ordering");
            Check(LinuxGuiVersion.Parse("0.1.44").CompareTo(LinuxGuiVersion.Parse(Version)) > 0, "stable version follows its beta");

            using (var service = Service(root, publicKey, _ => Response(envelope)))
            {
                Check(await service.CheckAsync("0.1.43-beta.1") == manifest, "new release is offered");
                Check(await service.CheckAsync(Version) == null, "current release is not offered again");
                Check(await service.CheckAsync("0.1.45-beta.1") == null, "older release is not installed");
            }
            using (var service = Service(root, publicKey, _ => new HttpResponseMessage(HttpStatusCode.Redirect)))
                await RejectAsync(() => service.CheckAsync("0.1.43-beta.1"), "manifest redirects refused");
            using (var service = Service(root, publicKey, _ => new HttpResponseMessage(HttpStatusCode.NotFound)))
            {
                try { await service.CheckAsync("0.1.43-beta.1"); throw new Exception("404 looked current"); }
                catch (InvalidOperationException ex) { Check(ex.Message.Contains("not available"), "missing feed is not shown as up to date"); }
            }
            using (var service = Service(root, publicKey, _ => Response(envelope, declaredLength: LinuxGuiUpdateService.MaxManifestBytes + 1)))
                await RejectAsync(() => service.CheckAsync("0.1.43-beta.1"), "large manifest header refused");
            using (var service = Service(root, publicKey, _ => Response(new byte[LinuxGuiUpdateService.MaxManifestBytes + 1])))
                await RejectAsync(() => service.CheckAsync("0.1.43-beta.1"), "large manifest body refused");

            string archive;
            using (var service = Service(root, publicKey, request =>
            {
                Check(request.RequestUri?.AbsoluteUri == manifest.PackageUrl, "download uses the signed GUI URL");
                return Response(package);
            }))
            {
                archive = await service.DownloadAsync(manifest);
                Check(File.ReadAllBytes(archive).SequenceEqual(package), "verified package download");
            }
            using (var service = Service(root, publicKey, _ => Response(package)))
                await RejectAsync(() => service.DownloadAsync(manifest with { Sha256 = new string('a', 64) }), "package hash mismatch");
            using (var service = Service(root, publicKey, _ => new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)))
                await RejectAsync(() => service.DownloadAsync(manifest), "package redirects refused");
            using (var service = Service(root, publicKey, _ => Response(package, declaredLength: LinuxGuiUpdateService.MaxPackageBytes + 1)))
                await RejectAsync(() => service.DownloadAsync(manifest), "large package header refused");
            using (var service = Service(root, publicKey, _ => Response(package, declaredLength: package.Length + 1)))
                await RejectAsync(() => service.DownloadAsync(manifest), "truncated package refused");
            using (var service = new LinuxGuiUpdateService(Path.Combine(root, "cache"), new Handler(_ => Response(package)), publicKey, _ => 0))
                await RejectAsync(() => service.DownloadAsync(manifest), "low cache space refuses download before writing");
            int spaceChecks = 0;
            using (var service = new LinuxGuiUpdateService(Path.Combine(root, "cache"), new Handler(_ => Response(package)), publicKey,
                _ => ++spaceChecks == 1 ? LinuxGuiUpdateService.CacheReserveBytes + package.Length : 0))
                await RejectAsync(() => service.DownloadAsync(manifest), "falling cache space stops download before exhausting reserve");
            using (var service = Service(root, publicKey, _ => Response(package)))
            {
                using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
                await RejectAsync(() => service.DownloadAsync(manifest, cancellationToken: cancelled.Token), "cancelled download leaves no partial package");
            }
            Check(!Directory.EnumerateFiles(Path.Combine(root, "cache"), "*.download").Any(), "failed download files removed");
            if (OperatingSystem.IsLinux()) Check(File.GetUnixFileMode(archive) == LinuxGuiUpdatePaths.PrivateFile, "private downloaded package permissions");

            var processes = new FakeProcesses();
            string apps = Path.Combine(root, "apps");
            string desktop = Path.Combine(root, "desktop");
            var installer = new LinuxGuiUpdateInstaller(apps, desktop, processes);
            var lowSpaceInstaller = new LinuxGuiUpdateInstaller(Path.Combine(root, "low-space-apps"), desktop, processes, _ => 0);
            await RejectAsync(() => lowSpaceInstaller.StageAsync(manifest, archive), "low application space refuses staging");
            int extractionSpaceChecks = 0;
            var fallingSpaceInstaller = new LinuxGuiUpdateInstaller(Path.Combine(root, "falling-space-apps"), desktop, processes,
                _ => ++extractionSpaceChecks <= 2 ? 2 * LinuxGuiUpdateService.CacheReserveBytes : 0);
            await RejectAsync(() => fallingSpaceInstaller.StageAsync(manifest, archive), "falling application space stops extraction before exhausting reserve");
            Check(!Directory.EnumerateDirectories(Path.Combine(root, "falling-space-apps"), ".incoming-*").Any(), "failed extraction removes its temporary files");
            PreparedLinuxGuiUpdate prepared = await installer.StageAsync(manifest, archive);
            Check(processes.Validations == 1 && File.Exists(Path.Combine(prepared.Directory, "slithy-desktop")), "package staged and startup checked");
            if (OperatingSystem.IsLinux())
                Check((File.GetUnixFileMode(Path.Combine(prepared.Directory, "slithy-desktop")) & UnixFileMode.UserExecute) != 0, "staged executable permissions");
            Check((await installer.StageAsync(manifest, archive)).Directory == prepared.Directory, "verified staged release can be retried");
            string wallet = Path.Combine(root, "untouched-wallet"); File.WriteAllText(wallet, "test-wallet-bytes");
            bool stopped = false;
            processes.BeforeLaunch = () => Check(stopped, "node shutdown precedes replacement launch");
            await installer.ActivateAndLaunchAsync(prepared, () => { stopped = true; return Task.CompletedTask; });
            string launcher = Path.Combine(desktop, "applications/io.slithy.Tove.desktop");
            string icon = Path.Combine(desktop, "icons/hicolor/256x256/apps/io.slithy.Tove.png");
            Check(File.ReadAllText(launcher).Contains(prepared.Directory + "/slithy-desktop"), "launcher targets the new managed version");
            Check(File.ReadAllText(wallet) == "test-wallet-bytes", "unrelated wallet files untouched");
            string previousVersion = Path.Combine(apps, "0.1.43-beta.1");
            File.WriteAllText(launcher, $"[Desktop Entry]\nName=Slithy Tove\nExec=\"{previousVersion}/slithy-desktop\"\nIcon=previous-slithy\n");
            File.WriteAllBytes(icon, Encoding.UTF8.GetBytes("previous app icon bytes"));
            byte[] oldLauncher = File.ReadAllBytes(launcher), oldIcon = File.ReadAllBytes(icon);
            processes.FailLaunch = true;
            stopped = false;
            await RejectAsync(() => installer.ActivateAndLaunchAsync(prepared, () => { stopped = true; return Task.CompletedTask; }), "replacement failure reported");
            Check(File.ReadAllBytes(launcher).SequenceEqual(oldLauncher) && File.ReadAllBytes(icon).SequenceEqual(oldIcon), "failed launch restores launcher and icon");
            Check(Directory.Exists(prepared.Directory), "failed launch does not delete the package");
            processes.FailLaunch = false;
            await RejectAsync(() => installer.ActivateAndLaunchAsync(prepared, () => throw new IOException("still saving")), "node shutdown failure aborts installation");
            Check(File.ReadAllBytes(launcher).SequenceEqual(oldLauncher), "shutdown failure leaves launcher unchanged");

            var malicious = new Dictionary<string, byte[]>
            {
                ["parent traversal"] = Archive(extraName: Prefix + "/../wallet.dat"),
                ["absolute path"] = Archive(extraName: "/tmp/slithy-unexpected"),
                ["wrong archive root"] = Archive(extraName: "wrong-root/extra.txt"),
                ["backslash path"] = Archive(extraName: Prefix + "/..\\wallet.dat"),
                ["nested data path"] = Archive(extraName: Prefix + "/wallets/test/wallet.dat"),
                ["wallet data entry"] = Archive(extraName: Prefix + "/wallet.dat"),
                ["duplicate entry"] = Archive(extraName: Prefix + "/README.md"),
                ["symbolic link entry"] = Archive(extraName: Prefix + "/link", type: TarEntryType.SymbolicLink),
                ["hard link entry"] = Archive(extraName: Prefix + "/link", type: TarEntryType.HardLink),
                ["special file entry"] = Archive(extraName: Prefix + "/pipe", type: TarEntryType.Fifo),
                ["missing required file"] = Archive(omit: "Binaries/slithyd"),
                ["empty archive"] = Archive(empty: true),
                ["oversized file"] = OversizedEntryArchive(),
            };
            foreach ((string label, byte[] bytes) in malicious)
            {
                string badPath = Path.Combine(root, "bad-" + Guid.NewGuid().ToString("N") + ".tar.gz"); File.WriteAllBytes(badPath, bytes);
                var badInstaller = new LinuxGuiUpdateInstaller(Path.Combine(root, "badapps-" + Guid.NewGuid().ToString("N")), desktop, processes);
                await RejectAsync(() => badInstaller.StageAsync(Manifest(bytes), badPath), "archive rejects " + label);
            }
            await RejectAsync(() => installer.StageAsync(manifest with { Sha256 = new string('b', 64) }, archive), "cached archive is checked again");
            File.WriteAllText(Path.Combine(prepared.Directory, "extra-user-data.txt"), "leave me alone");
            await RejectAsync(() => installer.StageAsync(manifest, archive), "existing version with extra data is not overwritten");
            await RejectAsync(() => installer.ActivateAndLaunchAsync(prepared, () => Task.CompletedTask), "prepared file changes block activation");
            Check(File.ReadAllText(Path.Combine(prepared.Directory, "extra-user-data.txt")) == "leave me alone", "existing extra data preserved");
            File.Delete(Path.Combine(prepared.Directory, "extra-user-data.txt"));
            if (OperatingSystem.IsLinux())
            {
                string linkRoot = Path.Combine(root, "linked-apps"); Directory.CreateSymbolicLink(linkRoot, apps);
                var linked = new LinuxGuiUpdateInstaller(linkRoot, desktop, processes);
                await RejectAsync(() => linked.StageAsync(manifest, archive), "symlink application folder refused");
                File.Delete(icon); File.CreateSymbolicLink(icon, wallet);
                await RejectAsync(() => installer.ActivateAndLaunchAsync(prepared, () => Task.CompletedTask), "symlink icon refused");
                Check(File.ReadAllText(wallet) == "test-wallet-bytes", "symlink target not changed");
            }
            await HandshakeChecks(root);
            if (OperatingSystem.IsLinux() && Assembly.GetEntryAssembly()?.GetName().Name == "LinuxGuiUpdate.Tests")
                await RealProcessChecks(root);
            using (var service = Service(root, publicKey, _ => Response(package)))
            {
                Reject(() => service.RemoveCachedDownload(wallet), "cache cleanup cannot delete wallet files");
                service.RemoveCachedDownload(archive);
                Check(!File.Exists(archive), "verified cached download can be removed after staging");
            }
            Console.WriteLine($"PASS Linux GUI updater: {_checks} checks. No public services or user wallets were used.");
            return 0;
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static async Task RealProcessChecks(string root)
    {
        string fixture = Path.Combine(root, "real-process-fixture"); Directory.CreateDirectory(fixture);
        foreach (string source in Directory.EnumerateFiles(AppContext.BaseDirectory))
            File.Copy(source, Path.Combine(fixture, Path.GetFileName(source)));
        string executable = Path.Combine(fixture, "slithy-desktop");
        File.Copy(Path.Combine(fixture, "LinuxGuiUpdate.Tests"), executable);
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.SetUnixFileMode(Path.Combine(fixture, "LinuxGuiUpdate.Tests"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Directory.CreateDirectory(Path.Combine(fixture, "Binaries"));
        string native = Path.Combine(fixture, "Binaries/slithyd");
        File.WriteAllText(native, "#!/bin/sh\nexec \"${0%/*}/../LinuxGuiUpdate.Tests\" \"$@\"\n");
        File.SetUnixFileMode(native, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string? previousCache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        Environment.SetEnvironmentVariable("XDG_CACHE_HOME", Path.Combine(root, "real-process-cache"));
        var runner = new LinuxGuiUpdateProcesses();
        try
        {
            await runner.ValidateAsync(fixture, CancellationToken.None);
            Check(true, "actual candidate self-test process succeeds");
            Environment.SetEnvironmentVariable("SLITHY_GUI_UPDATE_FIXTURE_NATIVE_FAIL", "1");
            await RejectAsync(() => runner.ValidateAsync(fixture, CancellationToken.None), "bundled native compatibility failure blocks staging");
            Environment.SetEnvironmentVariable("SLITHY_GUI_UPDATE_FIXTURE_NATIVE_FAIL", null);
            Environment.SetEnvironmentVariable("SLITHY_GUI_UPDATE_FIXTURE_SELFTEST_FAIL", "1");
            await RejectAsync(() => runner.ValidateAsync(fixture, CancellationToken.None), "actual candidate self-test failure is refused");
            Environment.SetEnvironmentVariable("SLITHY_GUI_UPDATE_FIXTURE_SELFTEST_FAIL", null);
            await runner.LaunchAsync(fixture, Version, CancellationToken.None);
            string marker = Path.Combine(fixture, "fixture-started.txt");
            for (int i = 0; i < 100 && !File.Exists(marker); i++) await Task.Delay(50);
            Check(File.Exists(marker), "actual replacement process completes two-phase handoff");
            int pid = int.Parse(await File.ReadAllTextAsync(marker));
            using (Process child = Process.GetProcessById(pid))
            {
                Check(File.ReadAllText($"/proc/{pid}/cmdline").StartsWith(executable + '\0', StringComparison.Ordinal), "test child belongs to the isolated fixture");
                child.Kill(); await child.WaitForExitAsync();
            }
            Check(!Directory.EnumerateDirectories(LinuxGuiUpdatePaths.CacheDirectory, "handoff-*").Any(), "actual handoff folder removed");
            Environment.SetEnvironmentVariable("SLITHY_GUI_UPDATE_FIXTURE_EXIT", "1");
            await RejectAsync(() => runner.LaunchAsync(fixture, Version, CancellationToken.None), "replacement early exit is refused");
            Check(!Directory.EnumerateDirectories(LinuxGuiUpdatePaths.CacheDirectory, "handoff-*").Any(), "failed process handoff cleaned up");
            Environment.SetEnvironmentVariable("SLITHY_GUI_UPDATE_FIXTURE_EXIT", null);
            Environment.SetEnvironmentVariable("SLITHY_GUI_UPDATE_FIXTURE_IGNORE_TERM", "1");
            bool uncertain = false;
            using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
            {
                try { await runner.LaunchAsync(fixture, Version, cancel.Token); }
                catch (LinuxGuiUpdateLaunchUncertainException) { uncertain = true; }
            }
            Check(uncertain, "unconfirmed child shutdown has a distinct error type");
        }
        finally
        {
            string waiting = Path.Combine(fixture, "fixture-waiting.txt");
            if (File.Exists(waiting))
            {
                int pid = int.Parse(await File.ReadAllTextAsync(waiting));
                if (File.Exists($"/proc/{pid}/cmdline") && File.ReadAllText($"/proc/{pid}/cmdline").StartsWith(executable + '\0', StringComparison.Ordinal))
                {
                    using Process child = Process.GetProcessById(pid);
                    child.Kill(); await child.WaitForExitAsync();
                }
            }
            Environment.SetEnvironmentVariable("XDG_CACHE_HOME", previousCache);
            Environment.SetEnvironmentVariable("SLITHY_GUI_UPDATE_FIXTURE_SELFTEST_FAIL", null);
            Environment.SetEnvironmentVariable("SLITHY_GUI_UPDATE_FIXTURE_EXIT", null);
            Environment.SetEnvironmentVariable("SLITHY_GUI_UPDATE_FIXTURE_IGNORE_TERM", null);
            Environment.SetEnvironmentVariable("SLITHY_GUI_UPDATE_FIXTURE_NATIVE_FAIL", null);
        }
    }

    private static async Task HandshakeChecks(string root)
    {
        string? oldCache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        Environment.SetEnvironmentVariable("XDG_CACHE_HOME", Path.Combine(root, "handoff-cache"));
        try
        {
            string folder = Path.Combine(LinuxGuiUpdatePaths.CacheDirectory, "handoff-" + Guid.NewGuid().ToString("N"));
            LinuxGuiUpdatePaths.EnsureDirectory(folder);
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            string app = Path.Combine(root, "fixture-app"); Directory.CreateDirectory(app);
            File.WriteAllText(Path.Combine(folder, "request.json"), JsonSerializer.Serialize(new { Token = token, Directory = app, Version, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(20) }));
            Task ready = LinuxGuiUpdateProcesses.AcknowledgeAsync(folder, app, Version);
            for (int i = 0; i < 100 && !File.Exists(Path.Combine(folder, "ready.json")); i++) await Task.Delay(10);
            Check(File.Exists(Path.Combine(folder, "ready.json")) && !ready.IsCompleted, "replacement waits for ownership before node startup");
            File.WriteAllText(Path.Combine(folder, ".proceed"), JsonSerializer.Serialize(new { Token = token, ProcessId = Environment.ProcessId }));
            File.Move(Path.Combine(folder, ".proceed"), Path.Combine(folder, "proceed.json"));
            await ready;
            Check(!Directory.Exists(folder), "completed handoff metadata removed");
            await RejectAsync(() => LinuxGuiUpdateProcesses.AcknowledgeAsync(Path.Combine(root, "untrusted"), app, Version), "handoff outside private cache refused");
            folder = Path.Combine(LinuxGuiUpdatePaths.CacheDirectory, "handoff-" + Guid.NewGuid().ToString("N"));
            LinuxGuiUpdatePaths.EnsureDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "request.json"), JsonSerializer.Serialize(new { Token = token, Directory = app, Version, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) }));
            await RejectAsync(() => LinuxGuiUpdateProcesses.AcknowledgeAsync(folder, app, Version), "expired handoff refused");
            File.WriteAllText(Path.Combine(folder, "request.json"), JsonSerializer.Serialize(new { Token = token, Directory = app, Version, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(20) }));
            await RejectAsync(() => LinuxGuiUpdateProcesses.AcknowledgeAsync(folder, app, "0.1.43-beta.1"), "wrong-version handoff refused");
            await RejectAsync(() => LinuxGuiUpdateProcesses.AcknowledgeAsync(folder, root, Version), "wrong-directory handoff refused");
        }
        finally { Environment.SetEnvironmentVariable("XDG_CACHE_HOME", oldCache); }
    }

    private static LinuxGuiUpdateManifest Manifest(byte[] package) => new(Version,
        $"https://slithy.io/downloads/linux/Slithy-Tove-Linux-GUI-{Version}-x64.tar.gz",
        Convert.ToHexString(SHA256.HashData(package)), "Updater test fixture", DateTimeOffset.UtcNow.AddMinutes(-5),
        LinuxGuiUpdateService.Platform, LinuxGuiUpdateService.Network, LinuxGuiUpdateService.Genesis);

    private static byte[] Sign(ECDsa key, LinuxGuiUpdateManifest manifest) => SignBytes(key, JsonSerializer.SerializeToUtf8Bytes(manifest));
    private static byte[] SignBytes(ECDsa key, byte[] payload) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        payloadBase64 = Convert.ToBase64String(payload),
        signatureBase64 = Convert.ToBase64String(key.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
    });

    private static LinuxGuiUpdateService Service(string root, string key, Func<HttpRequestMessage, HttpResponseMessage> response) =>
        new(Path.Combine(root, "cache"), new Handler(response), key);
    private static HttpResponseMessage Response(byte[] bytes, long? declaredLength = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        if (declaredLength.HasValue) response.Content.Headers.ContentLength = declaredLength;
        return response;
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(response(request));
        }
    }
    private sealed class FakeProcesses : ILinuxGuiUpdateProcesses
    {
        internal int Validations;
        internal bool FailLaunch;
        internal Action? BeforeLaunch;
        public Task ValidateAsync(string directory, CancellationToken cancellationToken) { Validations++; return Task.CompletedTask; }
        public Task LaunchAsync(string directory, string version, CancellationToken cancellationToken)
        {
            BeforeLaunch?.Invoke(); if (FailLaunch) throw new IOException("fixture startup failed"); return Task.CompletedTask;
        }
    }

    private static byte[] Archive(string? extraName = null, TarEntryType type = TarEntryType.RegularFile, string? omit = null, bool empty = false)
    {
        var contents = new Dictionary<string, byte[]>
        {
            ["slithy-desktop"] = new byte[] { 0x7f, 69, 76, 70, 1 },
            ["Binaries/slithyd"] = new byte[] { 0x7f, 69, 76, 70, 2 },
            ["Assets/slithy-icon-256.png"] = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1 },
        };
        foreach (string file in new[] { "slithy-desktop.dll", "slithy-desktop.deps.json", "slithy-desktop.runtimeconfig.json", "install-desktop.sh", "README.md", "LICENSE", "NOTICE", "licenses/BETA-TERMS.txt" }) contents[file] = Encoding.UTF8.GetBytes("fixture bytes");
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        using (var tar = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: true))
        {
            if (!empty)
                foreach ((string name, byte[] bytes) in contents)
                {
                    if (name == omit) continue;
                    var entry = new PaxTarEntry(TarEntryType.RegularFile, Prefix + "/" + name) { DataStream = new MemoryStream(bytes) };
                    tar.WriteEntry(entry);
                }
            if (extraName != null)
            {
                var entry = new PaxTarEntry(type, extraName);
                if (type is TarEntryType.SymbolicLink or TarEntryType.HardLink) entry.LinkName = "/tmp/outside";
                if (type == TarEntryType.RegularFile) entry.DataStream = new MemoryStream(new byte[] { 1 });
                tar.WriteEntry(entry);
            }
        }
        return output.ToArray();
    }

    private static byte[] OversizedEntryArchive()
    {
        using var raw = new MemoryStream();
        using (var writer = new TarWriter(raw, TarEntryFormat.Ustar, leaveOpen: true))
            writer.WriteEntry(new UstarTarEntry(TarEntryType.RegularFile, Prefix + "/large.bin") { DataStream = new MemoryStream(new byte[] { 1 }) });
        byte[] tar = raw.ToArray();
        byte[] size = Encoding.ASCII.GetBytes(Convert.ToString(257L * 1024 * 1024, 8).PadLeft(11, '0') + "\0");
        Array.Copy(size, 0, tar, 124, size.Length);
        Array.Fill(tar, (byte)' ', 148, 8);
        int checksum = tar.Take(512).Sum(value => (int)value);
        byte[] field = Encoding.ASCII.GetBytes(Convert.ToString(checksum, 8).PadLeft(6, '0') + "\0 ");
        Array.Copy(field, 0, tar, 148, field.Length);
        using var result = new MemoryStream();
        using (var gzip = new GZipStream(result, CompressionLevel.Fastest, leaveOpen: true)) gzip.Write(tar);
        return result.ToArray();
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("Updater check failed: " + label);
        _checks++; Console.WriteLine("PASS: " + label);
    }
    private static void Reject(Action action, string label)
    {
        bool refused = false;
        try { action(); }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or CryptographicException or JsonException or FormatException or KeyNotFoundException or IOException) { refused = true; }
        Check(refused, label);
    }
    private static async Task RejectAsync(Func<Task> action, string label)
    {
        bool refused = false;
        try { await action(); }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or CryptographicException or JsonException or FormatException or KeyNotFoundException or IOException or OperationCanceledException or HttpRequestException) { refused = true; }
        Check(refused, label);
    }
}
