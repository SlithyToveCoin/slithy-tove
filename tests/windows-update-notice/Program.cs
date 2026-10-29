using System.Net;
using System.Reflection;
using Slithy_Tove;

internal static class Program
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int _checks;

    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string temporary = Path.Combine(Path.GetTempPath(), "slithy-update-notice-" + Guid.NewGuid().ToString("N"));
        string? screenshotDirectory = args.Length > 1 ? Path.GetFullPath(args[1]) : null;
        if (screenshotDirectory != null) Directory.CreateDirectory(screenshotDirectory);
        try
        {
            using Form1 form = CreateForm(temporary);
            var badge = (Button)Field(form, "_headerUpdateButton");
            var settings = Field(form, "_settings");
            form.Show();
            Application.DoEvents();
            Check(!badge.Visible, "No update badge before a verified release is found");
            Check(badge.Image != null, "Update badge has a drawn download icon");
            Check(!Due(form), "Periodic checks wait for startup to finish");
            Set(form, "_startupComplete", true);
            Check(Due(form), "First automatic check is due after startup");
            Set(form, "_lastUpdateAttemptUtc", DateTimeOffset.UtcNow);
            Check(!Due(form), "Checks are limited to once an hour");
            Set(form, "_lastUpdateAttemptUtc", DateTimeOffset.UtcNow.AddHours(-2));
            Check(Due(form), "A long-running app checks again after an hour");
            settings.GetType().GetProperty("CheckForUpdatesAutomatically")!.SetValue(settings, false);
            Check(!Due(form), "The automatic-check preference is respected");
            settings.GetType().GetProperty("CheckForUpdatesAutomatically")!.SetValue(settings, true);
            foreach (string flag in new[] { "_walletBusy", "_walletOpening", "_walletDialogOpen", "_miningChangeInProgress", "_checkingForUpdates", "_installingUpdate", "_closingForExit", "_updateHandoffRequested" })
            {
                Set(form, flag, true);
                Check(!Due(form), "Background check waits during " + flag.TrimStart('_'));
                Set(form, flag, false);
            }
            Publish(form, "9.0.0");
            Check(badge.Visible && ((Button)Field(form, "checkUpdatesButton")).Text == "Review update", "Verified release gets a persistent badge and review button");
            Check(((ToolStripMenuItem)Field(form, "_trayUpdateItem")).Available, "Tray menu offers the same update page");
            Check((bool)Call(form, "ShouldNotifyForUpdate", "9.0.0")!, "A new version can notify once");
            Check(!(bool)Call(form, "ShouldNotifyForUpdate", "9.0.0")!, "Repeated checks do not repeat the notification");
            Check((bool)Call(form, "ShouldNotifyForUpdate", "9.0.1")!, "A later version can notify");
            Publish(form, "8.0.0");
            Check(AvailableVersion(form) == "9.0.0", "An older signed release does not erase a newer notice");
            badge.PerformClick();
            Check(((TabControl)Field(form, "mainTabControl")).SelectedTab == Field(form, "advancedTabPage") &&
                ((TabControl)Field(form, "advancedTabControl")).SelectedTab == Field(form, "updatesTabPage"), "Badge opens Updates without installing");
            Check(!(bool)Field(form, "_installingUpdate") && !(bool)Field(form, "_updateHandoffRequested"), "Review does not download, install or close the app");
            Set(form, "_miningStartedByApp", true);
            Publish(form, "9.1.0", "required");
            Check((bool)Field(form, "_networkLockedByUpdate") && !((Button)Field(form, "sendButton")).Enabled,
                "Required release blocks new payments");
            Check(((Button)Field(form, "miningToggleButton")).Enabled && (bool)Field(form, "_miningStartedByApp"),
                "Required release leaves running mining and its Stop button intact");
            Publish(form, "9.2.0");
            Check(!(bool)Field(form, "_networkLockedByUpdate"), "Optional release does not block network actions");

            // A wallet action begins after the HTTP request starts. The check must not clear it.
            ReplaceHttp(form, new TestHandler(() =>
            {
                Check(!(bool)Field(form, "_walletBusy"), "A feed check does not mark the wallet busy");
                Set(form, "_walletBusy", true);
                throw new HttpRequestException("Offline test fixture");
            }));
            Run((Task)Call(form, "CheckForUpdatesAsync", false)!);
            Check((bool)Field(form, "_walletBusy"), "Failed background check cannot release a busy wallet action");
            Check(badge.Visible && AvailableVersion(form) == "9.2.0", "Offline check preserves the known update");
            Set(form, "_walletBusy", false);
            if (args.Length > 0)
            {
                byte[] envelope = File.ReadAllBytes(args[0]);
                ReplaceHttp(form, new TestHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(envelope) }));
                Run((Task)Call(form, "CheckForUpdatesAsync", false)!);
                Check(badge.Visible && AvailableVersion(form) == "9.2.0", "A current-version feed does not dismiss a known newer release");
                Check(!(bool)Field(form, "_walletBusy"), "Successful background checks leave wallet controls available");
            }
            if (screenshotDirectory != null)
            {
                ((TabControl)Field(form, "mainTabControl")).SelectedIndex = 0;
                foreach (int width in new[] { 1100, 1495 })
                {
                    form.ClientSize = new Size(width, 900);
                    Application.DoEvents();
                    Check(!badge.Bounds.IntersectsWith(((Label)Field(form, "_headerBetaLabel")).Bounds), "Update badge fits above the network badge at " + width + "px");
                    Check(badge.Left == ((Label)Field(form, "_headerBetaLabel")).Left, "Header badges share the same alignment at " + width + "px");
                    using Bitmap bitmap = new(form.Width, form.Height);
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(screenshotDirectory, "windows-update-notice-" + width + ".png"));
                }
            }
            form.Hide();
            Console.WriteLine($"Passed {_checks} checks. No real wallet, node or external HTTP request was used.");
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }

    private static Form1 CreateForm(string directory)
    {
        Type storeType = typeof(Form1).Assembly.GetType("Slithy_Tove.AppSettingsStore")!;
        object store = Activator.CreateInstance(storeType, [directory])!;
        Form1 form = (Form1)Activator.CreateInstance(typeof(Form1), Hidden, null, [store], null)!;
        form.Shown -= (EventHandler)typeof(Form1).GetMethod("Form1_Shown", Hidden)!.CreateDelegate(typeof(EventHandler), form);
        ((System.Windows.Forms.Timer)Field(form, "statusTimer")).Stop();
        ((NotifyIcon)Field(form, "_trayIcon")).Visible = false;
        return form;
    }

    private static object Field(Form1 form, string name) => typeof(Form1).GetField(name, Hidden)!.GetValue(form)!;
    private static void Set(Form1 form, string name, object value) => typeof(Form1).GetField(name, Hidden)!.SetValue(form, value);
    private static object? Call(Form1 form, string name, params object[] args) => typeof(Form1).GetMethod(name, Hidden)!.Invoke(form, args);
    private static bool Due(Form1 form) => (bool)Call(form, "ShouldCheckForUpdatesNow", DateTimeOffset.UtcNow)!;
    private static string AvailableVersion(Form1 form) => (string)Field(form, "_availableUpdate").GetType().GetProperty("Version")!.GetValue(Field(form, "_availableUpdate"))!;
    private static void Publish(Form1 form, string version, string level = "normal")
    {
        Type manifestType = typeof(Form1).Assembly.GetType("Slithy_Tove.UpdateManifest")!;
        object manifest = Activator.CreateInstance(manifestType, [version, "https://slithy.io/downloads/windows/test.exe", new string('a', 64), "Test release", DateTimeOffset.UtcNow, level, level == "required", true])!;
        Call(form, "PublishAvailableUpdate", manifest, false);
    }
    private static void ReplaceHttp(Form1 form, HttpMessageHandler handler)
    {
        object service = Field(form, "_updateService");
        FieldInfo client = service.GetType().GetField("_httpClient", Hidden)!;
        ((HttpClient)client.GetValue(service)!).Dispose();
        client.SetValue(service, new HttpClient(handler));
    }
    private static void Run(Task task)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(10); }
        if (!task.IsCompleted) throw new TimeoutException("UI test did not finish");
        task.GetAwaiter().GetResult();
    }
    private static void Check(bool passed, string message)
    {
        if (!passed) throw new InvalidOperationException(message);
        _checks++;
        Console.WriteLine("PASS " + message);
    }
    private sealed class TestHandler(Func<HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(reply());
    }
}
