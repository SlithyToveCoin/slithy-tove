using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Slithy.Linux;

internal sealed record PreparedLinuxGuiUpdate(string Version, string Directory, string ArchiveSha256, string ContentSha256);

internal sealed class LinuxGuiUpdateLaunchUncertainException : IOException
{
    internal LinuxGuiUpdateLaunchUncertainException() : base("The replacement app did not confirm that it closed. Leave this app open and do not restart mining until that process has stopped.") { }
}

internal interface ILinuxGuiUpdateProcesses
{
    Task ValidateAsync(string directory, CancellationToken cancellationToken);
    Task LaunchAsync(string directory, string version, CancellationToken cancellationToken);
}

internal sealed class LinuxGuiUpdateInstaller
{
    private const long MaxExpandedBytes = 1024L * 1024 * 1024;
    private const int MaxFiles = 4096;
    private readonly string _applications;
    private readonly string _desktopData;
    private readonly ILinuxGuiUpdateProcesses _processes;
    private readonly Func<string, long> _availableBytes;
    private static readonly HashSet<string> Executables = new(StringComparer.Ordinal)
    {
        "slithy-desktop", "Binaries/slithyd", "install-desktop.sh", "createdump"
    };
    private static readonly string[] Required =
    {
        "slithy-desktop", "slithy-desktop.dll", "slithy-desktop.deps.json", "slithy-desktop.runtimeconfig.json",
        "Binaries/slithyd", "Assets/slithy-icon-256.png", "install-desktop.sh", "README.md", "LICENSE", "NOTICE", "licenses/BETA-TERMS.txt"
    };

    public LinuxGuiUpdateInstaller() : this(LinuxGuiUpdatePaths.ApplicationsDirectory,
        LinuxGuiUpdatePaths.DesktopDataDirectory, new LinuxGuiUpdateProcesses()) { }

    internal LinuxGuiUpdateInstaller(string applications, string desktopData, ILinuxGuiUpdateProcesses processes,
        Func<string, long>? availableBytes = null)
    {
        _applications = Path.GetFullPath(applications);
        _desktopData = Path.GetFullPath(desktopData);
        _processes = processes;
        _availableBytes = availableBytes ?? LinuxGuiUpdatePaths.AvailableBytes;
    }

