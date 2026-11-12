using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Slithy.Diagnostics;

internal static class Program
{
    private static int checks;
    private static readonly List<string> Failures = [];
    private const string PrivateMarker = "DISPOSABLE-PRIVATE-MARKER-736bd2f1";

    private static void Main()
    {
        string temporary = Path.Combine(Path.GetTempPath(), "slithy-diagnostic-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            TestAllowlist(Path.Combine(temporary, "allowlist"));
            TestRetention(Path.Combine(temporary, "retention"));
            TestExistingFile(Path.Combine(temporary, "existing"));
            TestFailures(temporary);
            TestLinks(temporary);
            TestPermissions(Path.Combine(temporary, "permissions"));
            TestConcurrentWriters(Path.Combine(temporary, "concurrent"));
            TestBlockedWriter(Path.Combine(temporary, "blocked-writer"));
            TestWorkerFailure(Path.Combine(temporary, "worker-failure"));
            if (Failures.Count > 0)
                throw new InvalidOperationException($"{Failures.Count} of {checks} checks failed:\n" + string.Join("\n", Failures));
            Console.WriteLine($"Passed {checks} diagnostic log checks. All files were temporary; no user data or network was accessed.");
        }
        finally
        {
            string resolved = Path.GetFullPath(temporary);
            string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(parent, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(resolved).StartsWith("slithy-diagnostic-log-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected cleanup path.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
        }
    }

    private static void TestAllowlist(string directory)
    {
        using AppDiagnosticLog log = new(directory);
        Check(!Directory.Exists(directory), "Constructing the logger does not create files");
        foreach (string message in new[] { PrivateMarker, "rpcpassword=" + PrivateMarker,
            "xprv-" + PrivateMarker, "recovery words: " + PrivateMarker, "https://fixture:" + PrivateMarker + "@invalid/" })
            log.RecordMessage(message);
        Check(!Directory.Exists(directory), "Unknown messages, credentials and arbitrary daemon output are not persisted");
        log.RecordMessage("Mining status request (getcpumininginfo) failed: TaskCanceledException " + PrivateMarker);
        Flush(log);
        string first = ReadAll(directory);
        Check(first.Contains("event=mining.status.failed kind=timeout", StringComparison.Ordinal), "A known failure becomes a fixed event and failure category");
        Check(!first.Contains(PrivateMarker, StringComparison.Ordinal), "A secret inside a recognized error is omitted");
        log.RecordMessage("Mining status request (getcpumininginfo) failed: TaskCanceledException another-private-value");
        Flush(log);
        Check(ReadAll(directory) == first, "Repeated adjacent events are throttled even when their raw messages differ");

        string[] messages =
        [
            "Created wallet ", "Opened wallet ", "Restored wallet ", "Wallet backup created at ",
            "Sent transaction ", "Confirmed previous payment ", "Previous send outcome is unknown:",
            "Verified update saved to ", "Diagnostics exported to ", "Public node preference changed to ",
            "Automatic public node chose ", "CPU mining started with ", "Transfer failed:", "Wallet open failed:",
            "Mining balance refresh failed:", "Mining control failed:", "Activity refresh failed:"
        ];
        foreach (string prefix in messages) log.RecordMessage(prefix + PrivateMarker + "\nInjected second line");
        Flush(log);
        string text = ReadAll(directory);
        Check(!text.Contains(PrivateMarker, StringComparison.Ordinal) && !text.Contains("Injected", StringComparison.Ordinal),
            "Recognized messages never persist their variable payload or injected lines");
        Regex format = new(@"^\d{4}-\d{2}-\d{2}T[0-9:.]+\+00:00 version=\d+(?:\.\d+){1,3} event=[a-z.]+(?: kind=[a-z-]+)?$", RegexOptions.CultureInvariant);
        Check(text.Split('\n', StringSplitOptions.RemoveEmptyEntries).All(line => format.IsMatch(line)),
            "Every durable line contains a UTC time, numeric assembly version and fixed labels");
        Check(AppDiagnosticLog.Describe("Wallet refresh failed: Please enter the wallet passphrase with walletpassphrase first. " + PrivateMarker) ==
            "event=wallet.balance.failed kind=wallet-locked", "A locked-wallet error is classified without recording its text");
        Check(AppDiagnosticLog.Describe("Mining status check failed (HttpRequestException). " + PrivateMarker) ==
            "event=mining.status.failed kind=connection", "Linux exception names map to the same fixed categories");
        Check(AppDiagnosticLog.Describe("Mining start was not confirmed (TaskCanceledException). " + PrivateMarker) ==
            "event=mining.start.unconfirmed kind=timeout", "An unconfirmed Linux start records its fixed failure category");
        Check(AppDiagnosticLog.Describe("Mining stop was not confirmed (HttpRequestException). " + PrivateMarker) ==
            "event=mining.stop.unconfirmed kind=connection", "An unconfirmed Linux stop records its fixed failure category");
        Check(AppDiagnosticLog.Describe("Wallet open failed: connection credentials do not match " + PrivateMarker) ==
            "event=wallet.open.failed kind=authentication", "Authentication failures do not record credentials");
        Check(AppDiagnosticLog.Describe("Wallet open failed:" + new string('x', 17_000)) is null,
            "Unexpectedly large messages are discarded");
        Close(log);
        string closed = ReadAll(directory);
        log.RecordMessage("CPU mining stopped.");
        Check(ReadAll(directory) == closed, "Disposed loggers ignore further writes");
    }

    private static void TestRetention(string directory)
    {
        using (AppDiagnosticLog log = new(directory, maximumFileBytes: 512, retainedFileCount: 3))
        {
            for (int i = 0; i < 80; i++) log.RecordMessage(i % 2 == 0 ? "CPU mining stopped." : "Slithy Tove desktop started.");
            Flush(log);
            log.RecordMessage("Mining status check failed (TimeoutException).");
            Close(log);
        }
        FileInfo[] files = Files(directory);
        Check(files.Length == 3, "Rotation retains the requested number of files");
        Check(files.All(file => file.Length <= 512) && files.Sum(file => file.Length) <= 1536,
            "Individual file size and total retained size remain bounded");
        Check(ReadAll(directory).Contains("event=mining.status.failed kind=timeout", StringComparison.Ordinal),
            "Rotation retains the most recent diagnostic event");
        Check(AppDiagnosticLog.MaximumFileBytes == 65_536 && AppDiagnosticLog.RetainedFileCount == 4,
            "Production retention is four files of at most 64 KiB each");
    }

    private static void TestExistingFile(string directory)
    {
        Directory.CreateDirectory(directory);
        string existing = Path.Combine(directory, "app-" + Guid.NewGuid().ToString("N") + ".log");
        File.WriteAllText(existing, "pre-existing-fixture-content");
        using (AppDiagnosticLog log = new(directory)) { log.RecordMessage("Slithy Tove desktop started."); Close(log); }
        Check(File.ReadAllText(existing) == "pre-existing-fixture-content" && Files(directory).Length == 2,
            "A new logger creates a file instead of appending to or truncating an existing file");
    }

    private static void TestFailures(string root)
    {
        string blocker = Path.Combine(root, "not-a-directory");
        File.WriteAllText(blocker, "unchanged");
        using (AppDiagnosticLog log = new(Path.Combine(blocker, "diagnostics")))
        {
            log.RecordMessage("Slithy Tove desktop started.");
            log.RecordMessage("CPU mining stopped.");
            Close(log);
        }
        Check(File.ReadAllText(blocker) == "unchanged", "A filesystem failure does not throw or change the blocking file");
        using (AppDiagnosticLog log = new("relative-path-refused")) log.RecordMessage("Slithy Tove desktop started.");
        Check(!Directory.Exists("relative-path-refused"), "Relative diagnostic directories are refused without throwing");
        using (AppDiagnosticLog log = new(Path.GetPathRoot(root)!)) log.RecordMessage("Slithy Tove desktop started.");
        Check(true, "A filesystem root is refused without interrupting the caller");
    }

    private static void TestLinks(string root)
    {
        string target = Path.Combine(root, "link-target");
        Directory.CreateDirectory(target);
        string linkedDirectory = Path.Combine(root, "linked-directory");
        bool canMakeLinks;
        try { Directory.CreateSymbolicLink(linkedDirectory, target); canMakeLinks = true; }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        { canMakeLinks = false; Console.WriteLine("SKIP Symbolic-link creation is not available to this test account."); }
        if (canMakeLinks)
        {
            using (AppDiagnosticLog log = new(Path.Combine(linkedDirectory, "diagnostics"))) { log.RecordMessage("Slithy Tove desktop started."); Close(log); }
            Check(!Directory.Exists(Path.Combine(target, "diagnostics")), "An ancestor symbolic link is rejected before creating a log directory");
            Directory.Delete(linkedDirectory);
            string ownDirectory = Path.Combine(root, "file-link");
            Directory.CreateDirectory(ownDirectory);
            string protectedFile = Path.Combine(target, "untouched.txt");
            File.WriteAllText(protectedFile, "unchanged-link-target");
            string link = Path.Combine(ownDirectory, "app-" + Guid.NewGuid().ToString("N") + ".log");
            File.CreateSymbolicLink(link, protectedFile);
            using (AppDiagnosticLog log = new(ownDirectory)) { log.RecordMessage("Slithy Tove desktop started."); Close(log); }
            Check(File.ReadAllText(protectedFile) == "unchanged-link-target" && Files(ownDirectory).Length == 1,
                "An existing symbolic log file is rejected without changing its target");
            File.Delete(link);
        }

        string hardLinkDirectory = Path.Combine(root, "hard-link");
        Directory.CreateDirectory(hardLinkDirectory);
        string original = Path.Combine(target, "hard-link-target.txt");
        File.WriteAllText(original, "unchanged-hard-link-target");
        string alias = Path.Combine(hardLinkDirectory, "app-" + Guid.NewGuid().ToString("N") + ".log");
        bool linked = OperatingSystem.IsWindows() ? CreateHardLink(alias, original, IntPtr.Zero) : Link(original, alias) == 0;
        Check(linked, "The disposable hard-link fixture was created");
        if (linked)
        {
            using (AppDiagnosticLog log = new(hardLinkDirectory, 512, 1)) { log.RecordMessage("Slithy Tove desktop started."); Close(log); }
            Check(File.ReadAllText(original) == "unchanged-hard-link-target" && !File.Exists(alias),
                "Retention removes an old hard-link entry without writing to its target");
        }
    }

    private static void TestPermissions(string directory)
    {
        using (AppDiagnosticLog log = new(directory)) { log.RecordMessage("Slithy Tove desktop started."); Close(log); }
        if (OperatingSystem.IsWindows())
        {
            Check(Files(directory).Length == 1, "Windows writes under the supplied user-local directory");
            return;
        }
        Check(File.GetUnixFileMode(directory) == (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute),
            "Linux diagnostic directory mode is 0700");
        bool privateFiles = true;
        foreach (FileInfo file in Files(directory))
            privateFiles &= File.GetUnixFileMode(file.FullName) == (UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Check(privateFiles, "Linux diagnostic file mode is 0600");
    }

    private static void TestConcurrentWriters(string directory)
    {
        using (AppDiagnosticLog log = new(directory, 512, 4))
        {
            Parallel.For(0, 120, i => log.RecordMessage(i % 2 == 0 ? "CPU mining stopped." : "Slithy Tove desktop started."));
            Close(log);
        }
        Check(Files(directory).Length <= 4 && Files(directory).All(file => file.Length <= 512),
            "Concurrent callers respect retention and size limits");
        Check(ReadAll(directory).Split('\n', StringSplitOptions.RemoveEmptyEntries).All(line =>
            line.EndsWith("event=mining.stopped", StringComparison.Ordinal) || line.EndsWith("event=app.started", StringComparison.Ordinal)),
            "Concurrent callers write complete diagnostic lines");
    }

    private static void TestBlockedWriter(string directory)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int active = 0, maximumActive = 0;
        using AppDiagnosticLog log = new(directory, 512, 4, () =>
        {
            int count = Interlocked.Increment(ref active);
            maximumActive = Math.Max(maximumActive, count);
            entered.Set();
            release.Wait();
            Interlocked.Decrement(ref active);
        });
        try
        {
            log.RecordMessage("Slithy Tove desktop started.");
            Check(entered.Wait(TimeSpan.FromSeconds(10)), "The disposable writer reached its simulated slow disk");
            Stopwatch elapsed = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++) log.RecordMessage(i % 2 == 0 ? "CPU mining stopped." : "Slithy Tove desktop started.");
            Check(elapsed.Elapsed < TimeSpan.FromSeconds(2) && !release.IsSet,
                "Recording a burst returns without waiting for a blocked writer");
            Check(log.QueuedEntries == AppDiagnosticLog.QueueCapacity && log.QueuedEntries == 64,
                "A blocked drive cannot grow the diagnostic queue beyond 64 fixed entries");
            elapsed.Restart();
            log.Dispose();
            Check(elapsed.Elapsed < TimeSpan.FromSeconds(2) && !release.IsSet,
                "Disposing the logger does not wait for a blocked disk");
        }
        finally { release.Set(); Flush(log); }
        Check(maximumActive == 1 && active == 0, "A single background writer drains accepted entries after release");
    }

    private static void TestWorkerFailure(string directory)
    {
        int attempts = 0;
        using AppDiagnosticLog log = new(directory, 512, 4, () =>
        {
            Interlocked.Increment(ref attempts);
            throw new IOException("Disposable simulated disk failure.");
        });
        log.RecordMessage("Slithy Tove desktop started.");
        Flush(log);
        log.RecordMessage("CPU mining stopped.");
        Close(log);
        Check(attempts == 1 && !Directory.Exists(directory),
            "A background disk failure disables writes and still completes the queue without throwing");
    }

    private static void Flush(AppDiagnosticLog log)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        log.FlushAsync(timeout.Token).GetAwaiter().GetResult();
    }
    private static void Close(AppDiagnosticLog log) { log.Dispose(); Flush(log); }
    private static FileInfo[] Files(string directory) => new DirectoryInfo(directory).GetFiles("app-*.log");
    private static string ReadAll(string directory) => string.Concat(Files(directory).OrderBy(file => file.LastWriteTimeUtc).Select(file =>
    {
        using FileStream stream = new(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }));
    private static void Check(bool passed, string message)
    {
        checks++;
        if (!passed) Failures.Add(message);
        Console.WriteLine((passed ? "PASS " : "FAIL ") + message);
    }
    [DllImport("Kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);
    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(string existingPath, string linkPath);
}
