using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Slithy.Linux;

internal sealed record DesktopInstallFile(string Path, string Sha256, bool Executable);
internal sealed record DesktopInstallPlan(string Version, string Source, string Target, long Bytes, DesktopInstallFile[] Files);

// The package inventory limits installation to release files, never a nearby wallet or log.
internal sealed class DesktopInstallService
{
    private const string DesktopId = "io.slithy.Tove";
    [DllImport("libc")] private static extern uint geteuid();
    internal static bool IsRoot => OperatingSystem.IsLinux() && geteuid() == 0;
    private readonly string _apps;
    private readonly string _desktopData;
    internal DesktopInstallService(string? apps = null, string? desktopData = null)
    {
        _apps = Path.GetFullPath(apps ?? LinuxGuiUpdatePaths.ApplicationsDirectory);
        _desktopData = Path.GetFullPath(desktopData ?? LinuxGuiUpdatePaths.DesktopDataDirectory);
    }

    internal async Task<DesktopInstallPlan> InspectAsync(string source, CancellationToken token = default)
    {
        source = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);
        LinuxGuiUpdatePaths.RejectLinks(source);
        string inventory = Path.Combine(source, "install-files.json");
        LinuxGuiUpdatePaths.RejectLinks(inventory);
        if (!File.Exists(inventory) || new FileInfo(inventory).Length > 1024 * 1024)
            throw new IOException("The installation inventory is missing or too large. Extract the complete Slithy package again.");
        var files = JsonSerializer.Deserialize<DesktopInstallFile[]>(await File.ReadAllTextAsync(inventory, token))
            ?? throw new IOException("The installation inventory is empty.");
        if (files.Length is < 5 or > 4096 || files.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count() != files.Length)
            throw new IOException("The installation inventory contains invalid entries.");
        long bytes = 0;
        foreach (DesktopInstallFile file in files)
        {
            token.ThrowIfCancellationRequested();
            ValidateRelative(file.Path);
            if (!Regex.IsMatch(file.Sha256 ?? "", "^[a-fA-F0-9]{64}$")) throw new IOException("An installation checksum is invalid.");
            string path = Path.Combine(source, file.Path);
            LinuxGuiUpdatePaths.RejectLinks(path);
            var info = new FileInfo(path);
            if (!info.Exists || (info.Attributes & FileAttributes.Directory) != 0) throw new IOException("The extracted package is incomplete.");
            bytes = checked(bytes + info.Length);
            if (bytes > 1024L * 1024 * 1024) throw new IOException("The extracted package exceeds the installation size limit.");
            await using FileStream stream = File.OpenRead(path);
            if (!Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException("A package file changed. Extract a fresh copy before installing.");
        }
        foreach (string required in new[] { "slithy-desktop", "slithy-desktop.dll", "Binaries/slithyd", "Assets/slithy-icon-256.png", "LICENSE", "NOTICE" })
            if (!files.Any(f => f.Path == required)) throw new IOException("A required application file is missing from the inventory.");
        string version = FileVersionInfo.GetVersionInfo(Path.Combine(source, "slithy-desktop.dll")).ProductVersion?.Split('+')[0] ?? "";
        _ = LinuxGuiVersion.Parse(version);
        string target = Path.Combine(_apps, version);
        ValidateShortcutPath(target);
        return new DesktopInstallPlan(version, source, target, bytes, files);
    }

