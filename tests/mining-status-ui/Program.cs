using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Slithy_Tove;

internal static class Program
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Assembly App = typeof(Form1).Assembly;
    private static readonly List<string> Failures = [];
    private static int checks;
    private static int fixtureNumber;
    private static string temporary = "";

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        temporary = Path.Combine(Path.GetTempPath(), "slithy-mining-status-ui-" + Guid.NewGuid().ToString("N"));
        try
        {
            TestWalletFailureDoesNotChangeMining();
            TestSnapshotUsesOneBalanceReply();
            TestBalanceAtUnchangedHeight();
            TestOldBalanceReply();
            TestMiningFailureAllowsStop();
            TestMiningTimeoutRecovers();
            TestLostStartReply();
            TestMalformedStartReply();
            TestLostStopReply();
            TestOrdinaryWalletRefresh();
            TestOldOrdinaryWalletReplies();
            TestMiningRequestsDoNotOverlap();
            TestStatusTicksDoNotOverlap();
            TestOldRepliesAfterStateChange();
            TestEntryGuards();
            TestGuardDuringRequest();
            TestHeldStartupGate();
            TestCompletedWalletActionInvalidatesPoll();
            if (Failures.Count > 0)
                throw new InvalidOperationException($"{Failures.Count} of {checks} checks failed:\n" + string.Join("\n", Failures));
            Console.WriteLine($"Passed {checks} checks. Replies and settings were isolated. No native node or real wallet was opened.");
        }
        finally
        {
            // This directory was created for this run and never contains user data.
            string path = Path.GetFullPath(temporary);
            string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(parent, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(path).StartsWith("slithy-mining-status-ui-", StringComparison.Ordinal))
                throw new InvalidOperationException("The test cleanup path is outside its temporary directory.");
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
    }

    private static void TestSnapshotUsesOneBalanceReply()
    {
        using Fixture fixture = new();
        fixture.OpenFakeWallet();
        int balanceCalls = 0;
        fixture.WalletReply = (method, _) =>
        {
            if (method != "getbalances") throw new InvalidOperationException("Unexpected snapshot method: " + method);
            balanceCalls++;
            return Task.FromResult(Reply(new
            {
                mine = new { trusted = 1.23456789m, immature = 9.00000001m, untrusted_pending = 0.00000002m },
                lastprocessedblock = new { height = 42 }
            }));
        };
        object wallet = Field(fixture.Form, "_walletRpcClient")!;
        Task task = (Task)wallet.GetType().GetMethod("GetSnapshotAsync")!.Invoke(wallet,
            [new Uri("http://127.0.0.1:1"), CancellationToken.None])!;
        Run(task);
        object snapshot = task.GetType().GetProperty("Result")!.GetValue(task)!;
        T Value<T>(string name) => (T)snapshot.GetType().GetProperty(name)!.GetValue(snapshot)!;
        Check(balanceCalls == 1, "The snapshot reads all balance categories from one getbalances reply");
        Check(Value<ulong>("BalanceAtomic") == 1_023_456_792UL && Value<decimal>("Balance") == 10.23456792m,
            "The total includes spendable, immature and pending amounts without rounding away small values");
        Check(Value<ulong>("UnlockedBalanceAtomic") == 123_456_789UL && Value<decimal>("UnlockedBalance") == 1.23456789m,
            "The snapshot preserves the exact spendable amount");
        Check(Value<ulong>("ImmatureBalanceAtomic") == 900_000_001UL && Value<decimal>("ImmatureBalance") == 9.00000001m,
            "The snapshot preserves the exact immature amount");
        Check(Value<ulong>("PendingBalanceAtomic") == 2UL && Value<decimal>("PendingBalance") == 0.00000002m,
            "The snapshot preserves the exact pending amount");
        Check(Value<long>("LastProcessedBlockHeight") == 42 && Value<long>("BlocksToUnlock") == -1,
            "The snapshot uses the matching balance height without inventing a maturity countdown");
    }

    private static void TestWalletFailureDoesNotChangeMining()
    {
        foreach (bool transportFailure in new[] { false, true })
        {
            using Fixture fixture = new();
            Form1 form = fixture.Form;
            fixture.OpenFakeWallet();
            int balanceCalls = 0, rescans = 0;
            fixture.WalletReply = (method, _) =>
            {
                if (method == "rescanblockchain")
                {
                    rescans++;
                    return Task.FromResult(Reply(new { start_height = 101, stop_height = 102 }));
                }
                if (method != "getbalances") throw new InvalidOperationException("Unexpected wallet method: " + method);
                balanceCalls++;
                if (transportFailure) throw new HttpRequestException("The disposable balance request failed.");
                return Task.FromResult(RpcError(-13, "The disposable wallet could not refresh its balance."));
            };
            Run(CallTask(form, "RefreshMiningStatusAsync"));
            string scenario = transportFailure ? "transport error" : "wallet RPC error";
            Check(balanceCalls == 1, scenario + " was injected into the balance request");
            Check(rescans == 0, "Routine mining refresh does not rescan the chain after " + scenario);
            Check(Control(form, "miningTitleLabel").Text == "Mining is on" &&
                Control(form, "miningDescriptionLabel").Text.Contains("321", StringComparison.Ordinal),
                "Confirmed mining and hash rate survive a " + scenario);
            Check((bool)Field(form, "_miningStartedByApp")! && MiningAnimation(form),
                "The confirmed running state survives a " + scenario);
            Check(Control(form, "miningToggleButton").Text == "Stop mining" && Control(form, "miningToggleButton").Enabled,
                "Stop remains available after a " + scenario);
            Check(Control(form, "balanceValueLabel").Text == "9.0000 SLTHY",
                "A failed balance refresh leaves the displayed balance intact");
            Check(Control(form, "logTextBox").Text.Contains("Mining balance refresh failed", StringComparison.Ordinal) &&
                !Control(form, "logTextBox").Text.Contains("Mining status request", StringComparison.Ordinal),
                "A " + scenario + " is logged as wallet work, not a failed mining status request");
            Check(!(bool)Field(form, "_miningStatusRefreshing")!, "The mining refresh guard is released after " + scenario);
        }
    }

    private static void TestBalanceAtUnchangedHeight()
    {
        using Fixture fixture = new();
        fixture.OpenFakeWallet();
        int balances = 0, nodeCalls = 0;
        fixture.NodeReply = (method, _) =>
        {
            nodeCalls++;
            if (method != "getcpumininginfo") throw new InvalidOperationException("Balance refresh unexpectedly requested " + method);
            return Task.FromResult(MiningReply(active: true));
        };
        fixture.WalletReply = (method, _) =>
        {
            if (method != "getbalances") throw new InvalidOperationException("Balance refresh unexpectedly requested " + method);
            balances++;
            return Task.FromResult(BalanceReply(pending: 2m));
        };
        Run(CallTask(fixture.Form, "RefreshMiningStatusAsync"));
        Check(balances == 1 && nodeCalls == 1, "Mining refresh reads balances without a rescan or separate height query");
        Check(Control(fixture.Form, "balanceValueLabel").Text == "11.0000 SLTHY" &&
            (long)Field(fixture.Form, "_lastWalletRefreshHeight")! == 100,
            "A pending payment updates the balance even when the chain height has not changed");
        Check(Field(fixture.Form, "_lastMiningRewardUtc") is null,
            "A pending incoming payment is not described as a mining reward");
        Check(!(bool)Field(fixture.Form, "_walletRefreshing")!, "A successful balance read releases the wallet guard");
    }

    private static void TestOldBalanceReply()
    {
        foreach (bool walletChanged in new[] { false, true })
        {
            using Fixture fixture = new();
            fixture.OpenFakeWallet();
            TaskCompletionSource<HttpResponseMessage> pending = PendingReply();
            int balanceCalls = 0;
            fixture.WalletReply = (_, token) => { balanceCalls++; return pending.Task.WaitAsync(token); };
            Task refresh = CallTask(fixture.Form, "RefreshMiningStatusAsync");
            Pump(10);
            Check((bool)Field(fixture.Form, "_walletRefreshing")!, "A pending balance request owns the wallet refresh guard");
            Run((Task)Call(fixture.Form, "RefreshWalletAsync", false, false, null)!);
            Check(balanceCalls == 1, "An ordinary wallet refresh cannot overlap a mining balance request");
            if (walletChanged) Set(fixture.Form, "_openWalletName", "next-disposable-wallet");
            else Set(fixture.Form, "_miningStateVersion", (long)Field(fixture.Form, "_miningStateVersion")! + 1);
            Control(fixture.Form, "balanceValueLabel").Text = "Newer balance";
            pending.SetResult(BalanceReply(pending: 2m));
            Run(refresh);
            Check(Control(fixture.Form, "balanceValueLabel").Text == "Newer balance",
                "A balance reply from before a " + (walletChanged ? "wallet switch" : "mining state change") + " is discarded");
            object settings = Field(fixture.Form, "_settings")!;
            object cache = settings.GetType().GetProperty("WalletBalanceCache")!.GetValue(settings)!;
            Check((int)cache.GetType().GetProperty("Count")!.GetValue(cache)! == 0,
                "A discarded balance is not saved into the wallet cache");
            Check(!(bool)Field(fixture.Form, "_walletRefreshing")!, "A discarded balance reply releases the wallet guard");
        }
    }

    private static void TestMiningFailureAllowsStop()
    {
        foreach (bool previouslyConfirmedActive in new[] { false, true })
        {
            using Fixture fixture = new();
            Form1 form = fixture.Form;
            Set(form, "_miningStartedByApp", previouslyConfirmedActive);
            if (previouslyConfirmedActive) Call(form, "ShowMiningStarted", 7, 321UL);
            fixture.NodeReply = (method, _) => method == "getcpumininginfo"
                ? throw new HttpRequestException("The disposable mining status request failed.")
                : Task.FromResult(NodeReply(method));
            Run(CallTask(form, "RefreshMiningStatusAsync"));
            Check(Control(form, "miningTitleLabel").Text == "Checking miner" &&
                Control(form, "miningDescriptionLabel").Text.Contains("unknown", StringComparison.OrdinalIgnoreCase),
                "A failed mining RPC reports an unknown state");
            Check(Control(form, "miningToggleButton").Text == "Stop mining" && Control(form, "miningToggleButton").Enabled,
                "An unknown mining state still has an enabled Stop button");
            Check(!MiningAnimation(form), "An unknown state does not animate as confirmed mining");
            Check(Control(form, "logTextBox").Text.Contains("Mining status request (getcpumininginfo) failed", StringComparison.Ordinal),
                "A failed mining request is identified in the technical log");
            int stops = 0, balances = 0, rescans = 0;
            if (previouslyConfirmedActive) fixture.OpenFakeWallet();
            fixture.WalletReply = (method, _) =>
            {
                switch (method)
                {
                    case "getbalances": balances++; return Task.FromResult(BalanceReply(pending: 0m));
                    case "listtransactions": return Task.FromResult(Reply(Array.Empty<object>()));
                    case "rescanblockchain":
                        rescans++;
                        throw new InvalidOperationException("Stopping mining must not rescan the wallet.");
                    default: throw new InvalidOperationException("Unexpected post-stop wallet method: " + method);
                }
            };
            fixture.NodeReply = (method, _) =>
            {
                if (method != "stopcpumining") throw new InvalidOperationException("Unexpected stop-path method: " + method);
                stops++;
                return Task.FromResult(Reply(new { active = false }));
            };
            ((Button)Control(form, "miningToggleButton")).PerformClick();
            Until(() => !(bool)Field(form, "_miningChangeInProgress")!);
            Check(stops == 1 && !(bool)Field(form, "_miningStartedByApp")! &&
                Control(form, "miningToggleButton").Text == "Start mining",
                "Clicking Stop after an unknown state sends one stop request and shows its confirmed result");
            Check(rescans == 0 && balances == (previouslyConfirmedActive ? 1 : 0),
                "Stopping mining reads an open wallet balance without a chain rescan");
        }
    }

    private static void TestMiningTimeoutRecovers()
    {
        using Fixture fixture = new();
        bool cancelled = false;
        fixture.NodeReply = async (_, token) =>
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { cancelled = true; throw; }
            return MiningReply(active: true);
        };
        Run(CallTask(fixture.Form, "RefreshMiningStatusAsync"));
        Check(cancelled && (bool)Field(fixture.Form, "_miningStatusUnknown")! &&
            Control(fixture.Form, "miningTitleLabel").Text == "Checking miner",
            "The real five-second RPC deadline produces an unknown state without an unhandled exception");
        fixture.NodeReply = (method, _) => Task.FromResult(NodeReply(method));
        Run(CallTask(fixture.Form, "RefreshMiningStatusAsync"));
        Check(!(bool)Field(fixture.Form, "_miningStatusUnknown")! &&
            Control(fixture.Form, "miningTitleLabel").Text == "Mining is on" && MiningAnimation(fixture.Form),
            "A successful retry clears the timeout state and restores confirmed mining");
        Check(Field(fixture.Form, "_lastMiningStatusUtc") is DateTimeOffset,
            "The latest successful mining reply has a recorded time");
    }

    private static void TestMiningRequestsDoNotOverlap()
    {
        using Fixture fixture = new();
        TaskCompletionSource<HttpResponseMessage> pending = PendingReply();
        int requests = 0;
        fixture.NodeReply = (method, token) =>
        {
            if (method != "getcpumininginfo") throw new InvalidOperationException("Unexpected poll method: " + method);
            requests++;
            return pending.Task.WaitAsync(token);
        };
        Task first = CallTask(fixture.Form, "RefreshMiningStatusAsync");
        Pump(20);
        Task second = CallTask(fixture.Form, "RefreshMiningStatusAsync");
        Run(second);
        Check(requests == 1 && !first.IsCompleted && (bool)Field(fixture.Form, "_miningStatusRefreshing")!,
            "A second mining refresh returns without starting another request");
        pending.SetResult(MiningReply(active: true));
        Run(first);
        Check(!(bool)Field(fixture.Form, "_miningStatusRefreshing")!, "A completed mining reply releases its guard");
        fixture.NodeReply = (method, _) =>
        {
            requests++;
            return Task.FromResult(NodeReply(method));
        };
        Run(CallTask(fixture.Form, "RefreshMiningStatusAsync"));
        Check(requests == 2, "The next mining refresh runs after the preceding one finishes");
    }

    private static void TestLostStartReply()
    {
        foreach (Exception error in new Exception[] { new OperationCanceledException("The Start reply timed out."), new TimeoutException("The Start reply was lost.") })
        {
            using Fixture fixture = new();
            Form1 form = fixture.Form;
            string[] initial = CardText(form);
            Check(!(bool)Call(form, "TryShowUnconfirmedMiningChange", false, false, error)! && initial.SequenceEqual(CardText(form)),
                "A failure before Start was sent does not invent an unconfirmed mining command");
            Exception rejection = (Exception)Activator.CreateInstance(App.GetType("Slithy_Tove.NodeRpcException")!, [-5, "Mining address rejected."])!;
            Check(!(bool)Call(form, "TryShowUnconfirmedMiningChange", false, true, rejection)!,
                "An explicit rejected Start is not treated as a lost connection");
            Check((bool)Call(form, "TryShowUnconfirmedMiningChange", false, true, error)! &&
                (bool)Field(form, "_miningStatusUnknown")! && Control(form, "miningTitleLabel").Text == "Start not confirmed",
                error.GetType().Name + " after sending Start reports an unconfirmed outcome");
            Check(Field(form, "_walletSnapshot") is null && !(bool)Field(form, "_miningStartedByApp")! &&
                Control(form, "miningToggleButton").Enabled && Control(form, "miningToggleButton").Text == "Stop mining",
                "An unconfirmed Start permits Stop without an open wallet or previous active status");
            int miningCalls = 0, startCalls = 0;
            fixture.NodeReply = (method, _) =>
            {
                if (method == "startcpumining") startCalls++;
                if (method == "getcpumininginfo") miningCalls++;
                return Task.FromResult(NodeReply(method));
            };
            Run(CallTask(form, "RefreshStatusAsync"));
            Check(miningCalls == 1 && startCalls == 0 && !(bool)Field(form, "_miningStatusUnknown")! &&
                (bool)Field(form, "_miningStartedByApp")! && Control(form, "miningTitleLabel").Text == "Mining is on",
                "The timer resolves a lost Start reply through status polling without sending Start again");
        }
    }

    private static void TestMalformedStartReply()
    {
        foreach (string response in new[] { "{", "{\"error\":null}", "[]" })
        {
            using Fixture fixture = new();
            Form1 form = fixture.Form;
            int starts = 0, miningCalls = 0;
            bool running = false;
            fixture.NodeReply = (method, _) =>
            {
                if (method == "startcpumining")
                {
                    starts++;
                    running = true;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) });
                }
                if (method == "getcpumininginfo") { miningCalls++; return Task.FromResult(MiningReply(running)); }
                return Task.FromResult(NodeReply(method));
            };
            object node = Field(form, "_rpcClient")!;
            Task start = (Task)node.GetType().GetMethod("StartMiningAsync")!.Invoke(node,
                [new Uri("http://127.0.0.1:1"), "fixture-receiving-address", 1])!;
            Exception? failure = null;
            try { Run(start); }
            catch (Exception error) { failure = error; }
            Check(running && starts == 1 && failure is JsonException or KeyNotFoundException or InvalidOperationException,
                "The fake node accepted Start before returning an unreadable acknowledgement");
            Check(failure is not null && (bool)Call(form, "TryShowUnconfirmedMiningChange", false, true, failure)! &&
                (bool)Field(form, "_miningStatusUnknown")! && Control(form, "miningToggleButton").Text == "Stop mining" &&
                Control(form, "miningToggleButton").Enabled,
                "An unreadable Start acknowledgement leaves Stop available instead of offering another Start");
            Run(CallTask(form, "RefreshStatusAsync"));
            Check(starts == 1 && miningCalls == 1 && !(bool)Field(form, "_miningStatusUnknown")! &&
                (bool)Field(form, "_miningStartedByApp")! && Control(form, "miningTitleLabel").Text == "Mining is on",
                "The next status poll confirms mining after a malformed reply without resending Start");
        }
    }

    private static void TestLostStopReply()
    {
        using Fixture fixture = new();
        Form1 form = fixture.Form;
        Set(form, "_miningStartedByApp", true);
        Call(form, "ShowMiningStarted", 3, 321UL);
        int stops = 0, starts = 0, miningCalls = 0;
        fixture.NodeReply = (method, _) =>
        {
            if (method != "stopcpumining") throw new InvalidOperationException("Unexpected command during Stop: " + method);
            stops++;
            throw new TimeoutException("The Stop reply was lost.");
        };
        ((Button)Control(form, "miningToggleButton")).PerformClick();
        Until(() => !(bool)Field(form, "_miningChangeInProgress")!);
        Check(stops == 1 && (bool)Field(form, "_miningStatusUnknown")! &&
            Control(form, "miningTitleLabel").Text == "Stop not confirmed" && Control(form, "miningToggleButton").Enabled,
            "A timed-out Stop leaves its outcome unknown and permits another explicit stop request");
        fixture.NodeReply = (method, _) =>
        {
            if (method == "startcpumining") starts++;
            if (method == "stopcpumining") stops++;
            if (method == "getcpumininginfo")
            {
                miningCalls++;
                return Task.FromResult(MiningReply(active: false));
            }
            return Task.FromResult(NodeReply(method));
        };
        Run(CallTask(form, "RefreshStatusAsync"));
        Check(stops == 1 && starts == 0 && miningCalls == 1 && !(bool)Field(form, "_miningStatusUnknown")! &&
            !(bool)Field(form, "_miningStartedByApp")! && Control(form, "miningToggleButton").Text == "Start mining",
            "The timer confirms a lost Stop reply without resending either mining command");
    }

    private static void TestOrdinaryWalletRefresh()
    {
        using Fixture fixture = new();
        fixture.OpenFakeWallet();
        int balances = 0, histories = 0;
        fixture.WalletReply = (method, _) => method switch
        {
            "getbalances" => CountBalance(),
            "listtransactions" => CountHistory(),
            _ => throw new InvalidOperationException("Unexpected ordinary refresh method: " + method)
        };
        Task<HttpResponseMessage> CountBalance() { balances++; return Task.FromResult(BalanceReply(pending: 2m)); }
        Task<HttpResponseMessage> CountHistory() { histories++; return Task.FromResult(HistoryReply()); }
        Run((Task)Call(fixture.Form, "RefreshWalletAsync", false, false, null)!);
        Check(balances == 1 && histories == 1 && Control(fixture.Form, "balanceValueLabel").Text == "11.0000 SLTHY",
            "An ordinary refresh still applies its current balance without a rescan");
        Check(Control(fixture.Form, "_latestActivitySummaryLabel").Text.Contains("2.0000 SLTHY", StringComparison.Ordinal),
            "An ordinary refresh still displays its current transaction history");
        Check(CacheCount(fixture.Form) == 1 && !(bool)Field(fixture.Form, "_walletRefreshing")!,
            "A current ordinary balance is cached and its refresh guard is released");
    }

    private static void TestOldOrdinaryWalletReplies()
    {
        foreach (bool holdHistory in new[] { false, true })
        foreach (bool changeName in new[] { false, true })
        {
            using Fixture fixture = new();
            fixture.OpenFakeWallet();
            TaskCompletionSource<HttpResponseMessage> pending = PendingReply();
            int histories = 0;
            fixture.WalletReply = (method, token) =>
            {
                if (method == "getbalances") return holdHistory ? Task.FromResult(BalanceReply(pending: 2m)) : pending.Task.WaitAsync(token);
                if (method == "listtransactions")
                {
                    histories++;
                    return holdHistory ? pending.Task.WaitAsync(token) : Task.FromResult(HistoryReply());
                }
                throw new InvalidOperationException("Unexpected deferred wallet method: " + method);
            };
            Task refresh = (Task)Call(fixture.Form, "RefreshWalletAsync", false, false, null)!;
            Pump(10);
            Check(!refresh.IsCompleted && histories == (holdHistory ? 1 : 0),
                "The ordinary refresh is held at its " + (holdHistory ? "history" : "balance") + " reply");
            Set(fixture.Form, "_miningStateVersion", (long)Field(fixture.Form, "_miningStateVersion")! + 1);
            if (changeName) Set(fixture.Form, "_openWalletName", "next-disposable-wallet");
            object currentSnapshot = Field(fixture.Form, "_walletSnapshot")!;
            Control(fixture.Form, "balanceValueLabel").Text = "Current session balance";
            Control(fixture.Form, "_latestActivitySummaryLabel").Text = "Current session activity";
            int priorCacheEntries = CacheCount(fixture.Form);
            pending.SetResult(holdHistory ? HistoryReply() : BalanceReply(pending: 2m));
            Run(refresh);
            string transition = changeName ? "a different wallet" : "a close and reopen of the same wallet name";
            Check(Control(fixture.Form, "balanceValueLabel").Text == "Current session balance" &&
                Control(fixture.Form, "_latestActivitySummaryLabel").Text == "Current session activity" &&
                ReferenceEquals(currentSnapshot, Field(fixture.Form, "_walletSnapshot")),
                "A late " + (holdHistory ? "history" : "balance") + " reply is ignored after " + transition);
            Check(CacheCount(fixture.Form) == priorCacheEntries && histories == (holdHistory ? 1 : 0) &&
                !(bool)Field(fixture.Form, "_walletRefreshing")!,
                "An obsolete ordinary refresh does not continue into history or cache writes and releases its guard");
        }
    }

    private static void TestStatusTicksDoNotOverlap()
    {
        using Fixture fixture = new();
        Set(fixture.Form, "_miningStartedByApp", true);
        TaskCompletionSource<HttpResponseMessage> pending = PendingReply();
        int chainCalls = 0, miningCalls = 0;
        fixture.NodeReply = (method, token) =>
        {
            if (method == "getblockchaininfo" && ++chainCalls == 1) return pending.Task.WaitAsync(token);
            if (method == "getcpumininginfo") miningCalls++;
            return Task.FromResult(NodeReply(method));
        };
        Task first = CallTask(fixture.Form, "RefreshStatusAsync");
        Pump(20);
        Task second = CallTask(fixture.Form, "RefreshStatusAsync");
        Run(second);
        Check(chainCalls == 1 && miningCalls == 0 && !first.IsCompleted &&
            (bool)Field(fixture.Form, "_statusRefreshInProgress")!,
            "An overlapping status tick cannot skip ahead into mining or wallet work");
        pending.SetResult(NodeReply("getblockchaininfo"));
        Run(first);
        Check(miningCalls == 1 && !(bool)Field(fixture.Form, "_statusRefreshInProgress")!,
            "One completed status tick checks mining once and releases its guard");
    }

    private static void TestOldRepliesAfterStateChange()
    {
        foreach (bool newStateActive in new[] { false, true })
        foreach (bool failedReply in new[] { false, true })
        {
            using Fixture fixture = new();
            Form1 form = fixture.Form;
            Set(form, "_miningStartedByApp", !newStateActive);
            TaskCompletionSource<HttpResponseMessage> pending = PendingReply();
            fixture.NodeReply = (_, token) => pending.Task.WaitAsync(token);
            Task oldPoll = CallTask(form, "RefreshMiningStatusAsync");
            Pump(10);
            // Model a completed user action without starting an actual miner.
            Set(form, "_miningStateVersion", (long)Field(form, "_miningStateVersion")! + 1);
            Set(form, "_miningStartedByApp", newStateActive);
            if (newStateActive) Call(form, "ShowMiningStarted", 3, 777UL);
            else Call(form, "ShowMiningStopped");
            string[] confirmed = CardText(form);
            if (failedReply) pending.SetException(new HttpRequestException("An obsolete request failed."));
            else pending.SetResult(MiningReply(active: !newStateActive));
            Run(oldPoll);
            string action = newStateActive ? "start" : "stop";
            string result = failedReply ? "failure" : "reply";
            Check(confirmed.SequenceEqual(CardText(form)) && (bool)Field(form, "_miningStartedByApp")! == newStateActive,
                "A stale " + result + " cannot overwrite a completed " + action);
            Check(MiningAnimation(form) == newStateActive && !(bool)Field(form, "_miningStatusRefreshing")!,
                "A stale " + result + " leaves animation correct and releases the poll guard after " + action);
        }
    }

    private static void TestEntryGuards()
    {
        foreach (string guard in new[] { "_walletBusy", "_walletDialogOpen", "_walletOpening", "_nodeStartupInProgress", "_closingForExit", "_miningChangeInProgress", "_installingUpdate" })
        {
            using Fixture fixture = new();
            Set(fixture.Form, "_miningStartedByApp", true);
            Set(fixture.Form, guard, true);
            int requests = 0;
            fixture.NodeReply = (method, _) => { requests++; return Task.FromResult(NodeReply(method)); };
            string[] prior = CardText(fixture.Form);
            Run(CallTask(fixture.Form, "RefreshMiningStatusAsync"));
            Run(CallTask(fixture.Form, "RefreshStatusAsync"));
            Check(requests == 0 && prior.SequenceEqual(CardText(fixture.Form)),
                guard + " prevents background HTTP requests and mining card changes");
            Check(!(bool)Field(fixture.Form, "_miningStatusRefreshing")! &&
                !(bool)Field(fixture.Form, "_statusRefreshInProgress")!,
                guard + " does not leave a refresh guard stuck");
            Set(fixture.Form, guard, false);
        }
    }

    private static void TestGuardDuringRequest()
    {
        foreach (string guard in new[] { "_walletBusy", "_walletDialogOpen", "_walletOpening", "_nodeStartupInProgress", "_closingForExit", "_miningChangeInProgress", "_installingUpdate" })
        {
            using Fixture fixture = new();
            TaskCompletionSource<HttpResponseMessage> pending = PendingReply();
            fixture.NodeReply = (_, token) => pending.Task.WaitAsync(token);
            Task poll = CallTask(fixture.Form, "RefreshMiningStatusAsync");
            Pump(10);
            Set(fixture.Form, guard, true);
            string[] prior = CardText(fixture.Form);
            pending.SetResult(MiningReply(active: true));
            Run(poll);
            Check(prior.SequenceEqual(CardText(fixture.Form)), guard + " prevents a pending reply from changing the mining card");
            Check(!(bool)Field(fixture.Form, "_miningStatusRefreshing")!, guard + " still allows a pending refresh to release its guard");
            Set(fixture.Form, guard, false);
        }
    }

    private static void TestHeldStartupGate()
    {
        using Fixture fixture = new();
        SemaphoreSlim gate = (SemaphoreSlim)Field(fixture.Form, "_nodeStartupGate")!;
        int requests = 0;
        fixture.NodeReply = (method, _) => { requests++; return Task.FromResult(NodeReply(method)); };
        gate.Wait();
        try
        {
            Run(CallTask(fixture.Form, "RefreshMiningStatusAsync"));
            Run(CallTask(fixture.Form, "RefreshStatusAsync"));
            Check(requests == 0, "The held startup gate blocks both refresh entry points");
        }
        finally { gate.Release(); }
        TaskCompletionSource<HttpResponseMessage> pending = PendingReply();
        fixture.NodeReply = (_, token) => pending.Task.WaitAsync(token);
        Task poll = CallTask(fixture.Form, "RefreshMiningStatusAsync");
        Pump(10);
        gate.Wait();
        try
        {
            string[] prior = CardText(fixture.Form);
            pending.SetException(new HttpRequestException("Startup superseded this request."));
            Run(poll);
            Check(prior.SequenceEqual(CardText(fixture.Form)) && !(bool)Field(fixture.Form, "_miningStatusRefreshing")!,
                "Startup suppresses an in-flight failure without stranding its refresh guard");
        }
        finally { gate.Release(); }
    }

    private static void TestCompletedWalletActionInvalidatesPoll()
    {
        foreach (bool showDialog in new[] { false, true })
        {
            using Fixture fixture = new();
            TaskCompletionSource<HttpResponseMessage> pending = PendingReply();
            fixture.NodeReply = (_, token) => pending.Task.WaitAsync(token);
            Task oldPoll = CallTask(fixture.Form, "RefreshMiningStatusAsync");
            Pump(10);
            long priorVersion = (long)Field(fixture.Form, "_miningStateVersion")!;
            if (showDialog)
            {
                using Form dialog = new() { Text = "Disposable wallet dialog", ShowInTaskbar = false };
                dialog.Shown += (_, _) => dialog.BeginInvoke(() => dialog.Close());
                Call(fixture.Form, "ShowWalletDialog", dialog);
            }
            else
            {
                Call(fixture.Form, "SetWalletBusy", true, "Disposable wallet work", -1);
                Call(fixture.Form, "SetWalletBusy", false, null, -1);
            }
            string[] afterAction = CardText(fixture.Form);
            Check((long)Field(fixture.Form, "_miningStateVersion")! > priorVersion &&
                !(bool)Field(fixture.Form, showDialog ? "_walletDialogOpen" : "_walletBusy")!,
                "A completed " + (showDialog ? "wallet dialog" : "busy operation") + " advances the mining generation");
            pending.SetResult(MiningReply(active: true));
            Run(oldPoll);
            Check(afterAction.SequenceEqual(CardText(fixture.Form)),
                "A reply from before a completed " + (showDialog ? "wallet dialog" : "busy operation") + " cannot change the card");
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal Form1 Form { get; }
        internal Func<string, CancellationToken, Task<HttpResponseMessage>> NodeReply { get; set; } =
            (method, _) => Task.FromResult(Program.NodeReply(method));
        internal Func<string, CancellationToken, Task<HttpResponseMessage>> WalletReply { get; set; } =
            (method, _) => throw new InvalidOperationException("Unexpected wallet method: " + method);

        internal Fixture()
        {
            string directory = Path.Combine(temporary, (++fixtureNumber).ToString());
            object store = Activator.CreateInstance(App.GetType("Slithy_Tove.AppSettingsStore")!, [directory])!;
            Form = (Form1)Activator.CreateInstance(typeof(Form1), Hidden, null, [store], null)!;
            Form.Shown -= (EventHandler)typeof(Form1).GetMethod("Form1_Shown", Hidden)!.CreateDelegate(typeof(EventHandler), Form);
            ((System.Windows.Forms.Timer)Field(Form, "statusTimer")!).Stop();
            ((System.Windows.Forms.Timer)Field(Form, "_updateCheckTimer")!).Stop();
            ((NotifyIcon)Field(Form, "_trayIcon")!).Visible = false;
            object settings = Field(Form, "_settings")!;
            settings.GetType().GetProperty("TreasuryStatusUrl")!.SetValue(settings, "");
            settings.GetType().GetProperty("CheckForUpdatesAutomatically")!.SetValue(settings, false);
            settings.GetType().GetProperty("AutoLockEnabled")!.SetValue(settings, false);
            ReplaceHttp(Field(Form, "_rpcClient")!, new Handler((method, token) => NodeReply(method, token)));
            ReplaceHttp(Field(Field(Form, "_walletRpcClient")!, "_rpc")!, new Handler((method, token) => WalletReply(method, token)));
            foreach (object client in new[] { Field(Form, "_updateService")!, Field(Field(Form, "_localNode")!, "_rpcClient")! })
                ReplaceHttp(client, new Handler((method, _) => throw new InvalidOperationException("Unexpected background request: " + method)));
            Set(Form, "_lastDiskScanUtc", DateTime.UtcNow);
            Form.Show();
            Pump(5);
            Control(Form, "miningToggleButton").Enabled = true;
            Check(!NodeRunning(), "The fixture has no native node process");
        }

        internal void OpenFakeWallet()
        {
            object wallet = Field(Form, "_walletRpcClient")!;
            Set(wallet, "_walletName", "disposable-fixture");
            Set(wallet, "_currentAddress", "fixture-receiving-address");
            Set(Form, "_openWalletName", "disposable-fixture");
            object snapshot = Activator.CreateInstance(App.GetType("Slithy_Tove.WalletSnapshot")!,
                ["fixture-receiving-address", 900_000_000UL, 0UL, 100L, 100L, 900_000_000UL, 0UL])!;
            Set(Form, "_walletSnapshot", snapshot);
            Set(Form, "_lastWalletRefreshHeight", 100L);
            Set(Form, "_lastMiningWalletRefreshUtc", DateTimeOffset.MinValue);
            Control(Form, "balanceValueLabel").Text = "9.0000 SLTHY";
        }

        private bool NodeRunning() => (bool)Field(Form, "_localNode")!.GetType().GetProperty("IsRunning")!.GetValue(Field(Form, "_localNode"))!;
        public void Dispose()
        {
            Check(!NodeRunning(), "The fixture did not start a native node");
            // Dispose directly. Closing invokes the app's normal shutdown workflow.
            Form.Hide();
            Form.Dispose();
            if (Field(Form, "_diagnosticLog") is { } diagnostics)
                Run((Task)diagnostics.GetType().GetMethod("FlushAsync", Hidden)!.Invoke(diagnostics, [CancellationToken.None])!);
        }
    }

    private static HttpResponseMessage NodeReply(string method) => method switch
    {
        "getcpumininginfo" => MiningReply(active: true),
        "getblockchaininfo" => Reply(new { chain = "test", blocks = 102, headers = 102, initialblockdownload = false, difficulty = 1m }),
        "getnetworkinfo" => Reply(new { subversion = "/fixture/" }),
        "getmempoolinfo" => Reply(new { size = 0 }),
        _ => throw new InvalidOperationException("Unexpected node method: " + method)
    };

    private static HttpResponseMessage MiningReply(bool active) => Reply(new
        { active, speed = 321, threads = active ? 7 : 0, address = "fixture-receiving-address", algorithm = "yespower", blocks = 2 });
    private static HttpResponseMessage BalanceReply(decimal pending) => Reply(new
        { mine = new { trusted = 0m, immature = 9m, untrusted_pending = pending }, lastprocessedblock = new { height = 100 } });
    private static HttpResponseMessage HistoryReply() => Reply(new[]
        { new { txid = new string('a', 64), category = "receive", amount = 2m, confirmations = 1, blockheight = 100, address = "fixture-receiving-address" } });
    private static HttpResponseMessage Reply(object result) => new(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(new { result, error = (object?)null, id = "fixture" })) };
    private static HttpResponseMessage RpcError(int code, string message) => new(HttpStatusCode.InternalServerError)
        { Content = new StringContent(JsonSerializer.Serialize(new { result = (object?)null, error = new { code, message }, id = "fixture" })) };
    private static TaskCompletionSource<HttpResponseMessage> PendingReply() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static object? Field(object target, string name) => target.GetType().GetField(name, Hidden)!.GetValue(target);
    private static void Set(object target, string name, object? value) => target.GetType().GetField(name, Hidden)!.SetValue(target, value);
    private static object? Call(Form1 form, string name, params object?[] args) => typeof(Form1).GetMethod(name, Hidden)!.Invoke(form, args);
    private static Task CallTask(Form1 form, string name) => (Task)Call(form, name)!;
    private static Control Control(Form1 form, string name) => (Control)Field(form, name)!;
    private static int CacheCount(Form1 form)
    {
        object settings = Field(form, "_settings")!;
        object cache = settings.GetType().GetProperty("WalletBalanceCache")!.GetValue(settings)!;
        return (int)cache.GetType().GetProperty("Count")!.GetValue(cache)!;
    }
    private static bool MiningAnimation(Form1 form) => (bool)Field(form, "_bookMiningAnimation")!.GetType().GetProperty("Mining")!.GetValue(Field(form, "_bookMiningAnimation"))!;
    private static string[] CardText(Form1 form) => [Control(form, "miningTitleLabel").Text,
        Control(form, "miningDescriptionLabel").Text, Control(form, "miningStatusLineLabel").Text, Control(form, "miningToggleButton").Text];
    private static void ReplaceHttp(object service, HttpMessageHandler handler)
    {
        FieldInfo field = service.GetType().GetField("_httpClient", Hidden)!;
        ((HttpClient)field.GetValue(service)!).Dispose();
        field.SetValue(service, new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });
    }
    private static void Pump(int milliseconds)
    {
        Stopwatch clock = Stopwatch.StartNew();
        do { Application.DoEvents(); Thread.Sleep(1); } while (clock.ElapsedMilliseconds < milliseconds);
    }
    private static void Until(Func<bool> complete)
    {
        Stopwatch clock = Stopwatch.StartNew();
        while (!complete() && clock.Elapsed < TimeSpan.FromSeconds(10)) Pump(1);
        if (!complete()) throw new TimeoutException("The isolated mining UI test did not finish.");
    }
    private static void Run(Task task) { Until(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static void Check(bool passed, string message)
    {
        checks++;
        if (!passed) Failures.Add(message);
        Console.WriteLine((passed ? "PASS " : "FAIL ") + message);
    }
    private sealed class Handler(Func<string, CancellationToken, Task<HttpResponseMessage>> reply) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri is null || !request.RequestUri.IsLoopback)
                throw new InvalidOperationException("The fixture refused a non-loopback request.");
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            return await reply(body.RootElement.GetProperty("method").GetString()!, token);
        }
    }
}
