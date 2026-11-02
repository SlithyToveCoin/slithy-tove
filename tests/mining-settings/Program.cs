using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Slithy_Tove;

internal static class Program
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static readonly Assembly App = typeof(Form1).Assembly;

    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string root = Path.Combine(Path.GetTempPath(), "slithy-mining-settings-" + Guid.NewGuid().ToString("N"));
        string? screenshots = args.FirstOrDefault();
        if (screenshots != null) Directory.CreateDirectory(screenshots);
        try
        {
            Type settingsType = App.GetType("Slithy_Tove.AppSettings")!;
            MethodInfo clamp = settingsType.GetMethod("ClampMiningThreads", BindingFlags.Static | BindingFlags.NonPublic)!;
            foreach (int cpus in new[] { 1, 2, 7, 64, 256 })
            {
                Check((int)clamp.Invoke(null, [0, cpus])! == 1, "Zero is clamped to one on " + cpus + " CPUs");
                Check((int)clamp.Invoke(null, [cpus + 20, cpus])! == cpus, "Saved count is clamped to " + cpus);
                Check((int)clamp.Invoke(null, [cpus, cpus])! == cpus, "Maximum remains reachable: " + cpus);
                int chosen = Math.Max(1, cpus / 2);
                Check((int)clamp.Invoke(null, [chosen, cpus])! == chosen, "A valid saved choice stays unchanged on " + cpus + " CPUs");
            }
            Check((int)settingsType.GetProperty("MiningThreads")!.GetValue(Activator.CreateInstance(settingsType))! == 1,
                "New settings default to one thread");
            object store = Activator.CreateInstance(App.GetType("Slithy_Tove.AppSettingsStore")!, [root])!;
            string settingsPath = (string)store.GetType().GetProperty("SettingsPath", Hidden)!.GetValue(store)!;
            object firstLoad = store.GetType().GetMethod("Load")!.Invoke(store, null)!;
            Check((int)settingsType.GetProperty("MiningThreads")!.GetValue(firstLoad)! == 1,
                "A new installation defaults to one thread");
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, "{\"AutoLockMinutes\":17}");
            object legacy = store.GetType().GetMethod("Load")!.Invoke(store, null)!;
            Check((int)settingsType.GetProperty("MiningThreads")!.GetValue(legacy)! == 1,
                "Settings without a saved thread count default to one thread");
            Check((int)settingsType.GetProperty("AutoLockMinutes")!.GetValue(legacy)! == 17,
                "Defaulting the thread count leaves unrelated settings unchanged");
            using Form1 form = CreateForm(store);
            TrackBar slider = (TrackBar)Field(form, "miningThreadsTrackBar");
            Check(slider.Minimum == 1 && slider.Maximum == Math.Max(1, Environment.ProcessorCount), "Slider spans every available CPU thread");
            Check(slider.SmallChange == 1 && slider.LargeChange == 1, "Keyboard changes by one thread");
            Check(slider.Value == 1, "The slider starts at one thread when no count was saved");
            Check(!AllControls((Control)Field(form, "miningCard")).Any(c => c is ComboBox), "Home has no mining preset selector");
            Check(slider.AccessibleName == "CPU mining threads", "Slider has a screen-reader name");
            int exact = Math.Min(3, slider.Maximum);
            slider.Value = slider.Maximum;
            slider.Value = exact;
            Check((int)Call(form, "MiningThreadCount")! == exact, "Mining uses the exact slider value");
            object loaded = store.GetType().GetMethod("Load")!.Invoke(store, null)!;
            Check((int)settingsType.GetProperty("MiningThreads")!.GetValue(loaded)! == exact, "Exact thread count persists to disk");
            using (Form1 reopened = CreateForm(store))
                Check(((TrackBar)Field(reopened, "miningThreadsTrackBar")).Value == exact, "Reopening restores the saved count");
            foreach (int saved in new[] { 1, Math.Max(1, slider.Maximum / 2), slider.Maximum }.Distinct())
            {
                settingsType.GetProperty("MiningThreads")!.SetValue(loaded, saved);
                store.GetType().GetMethod("Save")!.Invoke(store, [loaded]);
                object reloaded = store.GetType().GetMethod("Load")!.Invoke(store, null)!;
                Check((int)settingsType.GetProperty("MiningThreads")!.GetValue(reloaded)! == saved,
                    "A saved count survives reload without resetting to one: " + saved);
            }
            foreach (int invalid in new[] { -10, int.MaxValue })
            {
                settingsType.GetProperty("MiningThreads")!.SetValue(loaded, invalid);
                File.WriteAllText(settingsPath, JsonSerializer.Serialize(loaded, settingsType));
                object repaired = store.GetType().GetMethod("Load")!.Invoke(store, null)!;
                Check((int)settingsType.GetProperty("MiningThreads")!.GetValue(repaired)! == Math.Clamp(invalid, 1, slider.Maximum),
                    "Loaded settings clamp an out-of-range value: " + invalid);
            }
            store.GetType().GetMethod("Save")!.Invoke(store, [Field(form, "_settings")]);

            int rpcCalls = 0;
            object rpc = Field(form, "_rpcClient");
            ReplaceHttp(rpc, new Handler(async request =>
            {
                rpcCalls++;
                using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Check(body.RootElement.GetProperty("method").GetString() == "startcpumining", "Node request starts CPU mining");
                Check(body.RootElement.GetProperty("params")[1].GetInt32() == exact, "RPC receives the selected integer, not a preset");
                return new(HttpStatusCode.OK) { Content = new StringContent("{\"result\":{},\"error\":null,\"id\":\"1\"}") };
            }));
            Run((Task)rpc.GetType().GetMethod("StartMiningAsync")!.Invoke(rpc, [new Uri("http://127.0.0.1:1"), "fixture-address", Call(form, "MiningThreadCount")])!);
            Set(form, "_miningStartedByApp", true);
            slider.Value = 1;
            Call(form, "UpdateMiningThreadLabels");
            Check(rpcCalls == 1 && (bool)Field(form, "_miningStartedByApp"), "Changing threads does not stop or restart active mining");
            Check(((Label)Field(form, "miningThreadsHintLabel")).Text.Contains("stop and start"), "Running miner gets a clear next-start message");
            Check(((LinkLabel)Field(form, "miningSettingsLink")).Text.StartsWith("Next start:"), "Home distinguishes the saved count from active mining");
            Set(form, "_miningStartedByApp", false);
            Call(form, "UpdateMiningThreadLabels");

            form.Show();
            Application.DoEvents();
            Call(form, "ShowMiningSettings");
            Application.DoEvents();
            Check(((TabControl)Field(form, "advancedTabControl")).SelectedTab == Field(form, "miningSettingsTabPage"), "Home shortcut opens Settings > Mining");
            if (screenshots != null)
            {
                foreach (Size size in new[] { new Size(1000, 780), new Size(1495, 1006) })
                {
                    form.MinimumSize = Size.Empty;
                    form.ClientSize = size;
                    Application.DoEvents();
                    Check(!((Control)Field(form, "subtitleLabel")).Bounds.IntersectsWith(((Control)Field(form, "_headerBetaLabel")).Bounds),
                        "Header subtitle stays clear of the network badge at " + size.Width);
                    Control card = AllControls(form).First(c => c.Name == "miningSettingsCard");
                    Control[] children = card.Controls.Cast<Control>().ToArray();
                    for (int i = 0; i < children.Length; i++)
                    {
                        Check(children[i].Left >= card.Padding.Left && children[i].Right <= card.ClientSize.Width - card.Padding.Right,
                            "Settings content fits card: " + children[i].Name + " at " + size.Width);
                        for (int j = i + 1; j < children.Length; j++)
                            Check(!children[i].Bounds.IntersectsWith(children[j].Bounds), "Settings rows do not overlap");
                    }
                    Capture(form, Path.Combine(screenshots, "mining-settings-" + size.Width + ".png"));
                }
            }
            ((TabControl)Field(form, "mainTabControl")).SelectedIndex = 0;
            Set(form, "_installingUpdate", true);
            Control operation = (Control)Field(form, "_operationProgressPanel");
            Control main = (Control)Field(form, "mainTabControl");
            string balanceText = ((Label)Field(form, "availableBalanceLabel")).Text;
            Call(form, "SetUpdateProgress", "Downloading update... 0%", 0);
            Application.DoEvents();
            Call(form, "SetUpdateProgress", "Downloading update... 12%", 12);
            Check(((Control)Field(form, "_operationProgressFill")).Width == ((Control)Field(form, "_operationProgressTrack")).ClientSize.Width * 12 / 100,
                "A 12 percent download fills 12 percent of the bar");
            int operationIndex = form.Controls.GetChildIndex(operation);
            int mainIndex = form.Controls.GetChildIndex(main);
            int homeSizeChanges = 0;
            main.SizeChanged += (_, _) => homeSizeChanges++;
            for (int percent = 1; percent <= 100; percent++)
                Call(form, "SetUpdateProgress", $"Downloading update... {percent}%", percent);
            Check(homeSizeChanges == 0, "One hundred download reports do not resize the Home page");
            Check(form.Controls.GetChildIndex(operation) == operationIndex && form.Controls.GetChildIndex(main) == mainIndex, "Download progress never reorders docked controls");
            Check(((Label)Field(form, "availableBalanceLabel")).Text == balanceText, "Downloading does not overwrite the wallet balance caption");
            Check(((Control)Field(form, "_operationProgressFill")).Width == ((Control)Field(form, "_operationProgressTrack")).ClientSize.Width,
                "Download bar matches its percentage");
            Type nodeInfo = App.GetType("Slithy_Tove.NodeInfo")!;
            object info = Activator.CreateInstance(nodeInfo, [2797L, 1m, "test", false, "test", 0L, 1L, 3391L])!;
            Call(form, "ShowChainProgress", info);
            Application.DoEvents();
            Check(((ProgressBar)Field(form, "chainSyncProgressBar")).Value == 82, "Native chain progress value matches its 82 percent caption");
            Check(((Label)Field(form, "chainSyncLabel")).Text.StartsWith("Chain sync:"), "Chain progress is labeled separately from app download");
            Check(!operation.Bounds.IntersectsWith(((Control)Field(form, "chainSyncPanel")).Bounds), "Download and sync rows do not overlap");
            if (screenshots != null)
            {
                // Native progress bars animate toward the new value after WM_PAINT.
                // Allow those frames before taking a still image.
                DateTime frameDeadline = DateTime.UtcNow.AddSeconds(2);
                while (DateTime.UtcNow < frameDeadline) { Application.DoEvents(); Thread.Sleep(20); }
                Capture(form, Path.Combine(screenshots, "download-and-sync.png"));
            }
            Set(form, "_installingUpdate", false);
            form.Hide();

            TestDownloadProgress(root);
            Console.WriteLine($"Passed {checks} checks. Disposable settings and simulated HTTP were used. No user wallet or public mining was touched.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void TestDownloadProgress(string root)
    {
        byte[] bytes = new byte[1_024_000];
        Random.Shared.NextBytes(bytes);
        object service = Activator.CreateInstance(App.GetType("Slithy_Tove.UpdateService")!, [Path.Combine(root, "download"), true])!;
        using IDisposable owned = (IDisposable)service;
        ReplaceHttp(service, new Handler(_ =>
        {
            var content = new StreamContent(new SmallChunks(bytes));
            content.Headers.ContentLength = bytes.Length;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }));
        object manifest = Activator.CreateInstance(App.GetType("Slithy_Tove.UpdateManifest")!,
            ["9.0.0", "https://slithy.io/test-fixture.exe", Convert.ToHexString(SHA256.HashData(bytes)), "fixture", DateTimeOffset.UtcNow, "normal", false, true])!;
        var progress = new Collector();
        Task task = (Task)service.GetType().GetMethod("DownloadVerifiedPackageAsync")!.Invoke(service, [manifest, progress, CancellationToken.None])!;
        Run(task);
        Check(progress.Values.Count <= 101, "Two thousand download chunks produce at most 101 progress reports");
        Check(progress.Values.Last() == 100, "Download reports completion");
        Check(progress.Values.Distinct().Count() == progress.Values.Count, "Progress never reports the same percent twice");
        Check(progress.Values.SequenceEqual(progress.Values.Order()), "Download progress moves forward");
    }

    private static IEnumerable<Control> AllControls(Control root)
    {
        foreach (Control child in root.Controls) { yield return child; foreach (Control nested in AllControls(child)) yield return nested; }
    }
    private static Form1 CreateForm(object store)
    {
        Form1 form = (Form1)Activator.CreateInstance(typeof(Form1), Hidden, null, [store], null)!;
        form.Shown -= (EventHandler)typeof(Form1).GetMethod("Form1_Shown", Hidden)!.CreateDelegate(typeof(EventHandler), form);
        ((System.Windows.Forms.Timer)Field(form, "statusTimer")).Stop();
        ((NotifyIcon)Field(form, "_trayIcon")).Visible = false;
        return form;
    }
    private static object Field(Form1 form, string name) => typeof(Form1).GetField(name, Hidden)!.GetValue(form)!;
    private static void Set(Form1 form, string name, object value) => typeof(Form1).GetField(name, Hidden)!.SetValue(form, value);
    private static object? Call(Form1 form, string name, params object?[] args) => typeof(Form1).GetMethod(name, Hidden)!.Invoke(form, args);
    private static void ReplaceHttp(object service, HttpMessageHandler handler)
    {
        FieldInfo field = service.GetType().GetField("_httpClient", Hidden)!;
        ((HttpClient)field.GetValue(service)!).Dispose();
        field.SetValue(service, new HttpClient(handler));
    }
    private static void Run(Task task)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(1); }
        if (!task.IsCompleted) throw new TimeoutException("Test did not finish");
        task.GetAwaiter().GetResult();
    }
    private static void Capture(Form form, string path)
    {
        using Bitmap bitmap = new(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path);
    }
    private static void Check(bool passed, string message)
    {
        if (!passed) throw new InvalidOperationException(message);
        checks++;
        Console.WriteLine("PASS " + message);
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => reply(request);
    }
    private sealed class Collector : IProgress<int>
    {
        public List<int> Values { get; } = [];
        public void Report(int value) => Values.Add(value);
    }
    private sealed class SmallChunks(byte[] data) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, 512)], cancellationToken);
    }
}