    public async Task<PreparedLinuxGuiUpdate> StageAsync(LinuxGuiUpdateManifest manifest, string archivePath, CancellationToken cancellationToken = default)
    {
        LinuxGuiUpdateService.ValidateManifest(manifest);
        LinuxGuiUpdatePaths.RejectLinks(archivePath);
        if (new FileInfo(archivePath).Length > LinuxGuiUpdateService.MaxPackageBytes)
            throw new InvalidDataException("The update archive exceeds the size limit.");
        // Check again here so a changed cached file cannot reach extraction.
        await using (FileStream file = File.OpenRead(archivePath))
        {
            string hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken));
            if (!hash.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new CryptographicException("The cached update changed. Download it again.");
        }
        LinuxGuiUpdatePaths.EnsureDirectory(_applications);
        RequireSpace(_applications);
        string target = Path.Combine(_applications, manifest.Version);
        LinuxGuiUpdatePaths.RejectLinks(target);
        if (File.Exists(target)) throw new InvalidOperationException("A file occupies the new application's folder. It was not replaced.");
        string staging = Path.Combine(_applications, ".incoming-" + Guid.NewGuid().ToString("N"));
        LinuxGuiUpdatePaths.EnsureDirectory(staging);
        try
        {
            await ExtractAsync(archivePath, staging, manifest.Version, cancellationToken);
            await _processes.ValidateAsync(staging, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            LinuxGuiUpdatePaths.RejectLinks(target);
            if (Directory.Exists(target)) await CompareExistingAsync(staging, target, cancellationToken);
            else Directory.Move(staging, target);
            return new PreparedLinuxGuiUpdate(manifest.Version, target, manifest.Sha256, await HashTreeAsync(target, cancellationToken));
        }
        finally
        {
            // This is the random staging directory created by this call, never wallet data.
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private static async Task<string> HashTreeAsync(string directory, CancellationToken cancellationToken)
    {
        LinuxGuiUpdatePaths.RejectLinks(directory);
        var files = new List<string>();
        var pending = new Queue<string>(); pending.Enqueue(directory);
        while (pending.TryDequeue(out string? folder))
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(folder))
            {
                LinuxGuiUpdatePaths.RejectLinks(path);
                if (Directory.Exists(path)) pending.Enqueue(path);
                else files.Add(path);
            }
        }
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string file in files.OrderBy(path => Path.GetRelativePath(directory, path), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relative = Path.GetRelativePath(directory, file).Replace(Path.DirectorySeparatorChar, '/');
            hash.AppendData(Encoding.UTF8.GetBytes(relative + "\0"));
            await using FileStream stream = File.OpenRead(file);
            hash.AppendData(await SHA256.HashDataAsync(stream, cancellationToken));
            if (OperatingSystem.IsLinux()) hash.AppendData(Encoding.ASCII.GetBytes(((int)File.GetUnixFileMode(file)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "\0"));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static async Task CompareExistingAsync(string staged, string existing, CancellationToken cancellationToken)
    {
        var names = Directory.EnumerateFiles(staged, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(staged, path)).ToHashSet(StringComparer.Ordinal);
        var expectedEntries = Directory.EnumerateFileSystemEntries(staged, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(staged, path)).ToHashSet(StringComparer.Ordinal);
        var pending = new Queue<string>(); pending.Enqueue(existing);
        while (pending.TryDequeue(out string? folder))
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(folder))
            {
                LinuxGuiUpdatePaths.RejectLinks(path);
                if (!expectedEntries.Contains(Path.GetRelativePath(existing, path)))
                    throw new InvalidOperationException("The existing version folder contains different files. It was not replaced.");
                if (Directory.Exists(path)) pending.Enqueue(path);
            }
        }
        foreach (string name in names)
        {
            string path = Path.Combine(existing, name);
            LinuxGuiUpdatePaths.RejectLinks(path);
            if (!File.Exists(path)) throw new InvalidOperationException("The existing version folder is incomplete. It was not replaced.");
            await using FileStream first = File.OpenRead(Path.Combine(staged, name));
            await using FileStream second = File.OpenRead(path);
            if (!(await SHA256.HashDataAsync(first, cancellationToken)).SequenceEqual(await SHA256.HashDataAsync(second, cancellationToken)))
                throw new InvalidOperationException("The existing version folder contains changed files. It was not replaced.");
            if (OperatingSystem.IsLinux() && File.GetUnixFileMode(path) != File.GetUnixFileMode(Path.Combine(staged, name)))
                throw new InvalidOperationException("The existing version folder has different file permissions. It was not replaced.");
        }
    }

    public async Task ActivateAndLaunchAsync(PreparedLinuxGuiUpdate update, Func<Task> stopOwnedNode,
        CancellationToken cancellationToken = default)
    {
        _ = LinuxGuiVersion.Parse(update.Version);
        string directory = Path.GetFullPath(update.Directory);
        if (directory != Path.Combine(_applications, update.Version))
            throw new InvalidDataException("The prepared update is outside the application folder.");
        LinuxGuiUpdatePaths.RejectLinks(directory);
        if (await HashTreeAsync(directory, cancellationToken) != update.ContentSha256)
            throw new CryptographicException("The prepared update files changed. Download the update again before installing it.");
        foreach (string name in Required)
        {
            string file = Path.Combine(directory, name);
            LinuxGuiUpdatePaths.RejectLinks(file);
            if (!File.Exists(file)) throw new InvalidDataException("The prepared update is incomplete. Download it again.");
        }
        if (directory.IndexOfAny(new[] { '\n', '\r', '"', '`', '$', '%' }) >= 0 || (OperatingSystem.IsLinux() && directory.Contains('\\')))
            throw new InvalidOperationException("The application path contains characters that cannot be used in its desktop shortcut.");
        string applications = Path.Combine(_desktopData, "applications");
        string icons = Path.Combine(_desktopData, "icons", "hicolor", "256x256", "apps");
        LinuxGuiUpdatePaths.EnsureDirectory(applications);
        LinuxGuiUpdatePaths.EnsureDirectory(icons);
        string launcher = Path.Combine(applications, "io.slithy.Tove.desktop");
        string icon = Path.Combine(icons, "io.slithy.Tove.png");
        byte[]? previousLauncher = Snapshot(launcher, 64 * 1024);
        byte[]? previousIcon = Snapshot(icon, 1024 * 1024);
        if (previousLauncher != null)
        {
            string text = Encoding.UTF8.GetString(previousLauncher);
            if (!text.Split('\n').Any(line => line.TrimEnd('\r') == "Name=Slithy Tove") ||
                !text.Contains("/slithy-desktop\"", StringComparison.Ordinal))
                throw new InvalidOperationException("A different application owns the Slithy shortcut. It was not replaced.");
        }
        byte[] iconBytes = await File.ReadAllBytesAsync(Path.Combine(directory, "Assets/slithy-icon-256.png"), cancellationToken);
        string entry = "[Desktop Entry]\nType=Application\nName=Slithy Tove\nGenericName=Wallet and CPU miner\n" +
            "Comment=Linux wallet and CPU miner for the Slithy test network\n" +
            $"Exec=\"{directory}/slithy-desktop\"\nIcon=io.slithy.Tove\nStartupWMClass=io.slithy.Tove\nStartupNotify=true\nTerminal=false\nCategories=Office;Finance;\n";
        cancellationToken.ThrowIfCancellationRequested();
        // The caller stops polling and awaits its own node. It must not force-stop an unrelated process.
        await stopOwnedNode();
        bool wroteIcon = false, wroteLauncher = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteAtomic(icon, iconBytes); wroteIcon = true;
            WriteAtomic(launcher, Encoding.UTF8.GetBytes(entry)); wroteLauncher = true;
            await _processes.LaunchAsync(directory, update.Version, cancellationToken);
        }
        catch
        {
            if (wroteLauncher) Restore(launcher, previousLauncher);
            if (wroteIcon) Restore(icon, previousIcon);
            throw;
        }
    }

