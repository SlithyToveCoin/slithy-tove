using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Slithy_Tove;

internal static class WalletInterop
{
    public static async Task<int> RunAsync(string[] args)
    {
        string Argument(string name) => args[Array.IndexOf(args, name) + 1];
        string source = Path.GetFullPath(Argument("--fixture"));
        string binary = Path.GetFullPath(Argument("--node"));
        if (!OperatingSystem.IsLinux()) throw new InvalidOperationException("Run the interoperability stage with the reviewed Linux native daemon.");
        bool reuse = args.Contains("--existing-regtest");
        string directory = reuse ? Path.GetFullPath(Argument("--existing-regtest")) : Path.Combine(Path.GetTempPath(), "slithy-wallet-interop-" + Guid.NewGuid().ToString("N"));
        if (reuse && (Path.GetDirectoryName(directory) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) || !Path.GetFileName(directory).StartsWith("slithy-wallet-interop-", StringComparison.Ordinal)))
            throw new InvalidOperationException("The resume path must be an isolated interoperability directory in the system temporary folder.");
        Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        if (!reuse) foreach (string folder in new[] { "blocks", "chainstate" }) CopyDirectory(Path.Combine(source, "regtest", folder), Path.Combine(directory, "regtest", folder));
        string privateFile = Path.Combine(directory, "fixture.json");
        File.Copy(Path.Combine(source, "fixture.json"), privateFile);
        File.SetUnixFileMode(privateFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        using JsonDocument fixture = JsonDocument.Parse(await File.ReadAllTextAsync(privateFile));
        string words = fixture.RootElement.GetProperty("Words").GetString()!;
        string boundary = fixture.RootElement.GetProperty("ReceivingBoundary").GetString()!;
        string deep = fixture.RootElement.GetProperty("ReceivingDeep").GetString()!;
        string change = fixture.RootElement.GetProperty("Change").GetString()!;
        using TcpListener reservation = new(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        string conf = Path.Combine(directory, "slithy.conf");
        await File.WriteAllTextAsync(conf, $"regtest=1\nserver=1\nrpcuser=fixture\nrpcpassword={password}\n[regtest]\nlisten=0\ndnsseed=0\ndiscover=0\nconnect=0\nfallbackfee=0.0001\nrpcbind=127.0.0.1\nrpcallowip=127.0.0.1\nrpcport={port}\n");
        File.SetUnixFileMode(conf, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        ProcessStartInfo start = new(binary) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-datadir=" + directory);
        start.ArgumentList.Add("-printtoconsole=0");
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the Linux fixture.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using NodeRpcClient rpc = new();
        Uri endpoint = new($"http://fixture:{password}@127.0.0.1:{port}");
        Task<JsonElement> Call(string wallet, string method, params object[] parameters) => rpc.CallAsync(endpoint, wallet, method, parameters, TimeSpan.FromMinutes(15));
        int passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            passed++;
            Console.WriteLine("PASS " + name);
        }
        try
        {
            bool ready = false;
            for (int attempt = 0; attempt < 120; attempt++)
            {
                if (process.HasExited) throw new InvalidOperationException("Linux fixture stopped during startup.");
                try { ready = (await Call("", "getblockchaininfo")).GetProperty("chain").GetString() == "regtest"; if (ready) break; } catch (HttpRequestException) { } catch (InvalidOperationException) { }
                await Task.Delay(250);
            }
            Check(ready && (await Call("", "getconnectioncount")).GetInt32() == 0, "Linux imported the Windows fixture chain without public peers");
            using WalletRpcClient standard = new();
            if (reuse) await standard.OpenWalletAsync(endpoint, "cross-default", "fixture-passphrase");
            else await standard.RestoreWalletAsync(endpoint, "cross-default", "fixture-passphrase", words, 0);
            IReadOnlyList<WalletUnspentOutput> available = await standard.GetSpendableOutputsAsync(endpoint);
            if (reuse)
            {
                WalletTransactionDetails originalPayment = await standard.GetTransactionDetailsAsync(endpoint, fixture.RootElement.GetProperty("Payment").GetString()!);
                Check(originalPayment.Outputs.Any(output => output.Address == change && output.IsMine) &&
                    (await standard.GetHistoryPageAsync(endpoint, search: boundary)).Entries.Any(entry => entry.Address == boundary),
                    "reopened cross-platform wallet retains receiving and change history after spending");
            }
            else Check(available.Any(coin => coin.Address == change) && available.Any(coin => coin.Address == boundary), "Windows words restore funded change1001 and receive10000 on Linux");
            WalletSnapshot standardSnapshot = await standard.GetSnapshotAsync(endpoint);
            Check(standardSnapshot.Balance > 0 && standardSnapshot.Address.Length > 0, "restored boundary wallet opens with its balance");
            using WalletRpcClient restored = new();
            if (reuse) await restored.OpenWalletAsync(endpoint, "cross-deep", "fixture-passphrase");
            else await restored.RestoreWalletAsync(endpoint, "cross-deep", "fixture-passphrase", words, 123, SlithySeedPhrase.MaximumRecoveryRangeEnd);
            JsonElement restoredDescriptors = await Call("cross-deep", "listdescriptors", false);
            Check(restoredDescriptors.GetProperty("descriptors").EnumerateArray().All(item => item.GetProperty("range")[1].GetInt32() >= SlithySeedPhrase.MaximumRecoveryRangeEnd), "both restored branches include the maximum explicit scan range");
            Check((await restored.GetHistoryPageAsync(endpoint, search: deep)).Entries.Any(entry => entry.Address == deep), "maximum100000 range restores funded index12001 history across platforms");
            Check(await restored.CheckRecoveryWordsAsync(endpoint, words), "cross-platform restored descriptors match the original words");
            WalletSnapshot snapshot = await restored.GetSnapshotAsync(endpoint);
            Check(snapshot.Balance > 0 && snapshot.Address.Length > 0, "deep-restored wallet opens without generating another address");
            await restored.UnlockWalletAsync(endpoint, "fixture-passphrase", TimeSpan.FromMinutes(10));
            string[] descriptors = SlithySeedPhrase.BuildDescriptors(words, "regtest");
            JsonElement descriptorInfo = await Call("", "getdescriptorinfo", descriptors[1]);
            object[] imports = [new { desc = descriptors[1] + "#" + descriptorInfo.GetProperty("checksum").GetString(), active = true, @internal = true,
                range = new[] { 0, SlithySeedPhrase.MaximumRecoveryRangeEnd }, next_index = 10001, timestamp = "now" }];
            SlithySeedPhrase.ThrowIfImportFailed(await Call("cross-deep", "importdescriptors", (object)imports));
            string recipientWallet = "recipient-" + Guid.NewGuid().ToString("N");
            await Call("", "createwallet", recipientWallet);
            string recipient = (await Call(recipientWallet, "getnewaddress")).GetString()!;
            PreparedTransfer payment = await restored.PrepareTransferAsync(endpoint, recipient, 100_000_000);
            Check(payment.ReusesChangeAddress && payment.AmountAtomic == 100_000_000, "deep-restored wallet prepares spending with recovery-safe reused change");
            await restored.RelayTransferAsync(endpoint, payment);
            await Call("", "generatetoaddress", 1, recipient);
            Check((await Call("cross-deep", "gettransaction", payment.TransactionHash)).GetProperty("confirmations").GetInt32() > 0, "deep-restored Linux wallet spends Windows-generated funds");
            await restored.LockWalletOnlyAsync(endpoint);
            Check((await Call("cross-deep", "getwalletinfo")).GetProperty("unlocked_until").GetInt64() == 0, "restored wallet spending lock is confirmed");
            Check((await Call("", "getconnectioncount")).GetInt32() == 0, "cross-platform test stayed on its disconnected chain");
            return passed;
        }
        finally
        {
            try { await Call("", "stop"); } catch { }
            if (!process.HasExited && !process.WaitForExit(15000)) process.Kill(true);
            await stdout;
            await stderr;
            File.Delete(privateFile);
            File.Delete(conf);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (string folder in Directory.EnumerateDirectories(source)) CopyDirectory(folder, Path.Combine(destination, Path.GetFileName(folder)));
    }
}
