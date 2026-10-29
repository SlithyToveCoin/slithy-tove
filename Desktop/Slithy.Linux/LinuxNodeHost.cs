using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Slithy_Tove;

namespace Slithy.Linux;

internal sealed record LinuxStorageInfo(long FreeBytes, long NodeBytes, string Path);

// The GUI owns its node. It never borrows a running server or installer service.
internal sealed class LinuxNodeHost : IAsyncDisposable
{
    private readonly LinuxSettingsStore _store;
    private readonly LinuxSettings _settings;
    private readonly bool _regtest;
    private readonly string? _binaryPath;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly NodeRpcClient _rpc = new();
    private readonly string _cookieName = "gui-rpc-" + Guid.NewGuid().ToString("N") + ".cookie";
    private Process? _process;
    private bool _processStarted;
    private FileStream? _instanceLock;
    private bool _disposed;
    private string? _startupDiagnostic;
    private string[] _peerOrder = ["borogove", "mome", "rath"];
    private int RpcPort => _regtest ? 54436 : 54426;
    private int P2pPort => _regtest ? 54434 : 54424;
    private string CookiePath => Path.Combine(DataDirectory, _cookieName);
    public string DataDirectory => _regtest ? Path.Combine(_store.DataDirectory, "isolated-regtest") : _store.DataDirectory;
    public string WalletDirectory => Path.Combine(DataDirectory, "wallets");
    public string LogPath => Path.Combine(DataDirectory, "node.log");
    public bool IsRunning => _processStarted && _process != null && !_process.HasExited;
    public string PreferredNodeId => _peerOrder[0];

