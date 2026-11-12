using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Slithy_Tove;

namespace Slithy.Linux;

// All replies come from memory. These checks never start a node or open a real wallet.
internal static class UiMiningStatusSelfTest
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        using var fixture = new Fixture();
        MainWindow window = fixture.Window;
        RpcReplies replies = fixture.Replies;
        replies.FailedMethod = "getbalances";
        RunTask(Invoke(window, "Refresh"));
        Require(Text(window, "_miningTitle") == "Mining is on", "a failed balance read preserves confirmed mining");
        Require(Text(window, "_available").Contains("Last balance shown") &&
            Text(window, "_walletStatus").Contains("Balance update delayed"), "the old balance is marked as stale");
        Require(Text(window, "_balance") == "9.00000000 SLTHY" &&
            ((WalletSnapshot)Get(window, "_snapshot")!).Balance == 9m, "a failed refresh does not replace the balance");
        Require((string?)((Button)Get(window, "_miningButton")!).Content == "Stop mining", "mining can still be stopped after a balance failure");

        Set(window, "_snapshot", null);
        RunTask(Invoke(window, "Refresh"));
        Require(Text(window, "_balance") == "Balance unavailable" && !Text(window, "_available").Contains("Last balance"),
            "a first failed balance read does not invent a cached balance");

        replies.FailedMethod = null;
        replies.Trusted = 7.00000002m;
        replies.Immature = 3.12345678m;
        replies.Pending = 0.00000001m;
        int balanceReads = replies.BalanceReads;
        RunTask(Invoke(window, "Refresh"));
        Require(!Text(window, "_walletStatus").Contains("delayed") &&
            !Text(window, "_available").Contains("Last balance"), "a fresh balance clears the stale notice");
        var fresh = (WalletSnapshot)Get(window, "_snapshot")!;
        Require(replies.BalanceReads == balanceReads + 1 && fresh.BalanceAtomic == 1_012_345_681 &&
            fresh.UnlockedBalanceAtomic == 700_000_002 && fresh.ImmatureBalanceAtomic == 312_345_678 && fresh.PendingBalanceAtomic == 1,
            "one balance reply supplies exact total, spendable, immature and pending amounts");
        Require(Text(window, "_available").Contains(fresh.ImmatureBalance.ToString("N8")) &&
            Text(window, "_available").Contains(fresh.PendingBalance.ToString("N8")), "the displayed breakdown comes from that snapshot");

        // A failed single read must preserve the whole previous snapshot.
        var prior = (WalletSnapshot)Get(window, "_snapshot")!;
        replies.FailBalanceRead = replies.BalanceReads + 1;
        replies.Trusted = 25m;
        RunTask(Invoke(window, "Refresh"));
        Require(ReferenceEquals(Get(window, "_snapshot"), prior) && Text(window, "_available").Contains("Last balance"),
            "the balance and its breakdown are committed together");
        replies.FailBalanceRead = 0;

        replies.FailedMethod = "getcpumininginfo";
        RunTask(Invoke(window, "Refresh"));
        Require(Text(window, "_miningTitle") == "Checking miner" &&
            Text(window, "_miningStatus").Contains("unknown"), "a failed mining reply reports unknown state");
        Require((bool)Get(window, "_miningStatusUnknown")! && ((Button)Get(window, "_miningButton")!).IsEnabled,
            "unknown mining state leaves a stop request available");
        int startCalls = replies.StartCalls;
        RunTask(Invoke(window, "ToggleMining"));
        Require(replies.StopCalls == 1 && replies.StartCalls == startCalls,
            "a stop request is sent even when mining status cannot be read");

        replies.FailedMethod = null;
        replies.Active = false;
        RunTask(Invoke(window, "Refresh"));
        Require(!(bool)Get(window, "_miningStatusUnknown")! && Text(window, "_miningTitle") == "Earn Slithy",
            "a fresh stopped reply clears unknown state");

        replies.Active = true;
        replies.FailedMethod = "getblockchaininfo";
        RunTask(Invoke(window, "Refresh"));
        Require(Text(window, "_miningTitle") == "Mining is on" && window.DisplayedStatus.Contains("Network status"),
            "a failed network read does not suppress a successful mining reply");
        Require(Get(window, "_chain") == null && !Text(window, "_miningStatus").Contains("Next block") &&
            ((ProgressBar)Get(window, "_sync")!).IsIndeterminate,
            "a failed network read does not present the prior chain height as current");
        replies.FailedMethod = null;

        var readStorage = (Func<LinuxStorageInfo>)Get(window, "_readStorage")!;
        try
        {
            Set(window, "_readStorage", (Func<LinuxStorageInfo>)(() => throw new IOException("Injected disk read failure.")));
            RunTask(Invoke(window, "Refresh"));
            Require(Text(window, "_storage").Contains("could not be read") && Text(window, "_miningTitle") == "Mining is on",
                "a disk check failure does not erase confirmed mining");
        }
        finally { Set(window, "_readStorage", readStorage); }
        Require(!MainWindow.MiningStatusText(new MiningInfo(true, 25, 2, "fixture-address", "yespower", 1), null).Contains("Next block"),
            "an unavailable chain height does not invent a next block number");

        int before = replies.MiningReads;
        replies.MiningGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task poll = Invoke(window, "Poll");
        PumpUntil(() => replies.MiningReads == before + 1);
        RunTask(Invoke(window, "Poll"));
        Require(replies.MiningReads == before + 1, "a second timer tick does not overlap a running poll");
        replies.MiningGate.SetResult();
        RunTask(poll);
        replies.MiningGate = null;
        Require(!(bool)Get(window, "_polling")!, "the poll gate is released after a delayed reply");

        foreach (string guard in new[] { "_busy", "_closing" })
        {
            before = replies.MiningReads;
            Set(window, guard, true);
            RunTask(Invoke(window, "Poll"));
            Require(replies.MiningReads == before, "polling waits while " + guard.TrimStart('_'));
            Set(window, guard, false);
        }
        Require(replies.Methods.All(method => method != "rescanblockchain"), "routine Linux polling does not launch a wallet rescan");
        foreach (string method in new[] { "getblockchaininfo", "getcpumininginfo", "getbalances" })
        {
            CheckStopInterruptsPoll(method, lateFailure: false);
            CheckStopInterruptsPoll(method, lateFailure: true);
        }
        CheckLostMiningReply(stopping: false);
        CheckLostMiningReply(stopping: true);
        Console.WriteLine("PASS: Linux mining and balance failures stay separate; stale balances, stop requests and serial polling are covered.");
    }

    private static void CheckLostMiningReply(bool stopping)
    {
        using var fixture = new Fixture();
        MainWindow window = fixture.Window;
        RpcReplies replies = fixture.Replies;
        replies.Active = stopping;
        RunTask(Invoke(window, "Refresh"));
        replies.FailedMethod = stopping ? "stopcpumining" : "startcpumining";
        bool failed = false;
        try { RunTask(Invoke(window, "ToggleMining")); }
        catch (HttpRequestException) { failed = true; }
        Require(failed && (bool)Get(window, "_miningStatusUnknown")! &&
            Text(window, "_miningStatus").Contains("not confirmed") &&
            (string?)((Button)Get(window, "_miningButton")!).Content == "Stop mining" &&
            ((Button)Get(window, "_miningButton")!).IsEnabled,
            "a lost " + (stopping ? "stop" : "start") + " reply leaves an unconfirmed state and an available Stop");
        Require(stopping ? !replies.Active : replies.Active, "the fixture applied the command before losing its reply");
        int starts = replies.StartCalls;
        replies.FailedMethod = null;
        RunTask(Invoke(window, "ToggleMining"));
        Require(replies.StartCalls == starts && !replies.Active && !(bool)Get(window, "_miningStatusUnknown")!,
            "the next click after a lost reply sends Stop, never a second Start");
    }

    private static void CheckStopInterruptsPoll(string method, bool lateFailure)
    {
        using var fixture = new Fixture();
        MainWindow window = fixture.Window;
        RpcReplies replies = fixture.Replies;
        RunTask(Invoke(window, "Refresh"));
        var previousSnapshot = Get(window, "_snapshot");
        string previousBalance = Text(window, "_balance");
        string previousAvailable = Text(window, "_available");
        replies.Trusted = 999m;
        replies.HeldMethod = method;
        replies.HeldGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        replies.ThrowAfterHeldReply = lateFailure;
        Task poll = Invoke(window, "Poll");
        PumpUntil(() => replies.HeldEntered == 1);
        // Preview prevents real user actions. This fixture has fake RPC and is never shown.
        Set(window, "_preview", false);
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Task stop = Invoke(window, "HandleMiningClickAsync");
            RunTask(stop);
            Require(watch.Elapsed < TimeSpan.FromSeconds(2) && !replies.HeldGate.Task.IsCompleted && replies.StopCalls == 1,
                "Stop interrupts " + method + " without waiting for its reply or timeout");
            RunTask(poll);
            Require(!(bool)Get(window, "_polling")! && !(bool)Get(window, "_miningStatusUnknown")! &&
                !((MiningInfo)Get(window, "_mining")!).Active && Text(window, "_miningTitle") == "Earn Slithy",
                "a confirmed stop releases the polling gate and immediately shows stopped state");
            replies.HeldGate.SetResult();
            PumpUntil(() => replies.HeldReturned == 1);
            Require(!((MiningInfo)Get(window, "_mining")!).Active && Text(window, "_miningTitle") == "Earn Slithy" &&
                (string?)((Button)Get(window, "_miningButton")!).Content == "Start mining",
                "a late " + method + " reply cannot overwrite the stop acknowledgement");
            Require(ReferenceEquals(Get(window, "_snapshot"), previousSnapshot) && Text(window, "_balance") == previousBalance &&
                Text(window, "_available") == previousAvailable && !Text(window, "_log").Contains("refresh failed"),
                "cancelled polling does not replace the balance or report a connection failure");
        }
        finally
        {
            replies.HeldGate.TrySetResult();
            Set(window, "_preview", true);
        }
    }

    private static string Text(MainWindow window, string field) => ((TextBlock)Get(window, field)!).Text ?? "";
    private static object? Get(object target, string field) => target.GetType().GetField(field, Private)!.GetValue(target);
    private static void Set(object target, string field, object? value) => target.GetType().GetField(field, Private)!.SetValue(target, value);
    private static Task Invoke(MainWindow window, string method) => (Task)typeof(MainWindow).GetMethod(method, Private)!.Invoke(window, null)!;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void PumpUntil(Func<bool> finished)
    {
        var stop = DateTime.UtcNow.AddSeconds(10);
        while (!finished())
        {
            if (DateTime.UtcNow >= stop) throw new TimeoutException("The isolated polling check did not finish.");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }
        Dispatcher.UIThread.RunJobs();
    }
    private static void RunTask(Task task) { PumpUntil(() => task.IsCompleted); task.GetAwaiter().GetResult(); }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "slithy-mining-status-" + Guid.NewGuid().ToString("N"));
        public RpcReplies Replies { get; } = new();
        public MainWindow Window { get; }
        public Fixture()
        {
            Directory.CreateDirectory(_root);
            Window = new MainWindow(preview: true, store: new LinuxSettingsStore(_root));
            Set(Window, "_endpoint", new Uri("http://127.0.0.1:1/"));
            Set(Window, "_ready", true);
            Set(Window, "_walletName", "fixture");
            Set(Window, "_historyBusy", true);
            Set(Window, "_lastTreasury", DateTimeOffset.UtcNow);
            Set(Window, "_readStorage", (Func<LinuxStorageInfo>)(() => new LinuxStorageInfo(10L * 1073741824, 0, _root)));
            Set(Window, "_snapshot", new WalletSnapshot("fixture-address", 900_000_000, 900_000_000, 0, 100));
            ((TextBlock)Get(Window, "_balance")!).Text = "9.00000000 SLTHY";
            var wallet = (WalletRpcClient)Get(Window, "_wallet")!;
            Set(wallet, "_walletName", "fixture"); Set(wallet, "_currentAddress", "fixture-address");
            ReplaceHttp((NodeRpcClient)Get(Window, "_node")!);
            ReplaceHttp((NodeRpcClient)Get(wallet, "_rpc")!);
        }
        private void ReplaceHttp(NodeRpcClient rpc)
        {
            ((HttpClient)Get(rpc, "_httpClient")!).Dispose();
            Set(rpc, "_httpClient", new HttpClient(Replies, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan });
        }
        public void Dispose()
        {
            ((NodeRpcClient)Get(Window, "_node")!).Dispose();
            ((WalletRpcClient)Get(Window, "_wallet")!).Dispose();
            Window.Close();
            Replies.Dispose();
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class RpcReplies : HttpMessageHandler
    {
        public string? FailedMethod;
        public bool Active = true;
        public decimal Trusted = 7m;
        public decimal Immature = 3m, Pending;
        public int MiningReads, BalanceReads, FailBalanceRead, StopCalls, StartCalls;
        public TaskCompletionSource? MiningGate;
        public string? HeldMethod;
        public TaskCompletionSource? HeldGate;
        public int HeldEntered, HeldReturned;
        public bool ThrowAfterHeldReply;
        public List<string> Methods { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            string method = body.RootElement.GetProperty("method").GetString()!;
            Methods.Add(method);
            bool activeAtRequest = Active;
            decimal trustedAtRequest = Trusted;
            if (method == HeldMethod && HeldGate != null)
            {
                HeldEntered++;
                // Deliberately ignore cancellation to test replies arriving after Stop.
                await HeldGate.Task;
                HeldReturned++;
                if (ThrowAfterHeldReply) throw new HttpRequestException("Injected late failure.");
            }
            if (method == "getcpumininginfo")
            {
                MiningReads++;
                if (MiningGate != null) await MiningGate.Task.WaitAsync(cancellationToken);
            }
            if (method == "getbalances") BalanceReads++;
            if (method == "stopcpumining") { StopCalls++; Active = false; }
            if (method == "startcpumining") { StartCalls++; Active = true; }
            if (method == FailedMethod || (method == "getbalances" && BalanceReads == FailBalanceRead))
                throw new HttpRequestException("Injected reply failure.");
            object result = method switch
            {
                "getblockchaininfo" => new { chain = "test", blocks = 100, headers = 100, initialblockdownload = false, difficulty = 1 },
                "getnetworkinfo" => new { subversion = "/fixture/", connections = 1 },
                "getmempoolinfo" => new { size = 0 },
                "getpeerinfo" => new[] { new { version = 70016, synced_headers = 100, synced_blocks = 100, startingheight = 100 } },
                "getcpumininginfo" => new { active = activeAtRequest, speed = 25, threads = 2, address = "fixture-address", algorithm = "yespower", blocks = 1 },
                "getbalances" => new { mine = new { trusted = trustedAtRequest, immature = Immature, untrusted_pending = Pending }, lastprocessedblock = new { height = 100 } },
                "validateaddress" => new { isvalid = true, address = "fixture-address" },
                "stopcpumining" or "startcpumining" => new { active = Active },
                _ => throw new InvalidOperationException("Unexpected fixture method: " + method)
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { result, error = (object?)null, id = 1 }), Encoding.UTF8, "application/json")
            };
        }
    }
}
