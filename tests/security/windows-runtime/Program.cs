using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Slithy_Tove;

int checks = 0;
void Check(bool value, string name)
{
    if (!value) throw new Exception(name);
    Console.WriteLine("PASS " + name);
    checks++;
}
bool Rejects(Action action)
{
    try { action(); return false; }
    catch (IOException) { return true; }
    catch (InvalidDataException) { return true; }
    catch (ArgumentException) { return true; }
}

foreach (int status in new[] { 302, 307, 308 })
{
    await using HttpFixture target = new(_ => "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
    await using HttpFixture origin = new(_ => $"HTTP/1.1 {status} Redirect\r\nLocation: {target.Address}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
    using NodeRpcClient rpc = new();
    bool rejected = false;
    try { await rpc.CallAsync(new UriBuilder(origin.Address) { UserName = "fixture", Password = "test-auth" }.Uri,
        "test-wallet", "walletpassphrase", new object[] { "fixture-wallet-passphrase", 10 }, TimeSpan.FromSeconds(3)); }
    catch (HttpRequestException error) { rejected = error.Message.Contains("redirect"); }
    Check(rejected, $"RPC rejects HTTP {status}");
    Check(origin.RequestCount == 1 && origin.LastRequest.Contains("fixture-wallet-passphrase") &&
        origin.LastRequest.Contains("Authorization: Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("fixture:test-auth"))),
        $"HTTP {status} preserves the original authenticated RPC request");
    Check(target.RequestCount == 0 && target.BytesRead == 0, $"HTTP {status} sends no request or body to redirect target");
}
Check(NodeRpcClient.RedactRpcError("Bad password fixture-secret", "walletpassphrase", new object[] { "fixture-secret", 10 }) == "Bad password [redacted]", "RPC errors redact wallet passphrases");
Check(NodeRpcClient.RedactRpcError("old-secret new-secret", "walletpassphrasechange", new[] { "old-secret", "new-secret" }) == "[redacted] [redacted]", "RPC errors redact both password-change arguments");
Check(NodeRpcClient.RedactRpcError("Invalid test-secret", "createwallet", new object[] { "fixture", false, false, "test-secret" }) == "Invalid [redacted]", "RPC errors redact new-wallet passwords");
Check(NodeRpcClient.RedactRpcError("Invalid descriptor wpkh(tprvABC123/0/*)", "getdescriptorinfo", new[] { "wpkh(tprvABC123/0/*)" }) == "Invalid descriptor [redacted]", "RPC errors redact full private descriptors");
Check(!NodeRpcClient.RedactPrivateKeys("Bad checksum for wpkh(tprvABC123/0/*)").Contains("tprvABC123"), "Import errors redact embedded private keys");
Check(NodeRpcClient.RedactRpcError("Error: This wallet is not encrypted.", "walletpassphrase", new object[] { "fixture-secret", 10 }).Contains("not encrypted"), "Nonsecret wallet errors remain usable");
await using (HttpFixture errorNode = new(_ =>
{
    const string json = "{\"result\":null,\"error\":{\"code\":-8,\"message\":\"Invalid fixture-error-passphrase\"},\"id\":1}";
    return $"HTTP/1.1 500 Internal Server Error\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(json)}\r\nConnection: close\r\n\r\n{json}";
}))
{
    using NodeRpcClient rpc = new();
    string error = "";
    try { await rpc.CallAsync(errorNode.Address, "fixture", "walletpassphrase", new object[] { "fixture-error-passphrase", 10 }); }
    catch (InvalidOperationException failure) { error = failure.Message; }
    Check(error == "Invalid [redacted]", "HTTP RPC error is redacted before reaching callers");
}

string root = Path.Combine(Path.GetTempPath(), "slithy-runtime-security-" + Guid.NewGuid().ToString("N"));
List<string> junctions = [];
try
{
    Directory.CreateDirectory(root);
    string package = Path.Combine(root, "package.zip");
    File.WriteAllText(package, "fixture package");
    string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(package)));
    using (FileStream verified = UpdateInstaller.OpenVerifiedPackage(package, hash))
    {
        Check(verified.Position == 0, "Verified package remains open and ready to read");
        Check(Rejects(() => File.WriteAllText(package, "replacement")), "Verified package rejects concurrent writes");
        Check(Rejects(() => File.Delete(package)), "Verified package rejects deletion before install");
    }
    Check(Rejects(() => { using var ignored = UpdateInstaller.OpenVerifiedPackage(package, new string('0', 64)); }), "Changed package hash is rejected");
    File.WriteAllText(package, "after close");
    Check(File.ReadAllText(package) == "after close", "Package handle is released after verification scope");

    string source = Path.Combine(root, "backup");
    Directory.CreateDirectory(source);
    File.WriteAllText(Path.Combine(source, "wallet.dat"), "fixture-wallet");
    string wallets = Path.Combine(root, "wallets");
    Check(Rejects(() => WalletBackupTools.RestoreBackup(source, wallets, "..")), "Restore rejects parent traversal");
    Check(Rejects(() => WalletBackupTools.RestoreBackup(source, wallets, "child/wallet")), "Restore rejects nested wallet names");
    WalletBackupTools.RestoreBackup(source, wallets, "restored");
    Check(File.ReadAllText(Path.Combine(wallets, "restored", "wallet.dat")) == "fixture-wallet", "Normal wallet backup restores");
    Check(Rejects(() => WalletBackupTools.RestoreBackup(source, wallets, "restored")), "Restore refuses an existing wallet");
    Check(UpdateInstaller.RunRollbackSelfTest(), "Portable install and complete rollback still pass");

    string outside = Path.Combine(root, "outside");
    Directory.CreateDirectory(outside);
    File.WriteAllText(Path.Combine(outside, "untouched.txt"), "original");
    void Junction(string path, string target)
    {
        ProcessStartInfo info = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.Arguments = $"/d /c mklink /J \"{path}\" \"{target}\"";
        using Process process = Process.Start(info)!;
        process.WaitForExit();
        if (process.ExitCode != 0) throw new IOException("Fixture junction could not be created: " + process.StandardError.ReadToEnd());
        junctions.Add(path);
    }
    string sourceLink = Path.Combine(source, "linked");
    Junction(sourceLink, outside);
    Check(Rejects(() => WalletBackupTools.RestoreBackup(source, wallets, "linked-source")), "Restore rejects a linked source child");
    Check(!Directory.Exists(Path.Combine(wallets, "linked-source")), "Linked source is rejected before destination creation");
    Directory.Delete(sourceLink);
    junctions.Remove(sourceLink);
    string destinationLink = Path.Combine(root, "redirected");
    Junction(destinationLink, outside);
    Check(Rejects(() => WalletBackupTools.RestoreBackup(source, Path.Combine(destinationLink, "nested"), "wallet")), "Restore rejects a linked destination ancestor");
    string install = Path.Combine(root, "installed");
    Directory.CreateDirectory(install);
    Junction(Path.Combine(install, "subfolder"), outside);
    Directory.CreateDirectory(Path.Combine(source, "subfolder"));
    File.WriteAllText(Path.Combine(source, "subfolder", "untouched.txt"), "replacement");
    Check(Rejects(() => SafeDirectoryCopy.Copy(source, install, true)), "Update copy rejects a linked destination child");
    Check(File.ReadAllText(Path.Combine(outside, "untouched.txt")) == "original", "Linked destination target is unchanged");
    string dangling = Path.Combine(root, "dangling");
    Junction(dangling, Path.Combine(root, "missing"));
    Check(Rejects(() => SafeDirectoryCopy.RejectLinksInPath(Path.Combine(dangling, "child"))), "Dangling destination junction is rejected");
}
finally
{
    // Remove fixture links themselves before deleting the owned temporary tree.
    foreach (string junction in junctions.AsEnumerable().Reverse()) Directory.Delete(junction);
    string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (Path.GetFullPath(root).StartsWith(temp, StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
}
Console.WriteLine($"{checks} Windows runtime security checks passed. No real wallets were opened.");

sealed class HttpFixture : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _server;
    public Uri Address { get; }
    public int RequestCount;
    public int BytesRead;
    public string LastRequest = "";
    public HttpFixture(Func<string, string> reply)
    {
        _listener.Start();
        Address = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
        _server = Task.Run(async () =>
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    using TcpClient client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    using NetworkStream stream = client.GetStream();
                    using MemoryStream request = new();
                    byte[] buffer = new byte[1024];
                    while (request.Length < 8192)
                    {
                        int count = await stream.ReadAsync(buffer, _stop.Token);
                        if (count == 0) break;
                        Interlocked.Add(ref BytesRead, count);
                        request.Write(buffer, 0, count);
                        string text = Encoding.UTF8.GetString(request.ToArray());
                        int end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                        if (end < 0) continue;
                        string? header = text[..end].Split("\r\n").FirstOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                        int length = header is null ? 0 : int.Parse(header.Split(':')[1].Trim());
                        if (request.Length < end + 4 + length) continue;
                        LastRequest = text;
                        Interlocked.Increment(ref RequestCount);
                        await stream.WriteAsync(Encoding.UTF8.GetBytes(reply(text)), _stop.Token);
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        });
    }
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        await _server;
        _listener.Stop();
        _stop.Dispose();
    }
}
