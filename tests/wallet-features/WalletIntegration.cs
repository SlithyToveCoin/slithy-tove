using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using NBitcoin;
using Slithy_Tove;

internal static class WalletIntegration
{
    public static async Task<int> RunAsync(string[] args)
    {
        int flag = Array.IndexOf(args, "--node");
        if (flag < 0 || flag + 1 >= args.Length) throw new ArgumentException("Pass --node with a reviewed local slithyd binary.");
        string binary = Path.GetFullPath(args[flag + 1]);
        if (!File.Exists(binary)) throw new FileNotFoundException("Reviewed test daemon not found.");
        string directory = Path.Combine(Path.GetTempPath(), "slithy-wallet-features-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        using TcpListener reservation = new(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await File.WriteAllTextAsync(Path.Combine(directory, "slithy.conf"),
            $"regtest=1\nserver=1\nrpcuser=fixture\nrpcpassword={password}\n[regtest]\nlisten=0\ndnsseed=0\ndiscover=0\nconnect=0\nfallbackfee=0.0001\nrpcbind=127.0.0.1\nrpcallowip=127.0.0.1\nrpcport={port}\n");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Path.Combine(directory, "slithy.conf"), UnixFileMode.UserRead | UnixFileMode.UserWrite);
        ProcessStartInfo start = new(binary) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-datadir=" + directory);
        start.ArgumentList.Add("-printtoconsole=0");
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the isolated daemon.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using NodeRpcClient rpc = new();
        Uri endpoint = new($"http://fixture:{password}@127.0.0.1:{port}");
        int checks = 0;
        object? interoperability = null;
        void Check(bool pass, string name)
        {
            if (!pass) throw new InvalidOperationException(name);
            Console.WriteLine("PASS " + name);
            checks++;
        }
        Task<JsonElement> Call(string wallet, string method, params object[] parameters) => rpc.CallAsync(endpoint, wallet, method, parameters, TimeSpan.FromMinutes(10));
        try
        {
            bool ready = false;
            for (int attempt = 0; attempt < 120; attempt++)
            {
                if (process.HasExited) throw new InvalidOperationException("The isolated daemon stopped during startup: " + (await stderr).Replace(password, "[redacted]", StringComparison.Ordinal));
                try { ready = (await Call("", "getblockchaininfo")).GetProperty("chain").GetString() == "regtest"; if (ready) break; } catch (HttpRequestException) { } catch (InvalidOperationException) { }
                await Task.Delay(250);
            }
            Check(ready, "isolated daemon is on regtest");
            Check((await Call("", "getconnectioncount")).GetInt32() == 0, "fixture has no public peers");
            await Call("", "createwallet", "funding");
            string mining = (await Call("funding", "getnewaddress")).GetString()!;
            async Task Mine(int blocks)
            {
                while (blocks > 0) { int count = Math.Min(blocks, 10); await Call("", "generatetoaddress", count, mining); blocks -= count; }
            }
            await Mine(110);

            using WalletRpcClient original = new();
            string words = await original.CreateSeedWalletAsync(endpoint, "words-original", "fixture-passphrase", TimeSpan.FromHours(1));
            Check(await original.CheckRecoveryWordsAsync(endpoint, words), "recovery words match both original descriptors");
            Check(!await original.CheckRecoveryWordsAsync(endpoint, "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about"), "different valid words do not match");
            string[] descriptors = SlithySeedPhrase.BuildDescriptors(words, "regtest");
            async Task<string> PublicDescriptor(int branch) => (await Call("", "getdescriptorinfo", descriptors[branch])).GetProperty("descriptor").GetString()!;
            string external = await PublicDescriptor(0);
            string internalDescriptor = await PublicDescriptor(1);
            async Task<string> Derive(string descriptor, int index) => (await Call("", "deriveaddresses", descriptor, new[] { index, index }))[0].GetString()!;
            string address1001 = await Derive(external, 1001);
            string address10000 = await Derive(external, 10000);
            string deepAddress = await Derive(external, 12001);
            // Extend the original wallet for a controlled boundary and change-path fixture.
            object[] import = SlithySeedPhrase.BuildImportRequests(new[]
            {
                descriptors[0] + "#" + (await Call("", "getdescriptorinfo", descriptors[0])).GetProperty("checksum").GetString(),
                descriptors[1] + "#" + (await Call("", "getdescriptorinfo", descriptors[1])).GetProperty("checksum").GetString()
            }, false);
            foreach (Dictionary<string, object?> item in import.Cast<Dictionary<string, object?>>())
            {
                item["range"] = new[] { 0, 14000 };
                item["next_index"] = (bool)item["internal"]! ? 1001 : 12002;
            }
            SlithySeedPhrase.ThrowIfImportFailed(await Call("words-original", "importdescriptors", (object)import));
            await Call("funding", "sendtoaddress", address1001, 20m);
            await Call("funding", "sendtoaddress", address10000, 3m);
            await Call("funding", "sendtoaddress", deepAddress, 2m);
            await Mine(1);
            string recipient = (await Call("funding", "getnewaddress")).GetString()!;
            PreparedTransfer changing = await original.PrepareTransferAsync(endpoint, recipient, 100_000_000);
            await original.RelayTransferAsync(endpoint, changing);
            await Mine(1);
            WalletTransactionDetails details = await original.GetTransactionDetailsAsync(endpoint, changing.TransactionHash);
            Check(details.Outputs.Any(output => output.IsMine) && details.Outputs.Any(output => output.Address == recipient), "payment details distinguish recipient and wallet-owned change");
            string change1001 = await Derive(internalDescriptor, 1001);
            Check(details.Outputs.Any(output => output.Address == change1001), "real payment used funded change branch index 1001");
            interoperability = new { Words = words, ReceivingBoundary = address10000, ReceivingDeep = deepAddress, Change = change1001, Payment = changing.TransactionHash };
            if (!args.Contains("--skip-recovery"))
            {
            using WalletRpcClient restored = new();
            await restored.RestoreWalletAsync(endpoint, "words-restored", "fixture-passphrase", words, 0);
            IReadOnlyList<WalletUnspentOutput> restoredCoins = await restored.GetSpendableOutputsAsync(endpoint);
            Check(restoredCoins.Any(coin => coin.Address == change1001) && restoredCoins.Any(coin => coin.Address == address10000), "words restore finds funded change and receiving index 10000");
            using WalletRpcClient deeper = new();
            await deeper.RestoreWalletAsync(endpoint, "words-deeper", "fixture-passphrase", words, 123, 12001);
            Check((await deeper.GetHistoryPageAsync(endpoint, search: deepAddress)).Entries.Any(coin => coin.Address == deepAddress), "explicit deeper restore finds funded index12001 history and scans despite old restoreHeight argument");
            Check(await deeper.CheckRecoveryWordsAsync(endpoint, words), "restored wallet retains the same recovery words");
            }
            bool boundaryRejected = false;
            try { await original.CreateReceivingAddressAsync(endpoint); } catch (InvalidOperationException exception) { boundaryRejected = exception.Message.Contains("recovery window"); }
            Check(boundaryRejected, "new receiving address beyond default recovery window is blocked");

            using WalletRpcClient many = new();
            await many.CreateWalletAsync(endpoint, "many-outputs", "");
            string address = (await many.CreateReceivingAddressAsync(endpoint, "Many outputs")).Address;
            string scriptHex = (await Call("", "validateaddress", address)).GetProperty("scriptPubKey").GetString()!;
            Script script = Script.FromHex(scriptHex);
            for (int batch = 0; batch < 2; batch++)
            {
                Transaction transaction = Transaction.Create(Network.RegTest);
                transaction.Version = 2;
                JsonElement fundingCoin = (await Call("funding", "listunspent", 1)).EnumerateArray().First(coin => coin.GetProperty("amount").GetDecimal() > 2m);
                // A real input avoids NBitcoin's special zero-input transaction encoding.
                transaction.Inputs.Add(new TxIn(new OutPoint(uint256.Parse(fundingCoin.GetProperty("txid").GetString()!), fundingCoin.GetProperty("vout").GetUInt32())));
                for (int index = 0; index < 1100; index++) transaction.Outputs.Add(Money.Coins(0.001m), script);
                JsonElement funded = await Call("funding", "fundrawtransaction", transaction.ToHex(), new { fee_rate = 1m });
                JsonElement signed = await Call("funding", "signrawtransactionwithwallet", funded.GetProperty("hex").GetString()!);
                await Call("", "sendrawtransaction", signed.GetProperty("hex").GetString()!);
            }
            await Mine(1);
            IReadOnlyList<WalletUnspentOutput> coins = await many.GetSpendableOutputsAsync(endpoint);
            Check(coins.Count == 2200 && coins.All(coin => coin.Address == address), "one wallet address holds2200 spendable outputs");
            WalletHistoryPage first = await many.GetHistoryPageAsync(endpoint);
            WalletHistoryPage second = await many.GetHistoryPageAsync(endpoint, 1);
            Check(first.Entries.Count == 50 && first.HasMore && second.Entries.Count == 50, "history pages beyond the previous20-entry limit");
            string exported = await many.ExportHistoryCsvAsync(endpoint, address);
            Check(exported.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length == 2201, "full filtered CSV includes all2200 received outputs");
            PreparedTransfer automatic = await many.PrepareTransferAsync(endpoint, recipient, 130_000_000);
            Check(automatic.InputCount > 675, "automatic payment selects more than675 inputs without an address limit");
            await many.RelayTransferAsync(endpoint, automatic);
            TransferResult retry = await many.RelayTransferAsync(endpoint, automatic);
            Check(retry.TransactionHash == automatic.TransactionHash, "retry relays the same signed transaction");
            await Mine(1);
            coins = await many.GetSpendableOutputsAsync(endpoint);
            WalletOutpoint[] selected = coins.Where(coin => coin.Amount == 0.001m).Take(3).Select(coin => coin.Outpoint).ToArray();
            PreparedTransfer controlled = await many.PrepareTransferAsync(endpoint, recipient, 150_000, selected);
            JsonElement controlledRaw = await Call("", "decoderawtransaction", controlled.TransactionMetadata);
            Check(controlled.InputCount == 3 && controlledRaw.GetProperty("vin").GetArrayLength() == 3, "coin-control payment uses exactly three selected inputs");
            await many.RelayTransferAsync(endpoint, controlled);
            await Mine(1);
            coins = await many.GetSpendableOutputsAsync(endpoint);
            WalletOutpoint[] batchInputs = coins.Where(coin => coin.Amount == 0.001m).Take(200).Select(coin => coin.Outpoint).ToArray();
            PreparedTransfer consolidation = await many.PrepareConsolidationAsync(endpoint, batchInputs);
            Check(consolidation.IsConsolidation && consolidation.InputCount == 200 && consolidation.AmountAtomic + consolidation.FeeAtomic == 20_000_000, "consolidation preview subtracts the exact fee from200 selected inputs");
            await many.RelayTransferAsync(endpoint, consolidation);
            await Mine(1);
            WalletTransactionDetails consolidated = await many.GetTransactionDetailsAsync(endpoint, consolidation.TransactionHash);
            Check(consolidated.Outputs.Count == 1 && consolidated.Outputs[0].IsMine, "consolidation creates one new wallet-owned output");
            Check((await many.GetReceivingAddressesAsync(endpoint)).Any(item => item.Address == consolidation.RecipientAddress && item.Used), "receiving address history includes consolidation destination");
            bool oversized = false;
            try { await many.PrepareConsolidationAsync(endpoint, coins.Take(201).Select(coin => coin.Outpoint).ToArray()); } catch (ArgumentException) { oversized = true; }
            Check(oversized, "consolidation batch201 is refused before preparation");
            Check((await Call("", "getconnectioncount")).GetInt32() == 0, "all transactions stayed on the disconnected fixture chain");
            return checks;
        }
        finally
        {
            try { await Call("", "stop"); } catch { }
            if (!process.HasExited && !process.WaitForExit(15000)) process.Kill(true);
            await stdout;
            await stderr;
            int exportIndex = Array.IndexOf(args, "--export-interop");
            if (interoperability is not null && exportIndex >= 0)
            {
                string export = Path.GetFullPath(args[exportIndex + 1]);
                if (Directory.Exists(export)) throw new IOException("Interop output already exists.");
                Directory.CreateDirectory(export);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(export, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                await File.WriteAllTextAsync(Path.Combine(export, "fixture.json"), JsonSerializer.Serialize(interoperability));
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Path.Combine(export, "fixture.json"), UnixFileMode.UserRead | UnixFileMode.UserWrite);
                foreach (string folder in new[] { "blocks", "chainstate" }) CopyDirectory(Path.Combine(directory, "regtest", folder), Path.Combine(export, "regtest", folder));
                Console.WriteLine("Private disconnected interoperability fixture written. It contains disposable test words.");
            }
            // Leave failed fixtures in the system temp directory for local diagnosis.
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (string folder in Directory.EnumerateDirectories(source)) CopyDirectory(folder, Path.Combine(destination, Path.GetFileName(folder)));
    }
}