    private static void ValidateRelative(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') ||
            path.Split('/').Any(p => p is "" or "." or "..") || path.Any(char.IsControl) || path == "install-files.json")
            throw new IOException("The package contains an unsafe installation path.");
    }

    private static void ValidateShortcutPath(string path)
    {
        if (path.IndexOfAny(new[] { '\n', '\r', '"', '`', '$', '%', '\\' }) >= 0)
            throw new IOException("The installation path contains characters that cannot be used in an application shortcut.");
    }

    internal static async Task CheckNativeDependenciesAsync(string source, CancellationToken token = default)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(Path.Combine(source, "Binaries/slithyd"))
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            ArgumentList = { "-version" }
        } };
        try
        {
            if (!process.Start()) throw new IOException("The bundled node could not be checked.");
            Task<string> output = process.StandardOutput.ReadToEndAsync(token);
            Task<string> error = process.StandardError.ReadToEndAsync(token);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { if (!process.HasExited) process.Kill(); throw; }
            string diagnostic = await error;
            if (process.ExitCode != 0 || !(await output).Contains("Slithy", StringComparison.OrdinalIgnoreCase))
                throw new IOException(LinuxNodeHost.StartupDiagnostic(diagnostic) ?? "The bundled node could not run. Check the supported Linux versions before installing.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new IOException("Linux could not start the bundled node. Check that the package is executable and that this is an x64 computer.");
        }
    }

    internal void RequireOtherAppsClosed()
    {
        if (!OperatingSystem.IsLinux()) return;
        foreach (string folder in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(folder), out int pid) || pid == Environment.ProcessId) continue;
            try
            {
                string target = new FileInfo(Path.Combine(folder, "exe")).LinkTarget ?? "";
                if (target.StartsWith(_apps.TrimEnd('/') + "/", StringComparison.Ordinal) &&
                    Path.GetFileName(target) is "slithy-desktop" or "slithyd" or "slithy-install" or "slithy-uninstall")
                    throw new InvalidOperationException("Close the other Linux Slithy app before changing its installation. It has not been stopped.");
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
    }

    internal async Task<string> InstallAsync(DesktopInstallPlan plan, CancellationToken token = default)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Install this package on Linux.");
        if (IsRoot) throw new InvalidOperationException("Install Slithy from your normal Linux account, without sudo.");
        RequireOtherAppsClosed();
        DesktopInstallPlan checkedPlan = await InspectAsync(plan.Source, token);
        if (checkedPlan.Target != plan.Target) throw new IOException("The installation plan changed.");
        LinuxGuiUpdatePaths.EnsureDirectory(_apps);
        if (LinuxGuiUpdatePaths.AvailableBytes(_apps) < plan.Bytes + 64L * 1024 * 1024)
            throw new IOException("There is not enough free space for Slithy. Free some space and try again.");
        LinuxGuiUpdatePaths.RejectLinks(plan.Target);
        string staging = Path.Combine(_apps, ".install-" + Guid.NewGuid().ToString("N"));
        LinuxGuiUpdatePaths.EnsureDirectory(staging);
        bool moved = false;
        try
        {
            foreach (DesktopInstallFile file in checkedPlan.Files)
            {
                token.ThrowIfCancellationRequested();
                string output = Path.Combine(staging, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.Copy(Path.Combine(plan.Source, file.Path), output);
                File.SetUnixFileMode(output, file.Executable
                    ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute
                    : UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            }
            File.Copy(Path.Combine(plan.Source, "install-files.json"), Path.Combine(staging, "install-files.json"));
            await InspectAsync(staging, token);
            token.ThrowIfCancellationRequested();
            if (Directory.Exists(plan.Target))
            {
                DesktopInstallPlan existing = await InspectAsync(plan.Target, token);
                if (!existing.Files.SequenceEqual(checkedPlan.Files)) throw new IOException("This version's installed files are different. They were not replaced.");
            }
            else { Directory.Move(staging, plan.Target); moved = true; }
            await RegisterAsync(plan.Target, token);
            return plan.Target;
        }
        catch
        {
            if (moved && Directory.Exists(plan.Target)) Directory.Delete(plan.Target, recursive: true);
            throw;
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); }
    }

    private async Task RegisterAsync(string target, CancellationToken token)
    {
        string folder = Path.Combine(_desktopData, "applications");
        string icons = Path.Combine(_desktopData, "icons/hicolor/256x256/apps");
        LinuxGuiUpdatePaths.EnsureDirectory(folder); LinuxGuiUpdatePaths.EnsureDirectory(icons);
        string launcher = Path.Combine(folder, DesktopId + ".desktop");
        string icon = Path.Combine(icons, DesktopId + ".png");
        LinuxGuiUpdatePaths.RejectLinks(launcher); LinuxGuiUpdatePaths.RejectLinks(icon);
        byte[]? oldLauncher = Snapshot(launcher), oldIcon = Snapshot(icon);
        if (oldLauncher != null && !Encoding.UTF8.GetString(oldLauncher).Contains("\nName=Slithy Tove\n", StringComparison.Ordinal))
            throw new IOException("Another application owns the Slithy shortcut. It was not replaced.");
        string entry = LinuxGuiUpdatePaths.DesktopEntry(target);
        try
        {
            await AtomicWriteAsync(icon, await File.ReadAllBytesAsync(Path.Combine(target, "Assets/slithy-icon-256.png"), token), token);
            await AtomicWriteAsync(launcher, Encoding.UTF8.GetBytes(entry), token);
        }
        catch
        {
            if (oldIcon == null) File.Delete(icon); else await AtomicWriteAsync(icon, oldIcon, CancellationToken.None);
            if (oldLauncher == null) File.Delete(launcher); else await AtomicWriteAsync(launcher, oldLauncher, CancellationToken.None);
            throw;
        }
    }

    private static byte[]? Snapshot(string path)
    {
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 1024 * 1024) throw new IOException("An existing desktop file is too large.");
        return File.ReadAllBytes(path);
    }

    private static async Task AtomicWriteAsync(string path, byte[] data, CancellationToken token)
    {
        LinuxGuiUpdatePaths.RejectLinks(path);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, data, token);
            if (OperatingSystem.IsLinux()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    internal async Task UninstallAsync(string source, CancellationToken token = default)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Remove this package on Linux.");
        if (IsRoot) throw new InvalidOperationException("Remove Slithy from your normal Linux account, without sudo.");
        RequireOtherAppsClosed();
        DesktopInstallPlan plan = await InspectAsync(source, token);
        if (plan.Source != plan.Target) throw new IOException("Run Uninstall from the installed application's menu entry. Extracted download folders are not removed.");
        string launcher = Path.Combine(_desktopData, "applications", DesktopId + ".desktop");
        string icon = Path.Combine(_desktopData, "icons/hicolor/256x256/apps", DesktopId + ".png");
        LinuxGuiUpdatePaths.RejectLinks(launcher); LinuxGuiUpdatePaths.RejectLinks(icon);
        bool ownsLauncher = File.Exists(launcher) && (await File.ReadAllTextAsync(launcher, token)).Contains($"Exec=\"{plan.Target}/slithy-desktop\"\n", StringComparison.Ordinal);
        if (!ownsLauncher) throw new IOException("The current shortcut points elsewhere. No files were removed.");
        token.ThrowIfCancellationRequested();
        // Remove inventoried program files. Extra files, settings and wallets are left alone.
        foreach (DesktopInstallFile file in plan.Files) File.Delete(Path.Combine(plan.Target, file.Path));
        File.Delete(Path.Combine(plan.Target, "install-files.json"));
        foreach (string directory in Directory.EnumerateDirectories(plan.Target, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }).OrderByDescending(p => p.Length))
            if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
        if (!Directory.EnumerateFileSystemEntries(plan.Target).Any()) Directory.Delete(plan.Target);
        File.Delete(launcher);
        if (File.Exists(icon)) File.Delete(icon);
    }
}
