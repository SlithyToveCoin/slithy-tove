using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Slithy_Tove;

internal static class Program
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Assembly App = typeof(Form1).Assembly;
    private static readonly Uri LocalAddress = new("http://fixture:fixture@127.0.0.1:1");
    private static int checks;
    private static readonly List<string> failures = [];

    [STAThread]
    private static void Main(string[] args)
    {
        bool dpi96 = args.Contains("--dpi96", StringComparer.Ordinal);
        if (dpi96)
        {
            // Configure the fresh test process before WinForms caches its DPI.
            Check(Application.SetHighDpiMode(HighDpiMode.DpiUnaware), "The 96-DPI test process was configured before creating controls");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetDefaultFont(new Font("Segoe UI", 9F));
        }
        else ApplicationConfiguration.Initialize();
        string temporary = Path.Combine(Path.GetTempPath(), "slithy-startup-ui-" + Guid.NewGuid().ToString("N"));
        string? screenshots = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal));
        if (screenshots is not null) Directory.CreateDirectory(screenshots);
        Form1? form = null;
        try
        {
            TestRpcReplies();
            form = CreateForm(Path.Combine(temporary, "behavior"));
            form.Show();
            Pump(50);
            if (dpi96) Check(form.DeviceDpi == 96, "The fresh process actually created 96-DPI controls");
            TestProgress(form);
            TestWarmupAndRefresh(form);
            TestCancellation(form);
            TestFatalReplies(form);
            TestPreviouslyBusyWallet(form);
            form.Hide();
            if (screenshots is not null) TestLayouts(temporary, screenshots);
            if (failures.Count > 0)
                throw new InvalidOperationException($"{failures.Count} of {checks} checks failed:\n" + string.Join("\n", failures));
            Console.WriteLine($"Passed {checks} checks. HTTP replies and settings were isolated. No wallet or native node was opened.");
        }
        finally
        {
            if (form is not null)
            {
                object? diagnostics = Field(form, "_diagnosticLog");
                form.Dispose();
                if (diagnostics is not null)
                {
                    // The writer can outlive the controls. Finish it before removing its files.
                    using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
                    Task drain = (Task)diagnostics.GetType().GetMethod("FlushAsync", Hidden)!
                        .Invoke(diagnostics, [timeout.Token])!;
                    Run(drain.WaitAsync(timeout.Token));
                }
            }
            // Every file in this randomly named test directory belongs to this run.
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }

    private static void TestRpcReplies()
    {
        using IDisposable rpc = (IDisposable)Activator.CreateInstance(App.GetType("Slithy_Tove.NodeRpcClient")!)!;
        ReplaceHttp(rpc, new Handler(async (request, _) =>
        {
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Check(body.RootElement.GetProperty("method").GetString() == "getblockchaininfo",
                "Startup checks use the chain-info RPC");
            Check(body.RootElement.GetProperty("params").GetArrayLength() == 0,
                "Startup checks do not send wallet arguments");
            return Ready();
        }));
        Check(Run(StartupStage(rpc)) is null, "A ready node returns no loading stage");

        (string raw, string expected)[] stages =
        [
            ("Loading block index...", "Checking saved block headers..."),
            ("Verifying blocks...", "Checking recent blocks..."),
            ("Replaying blocks...", "Recovering saved chain data..."),
            ("Loading wallet private-wallet-name", "Preparing the wallet service..."),
            ("Loading P2P addresses...", "Preparing network connections..."),
            ("Loading banlist...", "Preparing network connections..."),
            ("Starting network threads...", "Preparing network connections..."),
            ("Done loading", "Connecting to the wallet service..."),
            ("Unexpected secret /private/path/token", "Starting the local Slithy node...")
        ];
        foreach ((string raw, string expected) in stages)
        {
            ReplaceHttp(rpc, new Handler((_, _) => Task.FromResult(RpcError(-28, raw))));
            Check(Run(StartupStage(rpc)) == expected, "Warmup uses a safe caption: " + expected);
        }
        foreach (HttpStatusCode status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden })
        {
            ReplaceHttp(rpc, new Handler((_, _) => Task.FromResult(new HttpResponseMessage(status))));
            Exception error = Failure(StartupStage(rpc));
            Check(error is InvalidOperationException && error.Message.Contains("credentials", StringComparison.Ordinal),
                "HTTP " + (int)status + " reports mismatched credentials, not a loading stage");
        }
        ReplaceHttp(rpc, new Handler((_, _) => Task.FromResult(RpcError(-32603, "Loading failed"))));
        Exception fatal = Failure(StartupStage(rpc));
        Check(fatal.GetType().Name == "NodeRpcException" &&
            (int)fatal.GetType().GetProperty("Code")!.GetValue(fatal)! == -32603,
            "A non-warmup RPC error retains its numeric code");
        ReplaceHttp(rpc, new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect))));
        Check(Failure(StartupStage(rpc)) is HttpRequestException, "A redirect is rejected during startup");
        ReplaceHttp(rpc, new Handler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Ready();
        }));
        using CancellationTokenSource cancelled = new();
        Task<string?> pending = StartupStage(rpc, cancelled.Token);
        cancelled.Cancel();
        Check(Failure(pending) is OperationCanceledException, "Cancellation reaches the pending RPC request");
    }

    private static void TestProgress(Form1 form)
    {
        Control panel = Control(form, "_operationProgressPanel");
        Control track = Control(form, "_operationProgressTrack");
        Control fill = Control(form, "_operationProgressFill");
        Label caption = (Label)Control(form, "_operationProgressLabel");
        Control main = Control(form, "mainTabControl");
        Call(form, "SetWalletBusy", true, "Reading saved blocks...", -1);
        Pump(50);
        Check(panel.Visible && Indeterminate(track) && !fill.Visible,
            "An unknown loading duration uses a moving bar without a made-up percentage");
        Check(track.AccessibleRole == AccessibleRole.ProgressBar &&
            track.AccessibleDescription == "Reading saved blocks...", "The loading stage is available to screen readers");
        System.Windows.Forms.Timer animation = (System.Windows.Forms.Timer)Field(track, "_animation")!;
        Check(animation.Enabled, "The loading animation runs while its bar is visible");
        int initialFrame = (int)Field(track, "_frame")!;
        Rectangle[] initialBounds = [panel.Bounds, track.Bounds, fill.Bounds, caption.Bounds, main.Bounds];
        int layouts = 0, textChanges = 0;
        panel.Layout += CountLayout;
        main.SizeChanged += CountLayout;
        caption.TextChanged += CountText;
        for (int repeat = 0; repeat < 20; repeat++)
            Call(form, "SetWalletBusy", true, "Reading saved blocks...", -1);
        bool dispatched = false;
        form.BeginInvoke(() => dispatched = true);
        Pump(360);
        Check((int)Field(track, "_frame")! != initialFrame && dispatched,
            "The animation advances while the window handles posted UI work");
        Check(layouts == 0 && textChanges == 0, "Animation and repeated stages do not relayout the footer or rewrite its text");
        Check(initialBounds.SequenceEqual(new[] { panel.Bounds, track.Bounds, fill.Bounds, caption.Bounds, main.Bounds }),
            "Animation does not move or resize the page, caption, or progress controls");
        panel.Layout -= CountLayout;
        main.SizeChanged -= CountLayout;
        caption.TextChanged -= CountText;
        Call(form, "SetWalletBusy", true, "Checking wallet history...", 37);
        Check(!Indeterminate(track) && !animation.Enabled && fill.Visible && fill.Width == track.ClientSize.Width * 37 / 100,
            "A measured 37 percent replaces the moving bar and stops its timer");
        Call(form, "SetWalletBusy", false, null, -1);
        Check(!panel.Visible && !Indeterminate(track) && !animation.Enabled && !form.UseWaitCursor,
            "Finishing wallet work hides and stops the loading bar");
        Call(form, "SetWalletBusy", true, "Reading saved blocks...", -1);
        form.Hide();
        Pump(40);
        Check(!animation.Enabled, "A hidden window does not animate its loading bar");
        form.Show();
        Pump(40);
        Check(animation.Enabled, "Showing a busy window resumes the loading bar");
        Set(form, "_installingUpdate", true);
        Call(form, "SetWalletBusy", true, "Preparing the update.", -1);
        Call(form, "ShowUpdatePage");
        Call(form, "SetUpdateProgress", "Downloading update... 37%", 37);
        Pump(30);
        Check(!panel.Visible && !Indeterminate(track) && !animation.Enabled,
            "An app update does not add a second animated footer bar");
        Control updateTrack = Control(form, "updateProgressTrackPanel");
        Check(Control(form, "updateProgressFillPanel").Width == updateTrack.ClientSize.Width * 37 / 100,
            "The Updates card still displays its measured download progress");
        Set(form, "_installingUpdate", false);
        Call(form, "SetWalletBusy", false, null, -1);
        ((TabControl)Control(form, "mainTabControl")).SelectedIndex = 0;

        void CountLayout(object? sender, EventArgs e) => layouts++;
        void CountText(object? sender, EventArgs e) => textChanges++;
    }

    private static void TestWarmupAndRefresh(Form1 form)
    {
        object rpc = Field(form, "_rpcClient")!;
        bool ready = false;
        int requests = 0;
        ReplaceHttp(rpc, new Handler((_, _) =>
        {
            requests++;
            return Task.FromResult(ready ? Ready() : RpcError(-28, "Loading block index..."));
        }));
        Set(form, "_walletOpening", true);
        Task wait = (Task)Call(form, "WaitForLocalNodeAsync")!;
        Pump(30);
        Check(!wait.IsCompleted && (bool)Field(form, "_nodeStartupInProgress")!,
            "Warmup leaves startup running instead of reporting a connection failure");
        Check(Control(form, "_cancelNodeStartupButton").Visible && Control(form, "_cancelNodeStartupButton").Enabled,
            "A wallet startup exposes an enabled Cancel button");
        Check(Control(form, "_operationProgressLabel").Text == "Checking saved block headers..." &&
            Control(form, "connectionStatusLabel").Text == "Loading wallet",
            "The footer and connection state describe the current loading stage");
        ComboBox mode = (ComboBox)Control(form, "connectionModeComboBox");
        ComboBox node = (ComboBox)Control(form, "officialNodeComboBox");
        Check(!mode.Enabled && !node.Enabled && !Control(form, "nodeAddressTextBox").Enabled,
            "Connection controls stay disabled during wallet startup");
        object settings = Field(form, "_settings")!;
        string savedSettings = JsonSerializer.Serialize(settings, settings.GetType());
        int modeIndex = mode.SelectedIndex, nodeIndex = node.SelectedIndex;
        int beforeSelection = requests;
        mode.SelectedIndex = modeIndex == 0 ? 1 : 0;
        node.SelectedIndex = nodeIndex == 0 ? 1 : 0;
        Check(savedSettings == JsonSerializer.Serialize(settings, settings.GetType()) && requests == beforeSelection,
            "Queued selector changes cannot restart the node or save another preference during startup");
        mode.SelectedIndex = modeIndex;
        node.SelectedIndex = nodeIndex;
        int beforeRefresh = requests;
        Task<bool> refresh = (Task<bool>)Call(form, "RefreshNodeStatusAsync")!;
        Check(!Run(refresh) && requests == beforeRefresh && !(bool)Field(form, "_refreshing")!,
            "A background refresh makes no RPC requests while startup is in progress");
        Check(Control(form, "connectionStatusLabel").Text == "Loading wallet",
            "Background refresh cannot replace loading with Everything is ready");
        bool responsive = false;
        form.BeginInvoke(() => responsive = true);
        Pump(80);
        Check(responsive && !wait.IsCompleted, "The UI remains responsive while waiting on a warm node");
        ready = true;
        Run(wait);
        Check(!(bool)Field(form, "_nodeStartupInProgress")! && Field(form, "_nodeStartupCancellation") is null &&
            !Control(form, "_cancelNodeStartupButton").Visible,
            "Ready startup clears its cancellation state and hides Cancel");
        Check(!Control(form, "_operationProgressPanel").Visible && !(bool)Field(form, "_walletBusy")!,
            "Startup releases the busy state it created");
        Set(form, "_walletOpening", false);
    }

    private static void TestCancellation(Form1 form)
    {
        int requests = 0;
        bool httpCancelled = false;
        ReplaceHttp(Field(form, "_rpcClient")!, new Handler(async (_, token) =>
        {
            requests++;
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { httpCancelled = true; throw; }
            return Ready();
        }));
        Set(form, "_walletOpening", true);
        Set(form, "_walletStartupCancelled", false);
        Task wait = (Task)Call(form, "WaitForLocalNodeAsync")!;
        Pump(20);
        Button cancel = (Button)Control(form, "_cancelNodeStartupButton");
        Check(cancel.AccessibleName == "Cancel opening the wallet", "Cancel has a specific accessible name");
        cancel.PerformClick();
        Check(Failure(wait) is OperationCanceledException && httpCancelled && requests == 1,
            "Cancel interrupts a pending RPC request without retrying it");
        Check((bool)Field(form, "_walletStartupCancelled")! &&
            !(bool)Field(form, "_nodeStartupInProgress")! && Field(form, "_nodeStartupCancellation") is null,
            "Cancelled startup records the user's choice and clears its running state");
        Check(!cancel.Visible && !Control(form, "_operationProgressPanel").Visible &&
            !Indeterminate(Control(form, "_operationProgressTrack")),
            "Cancellation removes both the button and loading animation");
        Set(form, "_walletOpening", false);
        Set(form, "_walletStartupCancelled", false);
    }

    private static void TestFatalReplies(Form1 form)
    {
        foreach (int code in new[] { 401, 403, -32603 })
        {
            int requests = 0;
            ReplaceHttp(Field(form, "_rpcClient")!, new Handler((_, _) =>
            {
                requests++;
                return Task.FromResult(code > 0 ? new HttpResponseMessage((HttpStatusCode)code) : RpcError(code, "Database unavailable"));
            }));
            Task wait = (Task)Call(form, "WaitForLocalNodeAsync")!;
            Check(Failure(wait) is InvalidOperationException && requests == 1,
                "Fatal startup response " + code + " is reported immediately without a retry loop");
            Check(!(bool)Field(form, "_nodeStartupInProgress")! &&
                !Control(form, "_operationProgressPanel").Visible && !Control(form, "_cancelNodeStartupButton").Visible,
                "Fatal startup response " + code + " clears its UI state");
        }
    }

    private static void TestPreviouslyBusyWallet(Form1 form)
    {
        ReplaceHttp(Field(form, "_rpcClient")!, new Handler((_, _) => Task.FromResult(Ready())));
        Call(form, "SetWalletBusy", true, "Opening the wallet...", -1);
        Run((Task)Call(form, "WaitForLocalNodeAsync")!);
        Check((bool)Field(form, "_walletBusy")! && Control(form, "_operationProgressPanel").Visible &&
            !Control(form, "walletButton").Enabled,
            "Node readiness does not unlock controls owned by the ongoing wallet operation");
        Call(form, "SetWalletBusy", false, null, -1);
    }

    private static void TestLayouts(string temporary, string screenshots)
    {
        using Form1 form = CreateForm(Path.Combine(temporary, "layout"));
        form.Show();
        Pump(30);
        int percentage = (int)Math.Round(form.DeviceDpi * 100f / 96f);
        Call(form, "SetWalletBusy", true, "Checking saved block headers...", -1);
        Control(form, "_cancelNodeStartupButton").Visible = true;
        Pump(50);
        CheckFooterBounds(form, percentage + " percent");
        Capture(form, Path.Combine(screenshots, "startup-" + percentage + ".png"));
        form.Size = form.MinimumSize;
        Pump(50);
        CheckFooterBounds(form, percentage + " percent minimum window");
        Capture(form, Path.Combine(screenshots, "startup-" + percentage + "-minimum.png"));
        Console.WriteLine($"LAYOUT {percentage}%: actual form DPI {form.DeviceDpi}; minimum {form.Width}x{form.Height}");
        form.Hide();
    }

    private static void CheckFooterBounds(Form1 form, string scenario)
    {
        Control panel = Control(form, "_operationProgressPanel");
        Control track = Control(form, "_operationProgressTrack");
        Control caption = Control(form, "_operationProgressLabel");
        Control cancel = Control(form, "_cancelNodeStartupButton");
        Check(panel.Visible && panel.ClientRectangle.Contains(track.Bounds) && panel.ClientRectangle.Contains(caption.Bounds) &&
            panel.ClientRectangle.Contains(cancel.Bounds), "Footer content fits at " + scenario);
        Check(!track.Bounds.IntersectsWith(caption.Bounds) && !caption.Bounds.IntersectsWith(cancel.Bounds) &&
            !track.Bounds.IntersectsWith(cancel.Bounds), "Progress, caption, and Cancel do not overlap at " + scenario);
        Check(!panel.Bounds.IntersectsWith(Control(form, "mainTabControl").Bounds), "The footer does not cover the page at " + scenario);
        Size required = TextRenderer.MeasureText(caption.Text, caption.Font, Size.Empty, TextFormatFlags.SingleLine);
        Console.WriteLine($"METRICS {scenario}: caption text {required}, space {caption.ClientSize}, padding {caption.Padding}");
        Check(required.Width <= caption.ClientSize.Width - caption.Padding.Horizontal &&
            required.Height <= caption.ClientSize.Height - caption.Padding.Vertical,
            "The full startup caption fits at " + scenario);
        Size cancelText = TextRenderer.MeasureText(cancel.Text, cancel.Font, Size.Empty, TextFormatFlags.SingleLine);
        Size cancelPreferred = cancel.GetPreferredSize(Size.Empty);
        Console.WriteLine($"METRICS {scenario}: cancel text {cancelText}, preferred {cancelPreferred}, space {cancel.ClientSize}");
        // Button layout adds space around its text, even without a painted border.
        Check(cancelText.Width <= cancel.ClientSize.Width && cancelPreferred.Height <= cancel.Height,
            "Cancel text and its internal inset fit at " + scenario);
    }

    private static Form1 CreateForm(string root)
    {
        object store = Activator.CreateInstance(App.GetType("Slithy_Tove.AppSettingsStore")!, [root])!;
        Form1 form = (Form1)Activator.CreateInstance(typeof(Form1), Hidden, null, [store], null)!;
        // Showing the real form must not start its normal app workflow.
        form.Shown -= (EventHandler)typeof(Form1).GetMethod("Form1_Shown", Hidden)!.CreateDelegate(typeof(EventHandler), form);
        ((System.Windows.Forms.Timer)Field(form, "statusTimer")!).Stop();
        ((System.Windows.Forms.Timer)Field(form, "_updateCheckTimer")!).Stop();
        ((NotifyIcon)Field(form, "_trayIcon")!).Visible = false;
        foreach (string service in new[] { "_rpcClient", "_updateService" })
            ReplaceHttp(Field(form, service)!, new Handler((_, _) => throw new InvalidOperationException("Unexpected HTTP request from " + service)));
        ReplaceHttp(Field(Field(form, "_walletRpcClient")!, "_rpc")!,
            new Handler((_, _) => throw new InvalidOperationException("Unexpected wallet HTTP request")));
        ReplaceHttp(Field(Field(form, "_localNode")!, "_rpcClient")!,
            new Handler((_, _) => throw new InvalidOperationException("Unexpected native-manager HTTP request")));
        Check(!(bool)Field(form, "_localNode")!.GetType().GetProperty("IsRunning")!.GetValue(Field(form, "_localNode"))!,
            "The isolated form has no native node process");
        return form;
    }

    private static HttpResponseMessage Ready() => new(HttpStatusCode.OK)
        { Content = new StringContent("{\"result\":{},\"error\":null,\"id\":\"1\"}") };
    private static HttpResponseMessage RpcError(int code, string message) => new(HttpStatusCode.InternalServerError)
        { Content = new StringContent(JsonSerializer.Serialize(new { result = (object?)null, error = new { code, message }, id = "1" })) };
    private static Task<string?> StartupStage(object rpc, CancellationToken token = default) =>
        (Task<string?>)rpc.GetType().GetMethod("GetStartupStageAsync")!.Invoke(rpc, [LocalAddress, token])!;
    private static object? Field(object target, string name) => target.GetType().GetField(name, Hidden)!.GetValue(target);
    private static Control Control(Form1 form, string name) => (Control)Field(form, name)!;
    private static void Set(Form1 form, string name, object? value) => typeof(Form1).GetField(name, Hidden)!.SetValue(form, value);
    private static object? Call(Form1 form, string name, params object?[] arguments) => typeof(Form1).GetMethod(name, Hidden)!.Invoke(form, arguments);
    private static bool Indeterminate(Control track) => (bool)track.GetType().GetProperty("Indeterminate", Hidden)!.GetValue(track)!;
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
    private static void Run(Task task)
    {
        Stopwatch clock = Stopwatch.StartNew();
        while (!task.IsCompleted && clock.Elapsed < TimeSpan.FromSeconds(10)) Pump(1);
        if (!task.IsCompleted) throw new TimeoutException("The isolated UI test did not finish.");
        task.GetAwaiter().GetResult();
    }
    private static T Run<T>(Task<T> task) { Run((Task)task); return task.GetAwaiter().GetResult(); }
    private static Exception Failure(Task task)
    {
        try { Run(task); }
        catch (Exception error) { return error; }
        throw new InvalidOperationException("The request unexpectedly succeeded.");
    }
    private static void Capture(Form form, string path)
    {
        using Bitmap bitmap = new(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path);
    }
    private static void Check(bool passed, string message)
    {
        checks++;
        if (!passed) failures.Add(message);
        Console.WriteLine((passed ? "PASS " : "FAIL ") + message);
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => reply(request, token);
    }
}