    public LinuxNodeHost(LinuxSettingsStore store, LinuxSettings settings, bool regtest = false, string? binaryPath = null)
    {
        _store = store;
        _settings = settings;
        _regtest = regtest;
        _binaryPath = binaryPath;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("The local Linux node runs on Linux. Use preview mode to inspect the interface here.");
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsRunning) return;
            if (_instanceLock != null) { _instanceLock.Dispose(); _instanceLock = null; }
            LinuxSettingsStore.EnsurePrivateDirectory(DataDirectory);
            LinuxSettingsStore.EnsurePrivateDirectory(WalletDirectory);
            string lockPath = Path.Combine(DataDirectory, "gui-instance.lock");
            LinuxSettingsStore.RejectLink(lockPath);
            try
            {
                _instanceLock = new FileStream(lockPath, new FileStreamOptions
                {
                    Mode = FileMode.OpenOrCreate, Access = FileAccess.ReadWrite, Share = FileShare.None,
                    UnixCreateMode = LinuxSettingsStore.PrivateFile
                });
            }
            catch (IOException)
            {
                throw new InvalidOperationException("Another Slithy GUI may be using this data directory. Close it before opening a second copy.");
            }
            string binary = ResolveBinary();
            EnsurePortAvailable(RpcPort);
            LinuxSettingsStore.RejectLink(CookiePath);
            LinuxSettingsStore.RejectLink(LogPath);
            string config = Path.Combine(DataDirectory, "gui-node.conf");
            LinuxSettingsStore.RejectLink(config);
            // A separate empty config prevents a CLI installation's RPC or mining options leaking into this node.
            using (FileStream file = new(config, new FileStreamOptions
            {
                Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None,
                UnixCreateMode = LinuxSettingsStore.PrivateFile
            }))
            {
                byte[] bytes = Encoding.UTF8.GetBytes("# Managed by the local Slithy GUI. Node options are supplied at startup.\n");
                await file.WriteAsync(bytes, cancellationToken);
            }
            File.SetUnixFileMode(config, LinuxSettingsStore.PrivateFile);
            if (!_regtest) await ChoosePeersAsync(cancellationToken);
            ProcessStartInfo start = new(binary)
            {
                UseShellExecute = false, CreateNoWindow = true,
                WorkingDirectory = DataDirectory,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (string argument in BuildArguments(config)) start.ArgumentList.Add(argument);
            _process?.Dispose();
            _process = new Process { StartInfo = start };
            _processStarted = false;
            _startupDiagnostic = null;
            // Map known startup failures to fixed text. Raw output can contain private paths or credentials.
            _process.OutputDataReceived += (_, e) => RecordStartupDiagnostic(e.Data);
            _process.ErrorDataReceived += (_, e) => RecordStartupDiagnostic(e.Data);
            _processStarted = _process.Start();
            if (!_processStarted) throw new InvalidOperationException("The local Slithy node did not start.");
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                if (_process.HasExited)
                {
                    await _process.WaitForExitAsync(timeout.Token);
                    throw new InvalidOperationException(_startupDiagnostic ?? $"The local node stopped during startup (exit code {_process.ExitCode}). Check node.log in the GUI data directory. If no log was created, report your Linux version and this exit code to support.");
                }
                try
                {
                    Uri endpoint = await ReadEndpointAsync(timeout.Token);
                    var chain = await _rpc.CallAsync(endpoint, "", "getblockchaininfo", Array.Empty<object>(), TimeSpan.FromSeconds(2));
                    if (_process.HasExited) throw new InvalidOperationException("The local node stopped during startup. It has not been replaced with another running node.");
                    string expected = _regtest ? "regtest" : "test";
                    if (chain.GetProperty("chain").GetString() != expected) throw new InvalidOperationException("The local node is running an unexpected network.");
                    if (!_regtest && await _rpc.GetBlockHashAsync(endpoint, 0, TimeSpan.FromSeconds(2)) != NodeRpcClient.BetaGenesisHash)
                        throw new InvalidOperationException("This native node does not use the current Slithy beta chain. Replace the bundled native tools with the reviewed beta build.");
                    return;
                }
                catch (Exception ex) when (ex is IOException or HttpRequestException or TaskCanceledException) { }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Loading", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("Verifying", StringComparison.OrdinalIgnoreCase)) { }
                await Task.Delay(250, timeout.Token);
            }
        }
        catch
        {
            await StopCoreAsync();
            throw;
        }
        finally { _lifecycle.Release(); }
    }

    private void RecordStartupDiagnostic(string? output)
    {
        string? message = StartupDiagnostic(output);
        if (message != null) Interlocked.CompareExchange(ref _startupDiagnostic, message, null);
    }

    internal static string? StartupDiagnostic(string? output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;
        if (output.Contains("GLIBC_", StringComparison.Ordinal) && output.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return "This native node needs a newer Linux C library. Check the supported Linux versions on the download page.";
        if (output.Contains("error while loading shared libraries", StringComparison.OrdinalIgnoreCase))
            return "A Linux system library needed by the native node is missing. Check the Linux dependencies on the download page.";
        if (output.Contains("Disk space is too low", StringComparison.OrdinalIgnoreCase) || output.Contains("No space left on device", StringComparison.OrdinalIgnoreCase))
            return "The local node ran out of disk space. Free space on the drive that holds your Slithy data before trying again.";
        if (output.Contains("Cannot obtain a lock", StringComparison.OrdinalIgnoreCase))
            return "Another node is using this data folder. Close that node before starting this copy of Slithy.";
        if (output.Contains("Unable to bind", StringComparison.OrdinalIgnoreCase) || output.Contains("Address already in use", StringComparison.OrdinalIgnoreCase))
            return "The local node could not open its network port because another process is using it. That process has not been stopped.";
        if (output.Contains("Invalid parameter", StringComparison.OrdinalIgnoreCase) || output.Contains("Unknown command line parameter", StringComparison.OrdinalIgnoreCase))
            return "The bundled native node rejected a startup option. Extract the complete Linux GUI package again so its files match.";
        return null;
    }

    private string ResolveBinary()
    {
        string? path = _binaryPath ?? Environment.GetEnvironmentVariable("SLITHY_NODE_BIN");
        path ??= Path.Combine(AppContext.BaseDirectory, "Binaries", "slithyd");
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
            throw new FileNotFoundException("Slithyd was not found. Place the reviewed Linux binary in Binaries/slithyd, or set SLITHY_NODE_BIN to its absolute filename.");
        return path;
    }

    internal IReadOnlyList<string> BuildArguments(string config)
    {
        List<string> args =
        [
            _regtest ? "-regtest" : "-testnet", "-conf=" + config, "-datadir=" + DataDirectory,
            "-walletdir=" + WalletDirectory, "-server=1", "-daemon=0", "-listen=0", "-listenonion=0",
            "-discover=0", "-natpmp=0", "-rpcbind=127.0.0.1", "-rpcallowip=127.0.0.1",
            "-rpcport=" + RpcPort, "-port=" + P2pPort, "-rpccookiefile=" + CookiePath,
            "-fallbackfee=0.0001", "-debuglogfile=" + LogPath, "-printtoconsole=0"
        ];
        if (_regtest) { args.Add("-connect=0"); args.Add("-dnsseed=0"); args.Add("-fixedseeds=0"); }
        else
        {
            // Beta miners may leave the chain quiet for days. Header and chain-work checks still apply.
            args.Add("-maxtipage=2147483647");
            // The shared peer check uses the handshake height before the first block arrives.
            args.Add("-deprecatedrpc=startingheight");
            foreach (string node in _peerOrder.OrderBy(n => n == _settings.PublicNodePreference ? 0 : 1))
                args.Add("-addnode=" + node + ".slithy.io:53424");
        }
        return args;
    }

    private async Task ChoosePeersAsync(CancellationToken cancellationToken)
    {
        string[] nodes = ["borogove", "mome", "rath"];
        if (_settings.PublicNodePreference != "auto")
        {
            _peerOrder = nodes.OrderBy(n => n == _settings.PublicNodePreference ? 0 : 1).ToArray();
            return;
        }
        var probes = await Task.WhenAll(nodes.Select(async node =>
        {
            using TcpClient probe = new();
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            Stopwatch clock = Stopwatch.StartNew();
            try
            {
                await probe.ConnectAsync(node + ".slithy.io", 53424, timeout.Token);
                return (Node: node, Delay: clock.ElapsedMilliseconds);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                return (Node: node, Delay: long.MaxValue);
            }
        }));
        cancellationToken.ThrowIfCancellationRequested();
        // Start with the quickest reachable seed; retain the others for independent peers and failover.
        _peerOrder = probes.OrderBy(p => p.Delay).Select(p => p.Node).ToArray();
    }

    private static void EnsurePortAvailable(int port)
    {
        using TcpListener listener = new(IPAddress.Loopback, port);
        try { listener.Start(); }
        catch (SocketException) { throw new InvalidOperationException($"Local RPC port {port} is already in use. Slithy will not attach to or stop the other process."); }
    }

    public Task<Uri> GetEndpointAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning) throw new InvalidOperationException("The local Slithy node is not running.");
        return ReadEndpointAsync(cancellationToken);
    }

    private async Task<Uri> ReadEndpointAsync(CancellationToken cancellationToken)
    {
        LinuxSettingsStore.RejectLink(CookiePath);
        string cookie = (await File.ReadAllTextAsync(CookiePath, cancellationToken)).Trim();
        string[] parts = cookie.Split(':', 2);
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace)) throw new IOException("The local node authentication cookie is not ready.");
        return new Uri($"http://{Uri.EscapeDataString(parts[0])}:{Uri.EscapeDataString(parts[1])}@127.0.0.1:{RpcPort}/");
    }

    public LinuxStorageInfo GetStorage()
    {
        long bytes = 0;
        if (Directory.Exists(DataDirectory))
        {
            EnumerationOptions options = new() { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            foreach (string file in Directory.EnumerateFiles(DataDirectory, "*", options))
                try { bytes += new FileInfo(file).Length; } catch (IOException) { }
        }
        string current = DataDirectory;
        while (!Directory.Exists(current)) current = Path.GetDirectoryName(current) ?? Path.GetPathRoot(DataDirectory)!;
        DriveInfo? volume = DriveInfo.GetDrives().Where(d => d.IsReady && (current == d.Name || current.StartsWith(d.Name.TrimEnd('/') + "/", StringComparison.Ordinal)))
            .OrderByDescending(d => d.Name.Length).FirstOrDefault();
        return new LinuxStorageInfo(volume?.AvailableFreeSpace ?? -1, bytes, DataDirectory);
    }

    public async Task StopOwnedAsync()
    {
        await _lifecycle.WaitAsync();
        try { await StopCoreAsync(); }
        finally { _lifecycle.Release(); }
    }

    private async Task StopCoreAsync()
    {
        if (IsRunning && _process != null)
        {
            try { await _rpc.StopDaemonAsync(await ReadEndpointAsync(CancellationToken.None)); }
            catch { /* If startup failed before RPC, SIGTERM still requests a clean native shutdown. */ }
            if (!_process.HasExited && OperatingSystem.IsLinux())
            {
                using Process? signal = Process.Start(new ProcessStartInfo("/bin/kill")
                {
                    UseShellExecute = false, ArgumentList = { "-TERM", _process.Id.ToString() }
                });
                if (signal != null) await signal.WaitForExitAsync();
            }
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(45));
            try { await _process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException("The local node is still saving its data. Leave the app open and try closing it again. It has not been force-stopped.");
            }
        }
        _process?.Dispose();
        _process = null;
        _processStarted = false;
        _instanceLock?.Dispose();
        _instanceLock = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopOwnedAsync();
        _disposed = true;
        _rpc.Dispose();
    }
}