    private static byte[]? Snapshot(string path, int limit)
    {
        LinuxGuiUpdatePaths.RejectLinks(path);
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > limit) throw new InvalidDataException("The existing launcher or icon is too large to update safely.");
        return File.ReadAllBytes(path);
    }

    private static void WriteAtomic(string path, byte[] contents)
    {
        LinuxGuiUpdatePaths.RejectLinks(path);
        string temporary = Path.Combine(Path.GetDirectoryName(path)!, ".slithy-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (FileStream stream = LinuxGuiUpdatePaths.CreatePrivateFile(temporary))
            {
                stream.Write(contents);
                stream.Flush(true);
            }
            if (OperatingSystem.IsLinux()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            LinuxGuiUpdatePaths.RejectLinks(path);
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    private static void Restore(string path, byte[]? contents)
    {
        LinuxGuiUpdatePaths.RejectLinks(path);
        if (contents == null) File.Delete(path);
        else WriteAtomic(path, contents);
    }

    private void RequireSpace(string path, long pendingBytes = 0)
    {
        if (_availableBytes(path) < LinuxGuiUpdateService.CacheReserveBytes + pendingBytes)
            throw new IOException("The update needs more free space on the application drive, with 1 GiB left for your running node. Your current app and wallet files have not changed.");
    }

    private async Task ExtractAsync(string archivePath, string destination, string version, CancellationToken cancellationToken)
    {
        string prefix = "slithy-tove-linux-gui-" + version;
        var files = new HashSet<string>(StringComparer.Ordinal);
        var directories = new HashSet<string>(StringComparer.Ordinal);
        long fileBytes = 0;
        int count = 0;
        byte[] buffer = new byte[65536];
        await using FileStream file = File.OpenRead(archivePath);
        await using var gzip = new GZipStream(file, CompressionMode.Decompress);
        await using var bounded = new LimitedReadStream(gzip, MaxExpandedBytes);
        await using var tar = new TarReader(bounded, leaveOpen: true);
        TarEntry? entry;
        while ((entry = await tar.GetNextEntryAsync(copyData: false, cancellationToken)) != null)
        {
            if (++count > MaxFiles) throw new InvalidDataException("The update archive has too many entries.");
            string name = entry.Name;
            if (name.Length > 240 || name.Contains('\\') || name.StartsWith('/') || name.Contains('\0'))
                throw new InvalidDataException("The update contains an unsafe archive path.");
            string[] parts = name.TrimEnd('/').Split('/');
            if (parts.Length == 0 || parts[0] != prefix || parts.Any(p => p.Length == 0 || p is "." or ".."))
                throw new InvalidDataException("The update contains an unexpected archive path.");
            if (parts.Any(p => !Regex.IsMatch(p, "\\A[A-Za-z0-9._+-]+\\z", RegexOptions.CultureInvariant)))
                throw new InvalidDataException("The update contains an unsupported filename.");
            if (entry.EntryType == TarEntryType.Directory && parts.Length == 1) continue;
            if (parts.Length < 2 || parts.Length > 3 || (parts.Length == 3 && parts[1] is not ("Assets" or "Binaries" or "licenses")))
                throw new InvalidDataException("The update archive has an unexpected folder.");
            string relative = string.Join('/', parts.Skip(1));
            string target = Path.Combine(destination, Path.Combine(parts.Skip(1).ToArray()));
            LinuxGuiUpdatePaths.RejectLinks(target);
            if (entry.EntryType == TarEntryType.Directory)
            {
                if (parts.Length != 2 || parts[1] is not ("Assets" or "Binaries" or "licenses") || files.Contains(relative) || !directories.Add(relative))
                    throw new InvalidDataException("The update repeats a folder or file.");
                Directory.CreateDirectory(target);
                continue;
            }
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                throw new InvalidDataException("Links and special files are not allowed in a GUI update.");
            if (!files.Add(relative) || directories.Contains(relative)) throw new InvalidDataException("The update repeats a file.");
            string extension = Path.GetExtension(relative).ToLowerInvariant();
            if (extension is ".cookie" or ".wallet" or ".dat" or ".log" or ".conf" or ".key" or ".pem" or ".pfx" or ".sqlite" or ".db" ||
                relative is "settings.json" or "pending-payment.json" or "gui-instance.lock")
                throw new InvalidDataException("The update contains data that does not belong in an application package.");
            if (entry.Length <= 0 || entry.Length > 256L * 1024 * 1024 || entry.DataStream == null || (fileBytes += entry.Length) > MaxExpandedBytes)
                throw new InvalidDataException("The update has an invalid unpacked size.");
            RequireSpace(destination, entry.Length);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using (FileStream output = LinuxGuiUpdatePaths.CreatePrivateFile(target))
            {
                int read;
                while ((read = await entry.DataStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    if (output.Length + read > entry.Length) throw new InvalidDataException("The update archive has an invalid file size.");
                    RequireSpace(destination, entry.Length - output.Length);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                if (output.Length != entry.Length) throw new InvalidDataException("The update archive is incomplete.");
            }
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(target, Executables.Contains(relative)
                    ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute
                    : UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }
        // Read through the gzip trailer as well, so a truncated or damaged stream is refused.
        await bounded.CopyToAsync(Stream.Null, cancellationToken);
        if (Required.Any(name => !files.Contains(name))) throw new InvalidDataException("The GUI update is missing required files.");
        await RequireHeaderAsync(Path.Combine(destination, "slithy-desktop"), new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F' }, cancellationToken);
        await RequireHeaderAsync(Path.Combine(destination, "Binaries/slithyd"), new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F' }, cancellationToken);
        await RequireHeaderAsync(Path.Combine(destination, "Assets/slithy-icon-256.png"), new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, cancellationToken);
    }

    private static async Task RequireHeaderAsync(string path, byte[] expected, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] header = new byte[expected.Length];
        await stream.ReadExactlyAsync(header, cancellationToken);
        if (!header.SequenceEqual(expected)) throw new InvalidDataException("The GUI update contains an invalid program or icon.");
    }

    // Called after the replacement window is created, before wallet or terms dialogs open.
    internal static async Task AcknowledgeStartupAsync(string[] args)
    {
        int index = Array.IndexOf(args, "--update-handoff");
        if (index < 0) return;
        if (index + 1 >= args.Length) throw new InvalidDataException("The update handoff is missing.");
        string runningVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
            ?? throw new InvalidDataException("This application is missing its release version.");
        await LinuxGuiUpdateProcesses.AcknowledgeAsync(args[index + 1], AppContext.BaseDirectory, runningVersion);
    }

    private sealed class LimitedReadStream(Stream inner, long maximum) : Stream
    {
        private long _read;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _read; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int result = inner.Read(buffer, offset, count); Count(result); return result;
        }
        public override int Read(Span<byte> buffer) { int result = inner.Read(buffer); Count(result); return result; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int result = await inner.ReadAsync(buffer, cancellationToken); Count(result); return result;
        }
        private void Count(int bytes) { if ((_read += bytes) > maximum) throw new InvalidDataException("The expanded update exceeds the size limit."); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

internal sealed class LinuxGuiUpdateProcesses : ILinuxGuiUpdateProcesses
{
    private sealed record Handoff(string Token, string Directory, string Version, DateTimeOffset ExpiresAt);
    private sealed record Ready(string Token, int ProcessId);

    public async Task ValidateAsync(string directory, CancellationToken cancellationToken)
    {
        await ValidateNativeAsync(directory, cancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        using Process process = Process.Start(new ProcessStartInfo(Path.Combine(directory, "slithy-desktop"))
        {
            UseShellExecute = false, WorkingDirectory = directory,
            ArgumentList = { "--self-test" }, RedirectStandardOutput = true, RedirectStandardError = true
        }) ?? throw new IOException("The new Linux app could not be tested.");
        Task stdout = DrainAsync(process.StandardOutput, deadline.Token);
        Task stderr = DrainAsync(process.StandardError, deadline.Token);
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            await Task.WhenAll(stdout, stderr);
            if (process.ExitCode != 0) throw new InvalidOperationException("The new Linux app did not pass its startup check. Your current installation has not changed.");
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task ValidateNativeAsync(string directory, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        using Process process = Process.Start(new ProcessStartInfo(Path.Combine(directory, "Binaries", "slithyd"))
        {
            UseShellExecute = false, WorkingDirectory = directory, ArgumentList = { "-version" },
            RedirectStandardOutput = true, RedirectStandardError = true
        }) ?? throw new IOException("The bundled node could not be checked on this computer.");
        Task<string> stdout = ReadBoundedAsync(process.StandardOutput, deadline.Token);
        Task<string> stderr = ReadBoundedAsync(process.StandardError, deadline.Token);
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            string output = await stdout;
            _ = await stderr;
            if (process.ExitCode != 0 || !output.Contains("Slithy Tove Core", StringComparison.Ordinal))
                throw new InvalidOperationException("The new bundled node cannot run on this computer. Its architecture or required libraries may not match. Your current app has not been changed.");
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        char[] buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            if (text.Length + read > 65536) throw new InvalidDataException("The bundled node's startup check produced too much output.");
            text.Append(buffer, 0, read);
        }
        return text.ToString();
    }

    public async Task LaunchAsync(string directory, string version, CancellationToken cancellationToken)
    {
        LinuxGuiUpdatePaths.EnsureDirectory(LinuxGuiUpdatePaths.CacheDirectory);
        string handoffDirectory = Path.Combine(LinuxGuiUpdatePaths.CacheDirectory, "handoff-" + Guid.NewGuid().ToString("N"));
        LinuxGuiUpdatePaths.EnsureDirectory(handoffDirectory);
        var request = new Handoff(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), Path.GetFullPath(directory), version, DateTimeOffset.UtcNow.AddSeconds(30));
        using (FileStream stream = LinuxGuiUpdatePaths.CreatePrivateFile(Path.Combine(handoffDirectory, "request.json"))) JsonSerializer.Serialize(stream, request);
        Process? process = null;
        bool started = false;
        try
        {
            process = Process.Start(new ProcessStartInfo(Path.Combine(directory, "slithy-desktop"))
            {
                UseShellExecute = false, WorkingDirectory = directory,
                ArgumentList = { "--update-handoff", handoffDirectory }
            }) ?? throw new IOException("The new Linux app could not be opened.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(20));
            string responsePath = Path.Combine(handoffDirectory, "ready.json");
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (process.HasExited) throw new IOException("The new Linux app closed before its window was ready.");
                if (File.Exists(responsePath))
                {
                    LinuxGuiUpdatePaths.RejectLinks(responsePath);
                    if (new FileInfo(responsePath).Length > 4096) throw new InvalidDataException("The update handoff response is invalid.");
                    Ready ready = JsonSerializer.Deserialize<Ready>(await File.ReadAllTextAsync(responsePath, deadline.Token))
                        ?? throw new InvalidDataException("The update handoff response is empty.");
                    if (ready.Token != request.Token || ready.ProcessId != process.Id) throw new InvalidDataException("The replacement app did not confirm its identity.");
                    // The child waits here before opening a node. After this write it owns startup.
                    string proceed = Path.Combine(handoffDirectory, "proceed.json");
                    string proceedTemporary = Path.Combine(handoffDirectory, ".proceed");
                    using (FileStream stream = LinuxGuiUpdatePaths.CreatePrivateFile(proceedTemporary))
                    {
                        JsonSerializer.Serialize(stream, ready);
                        stream.Flush(true);
                    }
                    File.Move(proceedTemporary, proceed);
                    started = true;
                    return;
                }
                await Task.Delay(100, deadline.Token);
            }
        }
        finally
        {
            if (!started && process is { HasExited: false })
            {
                // This child has not acknowledged its window, so no wallet/node startup is allowed yet.
                try
                {
                    using Process? signal = Process.Start(new ProcessStartInfo("/bin/kill") { UseShellExecute = false, ArgumentList = { "-TERM", process.Id.ToString() } });
                    if (signal != null) await signal.WaitForExitAsync();
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch
                {
                    if (!process.HasExited)
                    {
                        process.Dispose();
                        throw new LinuxGuiUpdateLaunchUncertainException();
                    }
                }
            }
            process?.Dispose();
            if (!started && Directory.Exists(handoffDirectory)) Directory.Delete(handoffDirectory, recursive: true);
        }
    }

    internal static async Task AcknowledgeAsync(string directory, string executableDirectory, string runningVersion)
    {
        string full = Path.GetFullPath(directory);
        if (Path.GetDirectoryName(full) != Path.GetFullPath(LinuxGuiUpdatePaths.CacheDirectory) ||
            !Regex.IsMatch(Path.GetFileName(full), "\\Ahandoff-[0-9a-f]{32}\\z", RegexOptions.CultureInvariant))
            throw new InvalidDataException("The update handoff folder is invalid.");
        LinuxGuiUpdatePaths.RejectLinks(full);
        string requestPath = Path.Combine(full, "request.json");
        LinuxGuiUpdatePaths.RejectLinks(requestPath);
        if (new FileInfo(requestPath).Length > 8192) throw new InvalidDataException("The update handoff is too large.");
        Handoff request = JsonSerializer.Deserialize<Handoff>(File.ReadAllText(requestPath)) ?? throw new InvalidDataException("The update handoff is empty.");
        if (!Regex.IsMatch(request.Token ?? "", "\\A[0-9A-F]{64}\\z", RegexOptions.CultureInvariant) ||
            Path.GetFullPath(request.Directory).TrimEnd(Path.DirectorySeparatorChar) != Path.GetFullPath(executableDirectory).TrimEnd(Path.DirectorySeparatorChar) ||
            request.Version != runningVersion || request.ExpiresAt < DateTimeOffset.UtcNow || request.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            throw new InvalidDataException("The update handoff has expired or belongs to a different application.");
        _ = LinuxGuiVersion.Parse(request.Version);
        string temporary = Path.Combine(full, ".ready-" + Guid.NewGuid().ToString("N"));
        using (FileStream stream = LinuxGuiUpdatePaths.CreatePrivateFile(temporary))
        {
            JsonSerializer.Serialize(stream, new Ready(request.Token!, Environment.ProcessId));
            stream.Flush(true);
        }
        File.Move(temporary, Path.Combine(full, "ready.json"));
        string proceed = Path.Combine(full, "proceed.json");
        while (!File.Exists(proceed))
        {
            if (DateTimeOffset.UtcNow > request.ExpiresAt)
                throw new IOException("The previous app did not finish the update handoff. This app has not opened a node.");
            await Task.Delay(50);
        }
        LinuxGuiUpdatePaths.RejectLinks(proceed);
        if (new FileInfo(proceed).Length > 4096) throw new InvalidDataException("The update handoff is invalid.");
        Ready confirmation = JsonSerializer.Deserialize<Ready>(await File.ReadAllTextAsync(proceed)) ?? throw new InvalidDataException("The update handoff is empty.");
        if (confirmation.Token != request.Token || confirmation.ProcessId != Environment.ProcessId)
            throw new InvalidDataException("The previous app did not confirm this update.");
        Directory.Delete(full, recursive: true);
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        char[] buffer = new char[4096];
        while (await reader.ReadAsync(buffer.AsMemory(), cancellationToken) > 0) { }
    }
}
