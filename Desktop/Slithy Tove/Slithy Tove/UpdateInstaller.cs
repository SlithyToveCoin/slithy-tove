//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Update Installer
//===============================================
using System.Diagnostics;
using System.Text.Json;
using System.IO.Compression;

namespace Slithy_Tove;

// This file records enough information to either finish an update or roll it back.
internal sealed record PendingUpdate(
    string PreviousVersion,
    string TargetVersion,
    string InstallDirectory,
    string BackupDirectory,
    string InstalledExecutable,
    string HealthToken,
    DateTimeOffset StartedAt,
    string PackageSha256 = "");

// Applies update packages after the main app exits.
// It also saves a backup so a broken update can be rolled back automatically.
internal static class UpdateInstaller
{
    private const string PendingFileName = "pending-update.json";
    private const string HealthyFileName = "healthy-update.txt";

    internal static string CreateHelperCopy(string sourceDirectory, string helperDirectory, string executableName)
    {
        string source = Path.GetFullPath(sourceDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string destination = Path.GetFullPath(helperDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (destination.StartsWith(source, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The update helper must be outside the application directory.");
        if (executableName != Path.GetFileName(executableName))
            throw new IOException("Invalid update executable name.");
        if (Directory.Exists(destination))
            throw new IOException("The update helper directory already exists.");
        CopyRuntime(new DirectoryInfo(source), destination, true);
        return Path.Combine(destination, executableName);
    }

    private static void CopyRuntime(DirectoryInfo source, string destination, bool topLevel)
    {
        if ((source.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("The update runtime cannot contain directory links.");
        Directory.CreateDirectory(destination);
        foreach (FileSystemInfo entry in source.EnumerateFileSystemInfos())
        {
            if (topLevel && entry is DirectoryInfo && entry.Name.Equals("Binaries", StringComparison.OrdinalIgnoreCase)) continue;
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("The update runtime cannot contain file links.");
            string target = Path.Combine(destination, entry.Name);
            if (entry is DirectoryInfo directory) CopyRuntime(directory, target, false);
            else File.Copy(entry.FullName, target);
        }
    }

    internal static bool RunHelperSelfTest()
    {
        string root = Path.Combine(Path.GetTempPath(), "slithy-helper-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            string executable = CreateHelperCopy(AppContext.BaseDirectory, root, "Slithy Tove.exe");
            ProcessStartInfo start = new(executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = root };
            start.ArgumentList.Add("--self-test-treasury");
            using Process child = Process.Start(start) ?? throw new IOException("The copied helper did not start.");
            if (!child.WaitForExit(15_000)) { child.Kill(entireProcessTree: true); child.WaitForExit(); return false; }
            return child.ExitCode == 0 && !Directory.Exists(Path.Combine(root, "Binaries"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    public static string CreatePendingUpdate(
        string updateDirectory,
        string previousVersion,
        string targetVersion,
        string installedExecutable,
        string packageSha256 = "")
    {
        // Save the update plan before closing the app, so the helper knows what to replace.
        string installDirectory = Path.GetDirectoryName(installedExecutable)
            ?? throw new InvalidOperationException("The application directory is unavailable.");
        string applicationDataDirectory = Path.GetDirectoryName(updateDirectory)
            ?? throw new InvalidOperationException("The application data directory is unavailable.");
        string backupDirectory = Path.Combine(
            applicationDataDirectory,
            "rollback",
            $"{previousVersion}-before-{targetVersion}-{Guid.NewGuid():N}");
        PendingUpdate pending = new(
            previousVersion,
            targetVersion,
            installDirectory,
            backupDirectory,
            installedExecutable,
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow,
            packageSha256);
        Directory.CreateDirectory(updateDirectory);
        string pendingPath = Path.Combine(updateDirectory, PendingFileName);
        File.WriteAllText(
            pendingPath,
            JsonSerializer.Serialize(pending, new JsonSerializerOptions { WriteIndented = true }));
        File.Delete(Path.Combine(updateDirectory, HealthyFileName));
        File.Delete(pendingPath + ".ready");
        return pendingPath;
    }

    public static void MarkCurrentVersionHealthy(string updateDirectory, Version currentVersion)
    {
        // The updated app writes a health marker after it starts successfully.
        PendingUpdate? pending = ReadPending(updateDirectory);
        if (pending is null ||
            currentVersion < Version.Parse(pending.TargetVersion))
        {
            return;
        }

        // Close the completed marker before exposing it to the waiting updater.
        // Older releases read this path without retrying sharing violations.
        string healthyPath = Path.Combine(updateDirectory, HealthyFileName);
        string temporary = healthyPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, pending.HealthToken);
            File.Move(temporary, healthyPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static int Apply(
        string packagePath,
        string pendingPath,
        int parentProcessId)
    {
        // This runs in the replacement app process. It waits for the old process,
        // backs up the old files, applies the update, then checks the new app starts.
        PendingUpdate pending = JsonSerializer.Deserialize<PendingUpdate>(
            File.ReadAllText(pendingPath))
            ?? throw new InvalidDataException("The pending update state is invalid.");
        string logPath = Path.Combine(
            Path.GetDirectoryName(pendingPath)!,
            "update-install.log");

        using (FileStream package = File.OpenRead(packagePath))
        {
            string actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(package));
            if (!actual.Equals(pending.PackageSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The downloaded package changed before installation. No application files were replaced.");
        }

        AppendLog(logPath, $"Update helper started for {pending.PreviousVersion} to {pending.TargetVersion}.");
        File.WriteAllText(pendingPath + ".ready", pending.HealthToken);
        WaitForParentExit(parentProcessId);
        CreateBackup(pending.InstallDirectory, pending.BackupDirectory);
        AppendLog(logPath, $"Backup created at {pending.BackupDirectory}.");

        Process? updated = null;
        int installExitCode = 2;
        try
        {
            installExitCode = ApplyPackage(packagePath, pending, logPath);
            if (installExitCode != 0)
                throw new InvalidOperationException($"Installer exited with code {installExitCode}.");
            updated = StartApplication(pending.InstalledExecutable, $"--updated-from={pending.PreviousVersion}");
            if (!WaitForHealthyMarker(Path.GetDirectoryName(pendingPath)!, pending.HealthToken, updated))
                throw new InvalidOperationException("The replacement application did not report a healthy startup.");
            AppendLog(logPath, "Updated application reported a healthy startup.");
            File.Delete(pendingPath);
            return 0;
        }
        catch (Exception ex)
        {
            AppendLog(logPath, $"Update failed: {ex.Message}");
            // Confirm the failed process has exited before moving its directory.
            if (updated is not null) TryTerminate(updated);
            RestoreBackup(pending);
            File.Delete(pendingPath);
            AppendLog(logPath, "Previous application files restored.");
            StartApplication(pending.InstalledExecutable, "--update-rolled-back");
            return installExitCode == 0 ? 2 : installExitCode;
        }
        finally { updated?.Dispose(); }
    }

    internal static PendingUpdate? ReadPending(string updateDirectory)
    {
        // Read pending-update.json if it exists. Bad files are ignored.
        string path = Path.Combine(updateDirectory, PendingFileName);
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    internal static bool RunRollbackSelfTest()
    {
        // Creates fake app files in temp storage and proves backup, install, and rollback work.
        string root = Path.Combine(
            Path.GetTempPath(),
            $"slithy-update-rollback-test-{Guid.NewGuid():N}");
        try
        {
            string installDirectory = Path.Combine(root, "installed");
            string updateDirectory = Path.Combine(root, "appdata", "updates");
            Directory.CreateDirectory(installDirectory);
            string executable = Path.Combine(installDirectory, "Slithy Tove.exe");
            File.WriteAllText(executable, "previous-version");
            string pendingPath = CreatePendingUpdate(
                updateDirectory,
                "0.1.1",
                "0.1.2",
                executable);
            PendingUpdate pending = ReadPending(updateDirectory)
                ?? throw new InvalidDataException("Self-test pending state was not readable.");
            CreateBackup(pending.InstallDirectory, pending.BackupDirectory);
            File.WriteAllText(executable, "failed-new-version");
            File.WriteAllText(Path.Combine(installDirectory, "introduced.dll"), "new-file");
            RestoreBackup(pending);
            bool rollbackPassed = File.ReadAllText(executable) == "previous-version" &&
                                  !File.Exists(Path.Combine(installDirectory, "introduced.dll")) &&
                                  File.Exists(pendingPath) &&
                                  Directory.Exists(pending.BackupDirectory);

            string packageDirectory = Path.Combine(root, "package");
            Directory.CreateDirectory(packageDirectory);
            File.WriteAllText(Path.Combine(packageDirectory, "Slithy Tove.exe"), "portable-new-version");
            Directory.CreateDirectory(Path.Combine(packageDirectory, "Binaries"));
            File.WriteAllText(Path.Combine(packageDirectory, "Binaries", "slithyd.exe"), "node-binary");
            string packagePath = Path.Combine(root, "portable.zip");
            ZipFile.CreateFromDirectory(packageDirectory, packagePath);
            int packageResult = ApplyPackage(packagePath, pending, Path.Combine(root, "update.log"));
            bool packagePassed = packageResult == 0 &&
                                 File.ReadAllText(executable) == "portable-new-version" &&
                                 File.Exists(Path.Combine(installDirectory, "Binaries", "slithyd.exe"));

            return rollbackPassed && packagePassed;
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

    private static void WaitForParentExit(int parentProcessId)
    {
        // The helper cannot overwrite the old exe until the old process has exited.
        try
        {
            using Process parent = Process.GetProcessById(parentProcessId);
            if (!parent.WaitForExit(30_000))
                throw new TimeoutException("Slithy is still running. No application files were replaced.");
        }
        catch (ArgumentException)
        {
            // The parent already exited.
        }
    }

    private static void CreateBackup(string sourceDirectory, string backupDirectory)
    {
        // Copy the whole app folder so rollback can restore it.
        if (Directory.Exists(backupDirectory))
        {
            throw new IOException("The update backup already exists. It will not be overwritten.");
        }
        CopyDirectory(sourceDirectory, backupDirectory);
    }

    private static void RestoreBackup(PendingUpdate pending)
    {
        if (!Directory.Exists(pending.BackupDirectory))
        {
            throw new DirectoryNotFoundException("The previous installation backup is missing.");
        }
        // Restore a complete snapshot. An overlay would leave DLLs introduced by
        // the failed release where Windows could still load them.
        string staged = pending.InstallDirectory + ".restore-" + Guid.NewGuid().ToString("N");
        string failed = pending.InstallDirectory + ".failed-" + Guid.NewGuid().ToString("N");
        CopyDirectory(pending.BackupDirectory, staged);
        bool moved = false;
        try
        {
            if (Directory.Exists(pending.InstallDirectory))
            {
                Directory.Move(pending.InstallDirectory, failed);
                moved = true;
            }
            Directory.Move(staged, pending.InstallDirectory);
        }
        catch
        {
            if (moved && !Directory.Exists(pending.InstallDirectory))
                Directory.Move(failed, pending.InstallDirectory);
            throw;
        }
    }

    private static int ApplyPackage(string packagePath, PendingUpdate pending, string logPath)
    {
        // ZIP updates are portable folder updates. EXE updates are installer updates.
        string extension = Path.GetExtension(packagePath);
        if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            string extractionRoot = Path.Combine(
                Path.GetDirectoryName(packagePath)!,
                $"extracted-{pending.TargetVersion}-{Guid.NewGuid():N}");
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(packagePath))
                {
                    long expanded = 0;
                    if (archive.Entries.Count > 10000) throw new InvalidDataException("The update contains too many files.");
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        expanded = checked(expanded + entry.Length);
                        if (expanded > 1L * 1024 * 1024 * 1024)
                            throw new InvalidDataException("The extracted update exceeds the size limit.");
                        if (((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000)
                            throw new InvalidDataException("Update packages cannot contain symbolic links.");
                    }
                    archive.ExtractToDirectory(extractionRoot);
                }
                string sourceDirectory = FindPortablePackageRoot(extractionRoot);
                CopyDirectory(sourceDirectory, pending.InstallDirectory);
                AppendLog(logPath, $"Portable ZIP update copied from {sourceDirectory}.");
                Directory.Delete(extractionRoot, recursive: true);
                return 0;
            }
            catch (Exception ex)
            {
                AppendLog(logPath, $"Portable ZIP update failed: {ex.Message}");
                if (Directory.Exists(extractionRoot))
                {
                    Directory.Delete(extractionRoot, recursive: true);
                }
                return 3;
            }
        }

        if (!extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The update package must be a ZIP or Windows installer.");
        ProcessStartInfo installerInfo = new()
        {
            FileName = packagePath,
            UseShellExecute = false
        };
        foreach (string argument in new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CURRENTUSER", "/DIR=" + pending.InstallDirectory })
            installerInfo.ArgumentList.Add(argument);
        using Process installer = Process.Start(installerInfo)
            ?? throw new InvalidOperationException("Windows did not start the update installer.");
        installer.WaitForExit();
        AppendLog(logPath, $"Installer exited with code {installer.ExitCode}.");
        return installer.ExitCode;
    }

    private static string FindPortablePackageRoot(string extractionRoot)
    {
        // Some ZIP files contain files at the root and some contain one folder.
        // This finds the folder that actually contains Slithy Tove.exe.
        string directExecutable = Path.Combine(extractionRoot, "Slithy Tove.exe");
        if (File.Exists(directExecutable))
        {
            return extractionRoot;
        }

        string[] matches = Directory.GetFiles(
            extractionRoot,
            "Slithy Tove.exe",
            SearchOption.AllDirectories);
        if (matches.Length != 1)
        {
            throw new InvalidDataException("The update ZIP does not contain Slithy Tove.exe.");
        }
        return Path.GetDirectoryName(matches[0])
            ?? throw new InvalidDataException("The update ZIP layout is invalid.");
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        // Copy every folder and file from source into destination, replacing old files.
        Directory.CreateDirectory(destinationDirectory);
        foreach (string directory in Directory.EnumerateDirectories(
                     sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
        }
        foreach (string file in Directory.EnumerateFiles(
                     sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDirectory, file);
            string destination = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static Process StartApplication(string executable, string argument)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add(argument);
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows did not restart Slithy Tove.");
    }

    private static bool WaitForHealthyMarker(
        string updateDirectory,
        string expectedToken,
        Process updatedProcess)
    {
        // The new app has 30 seconds to prove it started. If it does not, rollback starts.
        string healthyPath = Path.Combine(updateDirectory, HealthyFileName);
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (File.Exists(healthyPath) &&
                    File.ReadAllText(healthyPath).Trim().Equals(expectedToken, StringComparison.Ordinal))
                    return true;
            }
            catch (IOException) { /* The next poll retries a temporarily busy marker. */ }
            if (updatedProcess.HasExited)
            {
                return false;
            }
            Thread.Sleep(500);
        }
        return false;
    }

    private static void TryTerminate(Process process)
    {
        if (process.HasExited) return;
        try
        {
            process.Kill(entireProcessTree: true);
            if (!process.WaitForExit(5_000))
                throw new TimeoutException("The replacement app has not exited. Rollback files were left untouched.");
        }
        catch (InvalidOperationException) when (process.HasExited) { }
    }

    private static void AppendLog(string path, string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(
            path,
            $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
    }
}

