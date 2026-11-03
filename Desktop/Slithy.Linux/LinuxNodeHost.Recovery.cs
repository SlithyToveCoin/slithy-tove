using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Slithy.Linux;

internal sealed partial class LinuxNodeHost
{
    private sealed record Ownership(int Version, int ProcessId, string StartTicks, string Executable,
        string ExecutableHash, string[] Arguments);
    private string OwnershipPath => Path.Combine(DataDirectory, "gui-node-owner.json");

    [DllImport("libc")] private static extern uint geteuid();

    private async Task RecordOwnershipAsync(string binary, string[] arguments, CancellationToken token)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        LinuxGuiUpdatePaths.RejectLinks(OwnershipPath);
        string executable = Path.GetFullPath(binary);
        await using var stream = File.OpenRead(executable);
        var receipt = new Ownership(1, _process!.Id, ReadStartTicks(_process.Id), executable,
            Convert.ToHexString(await SHA256.HashDataAsync(stream, token)), arguments);
        string temporary = OwnershipPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var file = new FileStream(temporary, new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
                UnixCreateMode = LinuxSettingsStore.PrivateFile
            }))
            {
                await JsonSerializer.SerializeAsync(file, receipt, cancellationToken: token);
                file.Flush(true);
            }
            File.Move(temporary, OwnershipPath, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    private static string ReadStartTicks(int pid)
    {
        string stat = File.ReadAllText($"/proc/{pid}/stat");
        int end = stat.LastIndexOf(')');
        string[] fields = stat[(end + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (end < 0 || fields.Length <= 19 || !ulong.TryParse(fields[19], out _))
            throw new IOException("The previous node's process identity could not be read.");
        return fields[19];
    }

    private Ownership ReadOwnership()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        LinuxGuiUpdatePaths.RejectLinks(OwnershipPath);
        if (new FileInfo(OwnershipPath).Length > 32768) throw new IOException("The node recovery record is too large.");
        if ((File.GetUnixFileMode(OwnershipPath) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) != 0)
            throw new IOException("The node recovery record must be private to your Linux account.");
        Ownership receipt = JsonSerializer.Deserialize<Ownership>(File.ReadAllText(OwnershipPath))
            ?? throw new IOException("The node recovery record is empty.");
        if (receipt.Version != 1 || receipt.ProcessId <= 1 || receipt.Arguments == null ||
            receipt.Arguments.Length > 64 || receipt.Arguments.Any(string.IsNullOrWhiteSpace) ||
            !ulong.TryParse(receipt.StartTicks, out _) || string.IsNullOrWhiteSpace(receipt.Executable) ||
            !Path.IsPathFullyQualified(receipt.Executable) ||
            !Regex.IsMatch(receipt.ExecutableHash ?? "", "^[A-Fa-f0-9]{64}$"))
            throw new IOException("The node recovery record is invalid.");
        ValidateRecordedArguments(receipt.Arguments);
        return receipt;
    }

    private string Argument(string[] arguments, string name)
    {
        string[] matches = arguments.Where(a => a.StartsWith(name + "=", StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1) throw new IOException("The previous node's startup options do not match this app.");
        return matches[0][(name.Length + 1)..];
    }

    private void ValidateRecordedArguments(string[] arguments)
    {
        string cookie = Argument(arguments, "-rpccookiefile");
        if (Argument(arguments, "-datadir") != DataDirectory || Argument(arguments, "-walletdir") != WalletDirectory ||
            Argument(arguments, "-conf") != Path.Combine(DataDirectory, "gui-node.conf") ||
            Argument(arguments, "-rpcport") != RpcPort.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
            Argument(arguments, "-rpcbind") != "127.0.0.1" || Argument(arguments, "-rpcallowip") != "127.0.0.1" ||
            Argument(arguments, "-daemon") != "0" || Argument(arguments, "-server") != "1" ||
            !arguments.Contains(_regtest ? "-regtest" : "-testnet") ||
            arguments.Any(a => a == (_regtest ? "-testnet" : "-regtest") || a.StartsWith("-chain", StringComparison.Ordinal)) ||
            Path.GetDirectoryName(cookie) != DataDirectory ||
            !Regex.IsMatch(Path.GetFileName(cookie), "^gui-rpc-[a-f0-9]{32}\\.cookie$"))
            throw new IOException("The previous node belongs to a different data folder or network. It has not been stopped.");
    }

    private async Task VerifyProcessAsync(Ownership receipt, string expectedBinary, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (ReadStartTicks(receipt.ProcessId) != receipt.StartTicks)
            throw new IOException("That process ID now belongs to another program. It has not been stopped.");
        string status = File.ReadAllText($"/proc/{receipt.ProcessId}/status");
        string[] uid = status.Split('\n').Single(line => line.StartsWith("Uid:", StringComparison.Ordinal))
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (uid.Length != 5 || uid.Skip(1).Any(value => value != geteuid().ToString(System.Globalization.CultureInfo.InvariantCulture)))
            throw new IOException("The previous process belongs to another Linux account. It has not been stopped.");
        string executable = new FileInfo($"/proc/{receipt.ProcessId}/exe").LinkTarget ?? "";
        if (executable != receipt.Executable || !File.Exists(executable))
            throw new IOException("The previous node's executable changed. It has not been stopped.");
        LinuxGuiUpdatePaths.RejectLinks(executable);
        LinuxGuiUpdatePaths.RejectLinks(expectedBinary);
        await using var oldBinary = File.OpenRead(executable);
        await using var currentBinary = File.OpenRead(expectedBinary);
        string hash = Convert.ToHexString(await SHA256.HashDataAsync(oldBinary, token));
        if (hash != receipt.ExecutableHash || hash != Convert.ToHexString(await SHA256.HashDataAsync(currentBinary, token)))
            throw new IOException("The previous node does not match this build's native program. It has not been stopped.");
        byte[] raw = await File.ReadAllBytesAsync($"/proc/{receipt.ProcessId}/cmdline", token);
        if (raw.Length > 65536) throw new IOException("The previous process has unexpected startup options.");
        string[] command = Encoding.UTF8.GetString(raw).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (command.Length < 2 || command[0] != receipt.Executable || !command.Skip(1).SequenceEqual(receipt.Arguments))
            throw new IOException("The previous process has different startup options. It has not been stopped.");
        ValidateRecordedArguments(receipt.Arguments);
    }

    private async Task<Uri> PreviousEndpointAsync(Ownership receipt, CancellationToken token)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        string path = Argument(receipt.Arguments, "-rpccookiefile");
        LinuxGuiUpdatePaths.RejectLinks(path);
        if (new FileInfo(path).Length > 1024) throw new IOException("The previous node's authentication file is invalid.");
        if ((File.GetUnixFileMode(path) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) != 0)
            throw new IOException("The previous node's authentication file is not private.");
        string[] cookie = (await File.ReadAllTextAsync(path, token)).Trim().Split(':', 2);
        if (cookie.Length != 2 || cookie.Any(string.IsNullOrWhiteSpace)) throw new IOException("The previous node's authentication file is incomplete.");
        var endpoint = new Uri($"http://{Uri.EscapeDataString(cookie[0])}:{Uri.EscapeDataString(cookie[1])}@127.0.0.1:{RpcPort}/");
        var chain = await _rpc.CallAsync(endpoint, "", "getblockchaininfo", Array.Empty<object>(), TimeSpan.FromSeconds(3));
        if (chain.GetProperty("chain").GetString() != (_regtest ? "regtest" : "test") ||
            (!_regtest && await _rpc.GetBlockHashAsync(endpoint, 0, TimeSpan.FromSeconds(3)) != Slithy_Tove.NodeRpcClient.BetaGenesisHash))
            throw new IOException("The previous node is on a different network. It has not been stopped.");
        return endpoint;
    }

    private async Task RecoverPreviousNodeAsync(string binary, CancellationToken token)
    {
        if (!File.Exists(OwnershipPath)) return;
        Ownership receipt;
        try { receipt = ReadOwnership(); }
        catch (JsonException) { throw new IOException("The node recovery record could not be read. No process was stopped."); }
        if (!Directory.Exists($"/proc/{receipt.ProcessId}")) { File.Delete(OwnershipPath); return; }
        await VerifyProcessAsync(receipt, binary, token);
        await PreviousEndpointAsync(receipt, token);
        if (ConfirmOrphanRecoveryAsync == null || !await ConfirmOrphanRecoveryAsync())
            throw new InvalidOperationException("The previous Slithy node is still running. Recovery was canceled; that node was not stopped.");
        // Recheck after the dialog. A stale PID must never authorize stopping a new process.
        await VerifyProcessAsync(receipt, binary, token);
        Uri endpoint = await PreviousEndpointAsync(receipt, token);
        await VerifyProcessAsync(receipt, binary, token);
        using Process previous = Process.GetProcessById(receipt.ProcessId);
        await _rpc.StopDaemonAsync(endpoint);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try { await previous.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            throw new IOException("The previous node is still saving its data. Wait and try again. It has not been force-stopped.");
        }
        File.Delete(OwnershipPath);
    }

    private void RemoveOwnReceipt(int processId)
    {
        if (!File.Exists(OwnershipPath)) return;
        try
        {
            Ownership receipt = ReadOwnership();
            if (receipt.ProcessId == processId) File.Delete(OwnershipPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A damaged receipt must not prevent closing an already stopped node.
            // Leave that record untouched so the next start can report the problem.
        }
    }
}
