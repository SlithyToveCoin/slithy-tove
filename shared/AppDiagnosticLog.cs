using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;

namespace Slithy.Diagnostics;

// Durable diagnostics use fixed labels. Raw UI messages can contain wallet data or
// daemon output, so this logger never writes those messages to disk.
internal sealed class AppDiagnosticLog : IDisposable
{
    internal const int MaximumFileBytes = 64 * 1024;
    internal const int RetainedFileCount = 4;
    internal const int QueueCapacity = 64;
    private const UnixFileMode PrivateDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly object _gate = new();
    private readonly string? _directory;
    private readonly string _version = typeof(AppDiagnosticLog).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
    private readonly int _maximumFileBytes;
    private readonly int _retainedFileCount;
    private readonly Channel<LogEntry> _queue = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(QueueCapacity)
    {
        SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait,
        AllowSynchronousContinuations = false
    });
    private readonly Action? _beforeWrite;
    private Task? _writerTask;
    private bool _disposed;
    private Mutex? _directoryMutex;
    private FileStream? _stream;
    private string? _previousEvent;
    private DateTimeOffset _previousEventUtc;
    private volatile bool _disabled;

    private sealed record LogEntry(string? Label, DateTimeOffset Time, TaskCompletionSource? Completion = null);

    internal AppDiagnosticLog(string directory) : this(directory, MaximumFileBytes, RetainedFileCount) { }

    // Smaller limits let the file tests exercise rotation without filling a real log.
    internal AppDiagnosticLog(string directory, int maximumFileBytes, int retainedFileCount, Action? beforeWrite = null)
    {
        _maximumFileBytes = Math.Clamp(maximumFileBytes, 256, MaximumFileBytes);
        _retainedFileCount = Math.Clamp(retainedFileCount, 1, RetainedFileCount);
        _beforeWrite = beforeWrite;
        try
        {
            if (!Path.IsPathFullyQualified(directory)) { _disabled = true; return; }
            _directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            if (_directory.Equals(Path.TrimEndingDirectorySeparator(Path.GetPathRoot(_directory)!), StringComparison.OrdinalIgnoreCase))
                _disabled = true;
        }
        catch { _disabled = true; }
    }

    internal void RecordMessage(string? message)
    {
        try
        {
            string? entry = Describe(message);
            if (entry is null) return;
            lock (_gate)
            {
                if (_disabled || _disposed || _directory is null) return;
                // Raw messages never enter the queue. A slow drive can drop diagnostics,
                // but it cannot make a mining button wait for a file write.
                if (!_queue.Writer.TryWrite(new LogEntry(entry, DateTimeOffset.UtcNow))) return;
                _writerTask ??= Task.Run(WriteLoopAsync);
            }
        }
        catch { /* Diagnostics must not interrupt the caller. */ }
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (LogEntry entry in _queue.Reader.ReadAllAsync())
            {
                if (entry.Completion is not null) { entry.Completion.TrySetResult(); continue; }
                if (!_disabled) WriteEntry(entry.Label!, entry.Time);
            }
        }
        finally
        {
            _disabled = true;
            try { _stream?.Dispose(); } catch { }
            try { _directoryMutex?.Dispose(); } catch { }
            _stream = null;
            _directoryMutex = null;
        }
    }

    private void WriteEntry(string entry, DateTimeOffset now)
    {
        try
        {
            _beforeWrite?.Invoke();
            if (entry == _previousEvent && now >= _previousEventUtc && now - _previousEventUtc < TimeSpan.FromSeconds(30)) return;
            _directoryMutex ??= new Mutex(false, "Slithy.AppDiagnostics." +
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_directory!)))[..32]);
            bool ownsMutex = false;
            try
            {
                try { ownsMutex = _directoryMutex.WaitOne(0); }
                catch (AbandonedMutexException) { ownsMutex = true; }
                if (!ownsMutex) return;
                EnsureDirectory();
                byte[] bytes = Encoding.UTF8.GetBytes(now.ToString("O", CultureInfo.InvariantCulture) +
                    " version=" + _version + " " + entry + "\n");
                if (_stream is not null && _stream.Length + bytes.Length > _maximumFileBytes)
                {
                    _stream.Dispose();
                    _stream = null;
                }
                _stream ??= OpenNewFile();
                _stream.Write(bytes);
                _stream.Flush();
                _previousEvent = entry;
                _previousEventUtc = now;
            }
            finally { if (ownsMutex) _directoryMutex.ReleaseMutex(); }
        }
        catch
        {
            // A full disk or a permission change must not interrupt wallet work.
            _disabled = true;
            try { _stream?.Dispose(); } catch { }
            _stream = null;
        }
    }

    // Tests can await a queue barrier. The apps never wait for disk I/O on shutdown.
    internal async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        Task? writer;
        bool disposed;
        lock (_gate) { writer = _writerTask; disposed = _disposed; }
        if (writer is null) return;
        if (disposed) { await writer.WaitAsync(cancellationToken); return; }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await _queue.Writer.WriteAsync(new LogEntry(null, default, completion), cancellationToken);
            await completion.Task.WaitAsync(cancellationToken);
        }
        catch (ChannelClosedException) { await writer.WaitAsync(cancellationToken); }
    }

    internal int QueuedEntries => _queue.Reader.Count;

    internal static string? Describe(string? message)
    {
        if (string.IsNullOrEmpty(message) || message.Length > 16_384) return null;
        string? name = message switch
        {
            "Slithy Tove desktop started." => "app.started",
            "Local node started. Wallets stay on this computer." => "node.started",
            "CPU mining stopped." or "Mining stopped during application exit." => "mining.stopped",
            "Mining stopped. The update download can continue." => "mining.stopped",
            "Wallet opened." => "wallet.opened",
            "Wallet locked. Mining and balance updates continue." or "Wallet locked after inactivity." => "wallet.locked",
            "Wallet opening was cancelled." => "wallet.open.cancelled",
            "Wallet startup prompt was dismissed." => "wallet.open.dismissed",
            "Local Slithy mining node is ready." => "node.ready",
            "Local node fetched missing blocks from a peer." => "node.catchup.progress",
            "Preferences could not be saved." => "settings.save.failed",
            "Wallet refresh is still busy." => "wallet.refresh.busy",
            _ => null
        };
        if (name is not null) return "event=" + name;

        (string Prefix, string Event)[] events =
        [
            ("Mining status request (getcpumininginfo) failed:", "mining.status.failed"),
            ("Mining status check failed (", "mining.status.failed"),
            ("Mining balance refresh failed:", "wallet.balance.failed"),
            ("Wallet balance refresh failed (", "wallet.balance.failed"),
            ("Wallet refresh failed:", "wallet.balance.failed"),
            ("Wallet refresh after mining change failed:", "wallet.balance.failed"),
            ("Mining control failed:", "mining.command.failed"),
            ("Mining stop warning during exit:", "mining.stop.failed"),
            ("The start request could not be confirmed.", "mining.start.unconfirmed"),
            ("The stop request could not be confirmed.", "mining.stop.unconfirmed"),
            ("Mining start was not confirmed (", "mining.start.unconfirmed"),
            ("Mining stop was not confirmed (", "mining.stop.unconfirmed"),
            ("Mining did not confirm that it stopped.", "mining.stop.unconfirmed"),
            ("CPU mining started with ", "mining.started"),
            ("Status refresh failed", "app.status.failed"),
            ("Network status refresh failed", "node.status.failed"),
            ("Local node unavailable:", "node.status.failed"),
            ("Local node catch-up check skipped:", "node.catchup.failed"),
            ("Disk usage check failed", "storage.check.failed"),
            ("Shutdown failed:", "app.shutdown.failed"),
            ("Update check failed:", "update.check.failed"),
            ("Verified update saved to ", "update.download.verified"),
            ("Activity refresh failed:", "wallet.history.failed"),
            ("Wallet open failed:", "wallet.open.failed"),
            ("Wallet lock warning:", "wallet.lock.failed"),
            ("Wallet lock was not confirmed.", "wallet.lock.failed"),
            ("Wallet backup failed:", "wallet.backup.failed"),
            ("Wallet backup created at ", "wallet.backup.saved"),
            ("Created wallet ", "wallet.created"),
            ("Restored wallet ", "wallet.restored"),
            ("Opened wallet ", "wallet.opened"),
            ("Transfer failed:", "wallet.send.failed"),
            ("Sent transaction ", "wallet.send.broadcast"),
            ("Confirmed previous payment ", "wallet.send.confirmed"),
            ("Previous send outcome is unknown:", "wallet.send.unconfirmed"),
            ("Public node change failed:", "node.preference.failed"),
            ("Public node preference changed to ", "node.preference.changed"),
            ("Automatic public node chose ", "node.preference.automatic"),
            ("Diagnostics exported to ", "diagnostics.exported")
        ];
        foreach ((string prefix, string eventName) in events)
        {
            if (!message.StartsWith(prefix, StringComparison.Ordinal)) continue;
            return "event=" + eventName + (eventName.EndsWith(".failed", StringComparison.Ordinal) ||
                eventName.EndsWith(".unconfirmed", StringComparison.Ordinal)
                ? " kind=" + FailureKind(message) : "");
        }
        return null;
    }

    private static string FailureKind(string message)
    {
        bool Has(string text) => message.Contains(text, StringComparison.OrdinalIgnoreCase);
        if (Has("TaskCanceledException") || Has("OperationCanceledException") || Has("TimeoutException") ||
            Has("timed out") || Has("timeout") || Has("task was canceled") || Has("operation was canceled")) return "timeout";
        if (Has("credentials do not match") || Has("Unauthorized") || Has("Forbidden") || Has("Authorization failed")) return "authentication";
        if (Has("walletpassphrase") || Has("wallet is locked") || Has("wallet locked")) return "wallet-locked";
        if (Has("rescanning") || Has("rescan already in progress")) return "wallet-scanning";
        if (Has("HttpRequestException") || Has("SocketException") || Has("connection refused") ||
            Has("actively refused") || Has("connection reset")) return "connection";
        if (Has("JsonException") || Has("invalid JSON")) return "invalid-response";
        if (Has("disk space") || Has("disk is full") || Has("No space left")) return "disk-full";
        if (Has("IOException") || Has("database")) return "storage";
        return "unspecified";
    }

    private void EnsureDirectory()
    {
        // Never follow a link into another directory. The app supplies its own user-data path.
        for (DirectoryInfo? directory = new(_directory!); directory is not null; directory = directory.Parent)
            RejectLink(directory.FullName);
        if (!Directory.Exists(_directory))
        {
            if (OperatingSystem.IsWindows()) Directory.CreateDirectory(_directory!);
            else Directory.CreateDirectory(_directory!, PrivateDirectory);
        }
        RejectLink(_directory!);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(_directory!, PrivateDirectory);
    }

    private FileStream OpenNewFile()
    {
        List<FileInfo> files = new DirectoryInfo(_directory!).EnumerateFiles("app-*.log", SearchOption.TopDirectoryOnly)
            .Where(file => IsOwnFileName(file.Name)).Take(65).OrderBy(file => file.LastWriteTimeUtc).ToList();
        if (files.Count > 64) throw new IOException("Diagnostic retention needs attention.");
        foreach (FileInfo file in files) RejectLink(file.FullName);
        // Delete directory entries, never truncate a pre-existing file. An old file
        // could have been replaced with a hard link while the app was not running.
        while (files.Count >= _retainedFileCount)
        {
            FileInfo oldest = files[0];
            File.Delete(oldest.FullName);
            files.RemoveAt(0);
        }
        string path = Path.Combine(_directory!, "app-" + Guid.NewGuid().ToString("N") + ".log");
        FileStreamOptions options = new() { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.Read };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = PrivateFile;
        return new FileStream(path, options);
    }

    private static bool IsOwnFileName(string name) => name.Length == 40 && name.StartsWith("app-", StringComparison.Ordinal) &&
        name.EndsWith(".log", StringComparison.Ordinal) && name.AsSpan(4, 32).IndexOfAnyExcept("0123456789abcdef".AsSpan()) < 0;

    private static void RejectLink(string path)
    {
        FileSystemInfo entry = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        if (entry.LinkTarget is not null || (entry.Exists && (entry.Attributes & FileAttributes.ReparsePoint) != 0))
            throw new IOException("Diagnostic paths cannot be links.");
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _queue.Writer.TryComplete();
        }
    }
}
