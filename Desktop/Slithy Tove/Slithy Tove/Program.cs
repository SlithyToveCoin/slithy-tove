//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Main Program
//===============================================

namespace Slithy_Tove
{
    // Runs tests and updates and then launches the GUI.
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // The updater starts the same exe with special arguments so it can replace files.
            if (args.Contains("--apply-update", StringComparer.OrdinalIgnoreCase))
            {
                string installer = RequiredArgument(args, "--installer=");
                string pending = RequiredArgument(args, "--pending=");
                int parent = int.Parse(RequiredArgument(args, "--parent="));
                Environment.ExitCode = UpdateInstaller.Apply(installer, pending, parent);
                return;
            }
            if (args.Contains("--self-test-bootstrap-node", StringComparer.OrdinalIgnoreCase))
            {
                string? fixture = Environment.GetEnvironmentVariable("SLITHY_FIXTURE_RPC");
                if (!Uri.TryCreate(fixture, UriKind.Absolute, out Uri? endpoint) ||
                    !endpoint.IsLoopback || endpoint.Scheme != Uri.UriSchemeHttp)
                    throw new InvalidOperationException("This check requires a disposable loopback RPC fixture.");
                using NodeRpcClient node = new();
                NodeInfo info = node.GetInfoAsync(endpoint, TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                bool expected = Environment.GetEnvironmentVariable("SLITHY_FIXTURE_READY") == "true";
                Environment.ExitCode = info.Synchronized == expected ? 0 : 1;
                Console.WriteLine($"Bootstrap readiness: {info.Synchronized}; expected: {expected}.");
                return;
            }
            if (args.Contains("--self-test-network-reset", StringComparer.OrdinalIgnoreCase))
            {
                Environment.ExitCode = NetworkResetSelfTest.Run() ? 0 : 1;
                return;
            }
            if (args.Contains("--self-test-update-helper", StringComparer.OrdinalIgnoreCase))
            {
                bool passed = UpdateInstaller.RunHelperSelfTest();
                Console.WriteLine(passed ? "Copied update runtime test passed." : "Copied update runtime test failed.");
                Environment.ExitCode = passed ? 0 : 1;
                return;
            }
            // These self-test commands check app behavior without opening a real wallet.
            if (args.Contains("--self-test-updates", StringComparer.OrdinalIgnoreCase))
            {
                Environment.ExitCode = UpdateSecuritySelfTest.Run() ? 0 : 1;
                return;
            }
            if (args.Contains("--self-test-update-feed", StringComparer.OrdinalIgnoreCase))
            {
                using UpdateService service = new(Path.Combine(
                    Path.GetTempPath(),
                    "slithy-update-feed-test"));
                UpdateCheckResult result = service.CheckAsync(
                    new Uri(AppSettings.DefaultUpdateManifestUrl),
                    ReleaseTrust.UpdatePublicKeyPem,
                    new Version(0, 0, 0)).GetAwaiter().GetResult();
                bool passed = result.UpdateAvailable &&
                    result.Manifest is not null &&
                    Version.Parse(result.Manifest.Version) >= new Version(0, 1, 1);
                Console.WriteLine(passed
                    ? $"Signed Slithy update {result.Manifest!.Version} verified."
                    : "Slithy update feed verification failed.");
                Environment.ExitCode = passed ? 0 : 1;
                return;
            }
            if (args.Contains("--self-test-update-rollback", StringComparer.OrdinalIgnoreCase))
            {
                Environment.ExitCode = UpdateInstaller.RunRollbackSelfTest() ? 0 : 1;
                return;
            }
            if (args.Contains("--self-test-wallet-restore", StringComparer.OrdinalIgnoreCase))
            {
                Environment.ExitCode = WalletBackupTools.RunSelfTest() ? 0 : 1;
                return;
            }
            if (args.Contains("--self-test-wallet-rpc", StringComparer.OrdinalIgnoreCase))
            {
                Environment.ExitCode = WalletRpcTransportSelfTest.RunAsync().GetAwaiter().GetResult() ? 0 : 1;
                return;
            }
            if (args.Contains("--self-test-seed-wallet", StringComparer.OrdinalIgnoreCase))
            {
                Environment.ExitCode = SeedWalletSelfTest.RunAsync().GetAwaiter().GetResult() ? 0 : 1;
                return;
            }
            if (args.Contains("--self-test-child-job", StringComparer.OrdinalIgnoreCase))
            {
                Environment.ExitCode = ChildProcessJob.RunSelfTest() ? 0 : 1;
                return;
            }
            if (args.Contains("--self-test-mining-rpc", StringComparer.OrdinalIgnoreCase))
            {
                Environment.ExitCode = MiningRpcSelfTest.RunAsync().GetAwaiter().GetResult() ? 0 : 1;
                return;
            }
            if (args.Contains("--self-test-send-receive", StringComparer.OrdinalIgnoreCase))
            {
                Environment.ExitCode = SendReceiveSelfTest.RunAsync().GetAwaiter().GetResult() ? 0 : 1;
                return;
            }
            if (args.Contains("--self-test-treasury", StringComparer.OrdinalIgnoreCase))
            {
                TreasuryStatus status = TreasuryStatusCalculator.FromChainHeight(872);
                bool passed = status.Accrued == 872.000000000000m
                    && status.ChainHeight == 872
                    && status.Address == TreasuryStatusCalculator.DevelopmentTreasuryAddress;
                Console.WriteLine(passed
                    ? "Treasury status calculation passed."
                    : $"Treasury status calculation failed: {status.Accrued} SLTHY");
                Environment.ExitCode = passed ? 0 : 1;
                return;
            }
            if (args.Contains("--self-test-node-rpc", StringComparer.OrdinalIgnoreCase))
            {
                Environment.ExitCode = PublicNodeRpcSelfTest.RunAsync().GetAwaiter().GetResult() ? 0 : 1;
                return;
            }
            if (args.Contains("--self-test-existing-wallet", StringComparer.OrdinalIgnoreCase))
            {
                string walletName = RequiredArgument(args, "--wallet-name=");
                string passwordEnv = RequiredArgument(args, "--wallet-password-env=");
                string? walletPassword = Environment.GetEnvironmentVariable(passwordEnv);
                if (string.IsNullOrEmpty(walletPassword))
                {
                    Console.WriteLine("Wallet password environment variable was empty.");
                    Environment.ExitCode = 1;
                    return;
                }

                Environment.ExitCode = ExistingWalletOpenSelfTest.RunAsync(walletName, walletPassword).GetAwaiter().GetResult() ? 0 : 1;
                return;
            }

            // Normal app startup begins here.
            ApplicationConfiguration.Initialize();

            // One copy of the app should run at a time. If a second copy opens,
            // it tells the first copy to show itself instead of starting another wallet.
            using Mutex singleInstance = new(
                initiallyOwned: true,
                name: "SlithyTove.Desktop.SingleInstance",
                createdNew: out bool createdNew);
            using EventWaitHandle showExistingInstance = new(
                initialState: false,
                EventResetMode.AutoReset,
                "SlithyTove.Desktop.ShowExisting");
            using EventWaitHandle exitExistingInstance = new(
                initialState: false,
                EventResetMode.AutoReset,
                "SlithyTove.Desktop.ExitExisting");
            if (!createdNew)
            {
                if (args.Contains("--exit-existing", StringComparer.OrdinalIgnoreCase))
                {
                    exitExistingInstance.Set();
                }
                else
                {
                    showExistingInstance.Set();
                }
                return;
            }

            Form1 form;
            try
            {
                // Ask before Form1 can start the node or open a wallet.
                if (!TermsDialog.Confirm()) return;
                form = new Form1();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Slithy could not start. Your existing files have not been reset.\n\n{ex.Message}",
                    "Startup needs attention", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Environment.ExitCode = 1;
                return;
            }
            using Form1 ownedForm = form;
            using ManualResetEvent stopSignals = new(false);

            // This background task waits for "show" or "exit" signals from a second launch.
            Task signalTask = Task.Run(() =>
            {
                WaitHandle[] signals = [showExistingInstance, exitExistingInstance, stopSignals];
                while (true)
                {
                    int signal = WaitHandle.WaitAny(signals);
                    if (signal == 2 || form.IsDisposed)
                    {
                        return;
                    }
                    try { form.BeginInvoke(signal == 0
                        ? form.RestoreFromTray
                        : form.ExitFromTray); }
                    catch (InvalidOperationException) { return; }
                }
            });
            try { Application.Run(form); }
            finally { stopSignals.Set(); signalTask.GetAwaiter().GetResult(); }
        }

        private static string RequiredArgument(string[] args, string prefix)
        {
            // Helper for command line options like --pending=C:\path\file.json.
            string? value = args.FirstOrDefault(
                argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (value is null || value.Length == prefix.Length)
            {
                throw new ArgumentException($"Missing required argument {prefix}");
            }
            return value[prefix.Length..];
        }
    }
}

