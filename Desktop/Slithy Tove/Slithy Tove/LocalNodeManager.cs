//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Local Node Manager
//===============================================
using System.Diagnostics;

namespace Slithy_Tove;

// Starts and stops the local slithyd process used for mining on this computer.
internal sealed class LocalNodeManager
{
    private readonly Action<string> _log;
    private readonly AppSettingsStore _paths;
    private readonly NodeRpcClient _rpcClient = new();
    private Process? _process;
    private readonly object _startupLock = new();
    private string _startupStage = "Starting the local Slithy node...";
    private long _startupWorkSequence;

    public LocalNodeManager(Action<string> log, AppSettingsStore? paths = null)
    {
        _log = log;
        _paths = paths ?? new AppSettingsStore();
    }

    public bool IsRunning => _process is { HasExited: false };

    public NodeStartupActivity ReadStartupActivity()
    {
        Process? process = _process;
        if (process is null || process.HasExited)
            return new(false, TimeSpan.Zero, 0, null);
        TimeSpan cpu;
        try
        {
            process.Refresh();
            cpu = process.TotalProcessorTime;
        }
        catch (InvalidOperationException)
        {
            return new(false, TimeSpan.Zero, 0, null);
        }
        lock (_startupLock)
            return new(true, cpu, _startupWorkSequence, _startupStage);
    }

    private void ObserveStartupLine(string line)
    {
        const string marker = "init message: ";
        int offset = line.IndexOf(marker, StringComparison.Ordinal);
        if (offset < 0) return;
        string stage = NodeRpcClient.DescribeStartupStage(line[(offset + marker.Length)..]);
        lock (_startupLock)
        {
            if (stage == _startupStage) return;
            _startupStage = stage;
            _startupWorkSequence++;
        }
    }

    public Task StartAsync(AppSettings settings)
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        lock (_startupLock)
        {
            _startupStage = "Starting the local Slithy node...";
            _startupWorkSequence = 0;
        }

        string executable = Path.Combine(AppContext.BaseDirectory, "Binaries", "slithyd.exe");
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException(
                "The bundled slithyd.exe was not found. Rebuild the desktop project.", executable);
        }

        Directory.CreateDirectory(_paths.NodeDataDirectory);
        Directory.CreateDirectory(_paths.WalletDirectory);
        Directory.CreateDirectory(_paths.LogDirectory);
        string logFile = Path.Combine(_paths.LogDirectory, "slithyd.log");
        List<string> nodeArguments =
        [
            "-testnet",
            // Beta miners may be offline for days. An old tip alone is not an unfinished download.
            // The node still checks headers, chain work and every block.
            "-maxtipage=2147483647",
            $"-datadir={_paths.NodeDataDirectory}",
            $"-walletdir={_paths.WalletDirectory}",
            "-server=1",
            // The bundled core needs this flag to report peer heights before the first block.
            "-deprecatedrpc=startingheight",
            "-listen=0",
            $"-port={settings.LocalP2pPort}"
        ];
        foreach (string peer in settings.GetPeerSeedAddresses())
        {
            nodeArguments.Add($"-addnode={peer}");
        }
        nodeArguments.AddRange([
            "-rpcbind=127.0.0.1",
            "-rpcallowip=127.0.0.1",
            $"-rpcport={settings.LocalRpcPort}",
            $"-rpcuser={settings.RpcUser}",
            $"-rpcpassword={settings.RpcPassword}",
            "-fallbackfee=0.0001",
            $"-debuglogfile={logFile}"
        ]);

        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        foreach (string argument in nodeArguments) startInfo.ArgumentList.Add(argument);
        _process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        _process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                ObserveStartupLine(e.Data);
                _log(e.Data);
            }
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                ObserveStartupLine(e.Data);
                _log(e.Data);
            }
        };
        _process.Exited += (_, _) =>
        {
            int exitCode;
            try
            {
                exitCode = _process?.ExitCode ?? -1;
            }
            catch
            {
                exitCode = -1;
            }

            _log($"Local node exited with code {exitCode}.");
        };

        if (!_process.Start())
        {
            throw new InvalidOperationException("Windows did not start the local Slithy node.");
        }

        try
        {
            ChildProcessJob.Assign(_process);
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }
        catch
        {
            // A failed job assignment must not leave an unmanaged miner behind.
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit();
            }
            _process.Dispose();
            _process = null;
            throw;
        }
        _log($"Local node process started with PID {_process.Id}.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(Uri nodeUri)
    {
        if (!IsRunning)
        {
            return;
        }

        try
        {
            await _rpcClient.StopDaemonAsync(nodeUri);
            if (_process is not null)
            {
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
            }
        }
        catch (Exception ex)
        {
            _log($"Graceful stop was unavailable: {ex.Message}");
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        finally
        {
            _process?.Dispose();
            _process = null;
        }
    }
}

