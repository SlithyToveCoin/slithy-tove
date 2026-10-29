using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Reflection;

namespace Slithy.Linux;

internal static class UiSelfTest
{
    public static int Run(string[] args)
    {
        string output = args.SkipWhile(a => a != "--screenshots").Skip(1).FirstOrDefault()
            ?? Path.Combine(Path.GetTempPath(), "slithy-gui-screenshots");
        Directory.CreateDirectory(output);
        try
        {
            if (Branding.ApplicationImage.PixelSize != new Avalonia.PixelSize(256, 256) || Branding.WindowIcon == null)
                throw new InvalidOperationException("The application icon was not loaded from the embedded artwork.");
            Branding.ApplicationImage.Save(Path.Combine(output, "application-icon.png"));
            var dialog = Dialogs.CreateWindow("Icon check");
            if (!ReferenceEquals(dialog.Icon, Branding.WindowIcon)) throw new InvalidOperationException("Dialog icon missing.");
            dialog.Close();
            Console.WriteLine("PASS: branded application and dialog icons load from embedded artwork.");
            string mining = MainWindow.MiningStatusText(new Slithy_Tove.MiningInfo(true, 250, 4, "test-address", "yespower", 2), 2000);
            if (!mining.Contains((2001).ToString("N0")) || !mining.Contains("Blocks found this session: 2"))
                throw new InvalidOperationException("Mining status confused rewards found with chain height.");
            Console.WriteLine("PASS: mining status separates chain height from blocks found.");
            string settingsRoot = Path.Combine(Path.GetTempPath(), "slithy-gui-invalid-settings-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new LinuxSettingsStore(settingsRoot);
                Directory.CreateDirectory(store.ConfigDirectory);
                File.WriteAllText(store.SettingsPath, "{broken");
                var damaged = new MainWindow(store: store);
                if (!damaged.DisplayedStatus.Contains("Settings could not be loaded") || File.ReadAllText(store.SettingsPath) != "{broken")
                    throw new InvalidOperationException("Damaged settings were not preserved for review.");
                Console.WriteLine("PASS: damaged settings produce a visible message without overwriting data.");
            }
            finally { Directory.Delete(settingsRoot, true); }
            foreach (string invalid in new[] { "", "null", "{}", "{\"Transfer\":null,\"Address\":\"test\",\"Amount\":1}" })
                if (MainWindow.PendingRecordIsValid(invalid)) throw new InvalidOperationException("Damaged payment record accepted.");
            string valid = System.Text.Json.JsonSerializer.Serialize(new
            {
                Transfer = new { TransactionHash = new string('a', 64), FeeAtomic = 1UL, TransactionMetadata = "00aa" },
                Address = "validation-fixture", Amount = 1m
            });
            if (!MainWindow.PendingRecordIsValid(valid) || MainWindow.PendingRecordIsValid(valid.Replace("00aa", "not-hex")))
                throw new InvalidOperationException("Payment record checks failed.");
            Console.WriteLine("PASS: pending payment records reject missing fields and malformed data.");
            CheckWalletPicker(output);
            foreach (int width in new[] { 1200, 980, 760 })
            {
                MainWindow window = new(preview: true) { Width = width, Height = 720 };
                if (!ReferenceEquals(window.Icon, Branding.WindowIcon)) throw new InvalidOperationException("Main window icon missing.");
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var logo = window.GetVisualDescendants().OfType<Image>().Single(c => c.Name == "SlithyHeaderLogo");
                if (!ReferenceEquals(logo.Source, Branding.HeaderMark)) throw new InvalidOperationException("Header artwork missing.");
                TabControl? tabs = window.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
                if (tabs == null) throw new InvalidOperationException("The main tabs are missing.");
                for (int index = 0; index < tabs.ItemCount; index++)
                {
                    tabs.SelectedIndex = index;
                    Dispatcher.UIThread.RunJobs();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    using var image = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rendered frame.");
                    image.Save(Path.Combine(output, $"linux-{width}-tab-{index}.png"));
                    if (index == 0)
                    {
                        var home = window.GetVisualDescendants().OfType<Control>().ToArray();
                        var balance = home.OfType<TextBlock>().Single(c => c.Name == "WalletBalance");
                        var mine = home.OfType<Button>().Single(c => c.Name == "MiningToggle");
                        if (balance.Bounds.Width < 200 || mine.Bounds.Width < 180)
                            throw new InvalidOperationException("Home controls lost their usable width.");
                    }
                }
                var controls = window.GetVisualDescendants().OfType<Control>().ToArray();
                var autoLock = controls.OfType<CheckBox>().Single(c => c.Name == "AutoLockEnabled");
                var minutes = controls.OfType<NumericUpDown>().Single(c => c.Name == "AutoLockMinutes");
                if (autoLock.IsChecked != true || !minutes.IsEnabled) throw new InvalidOperationException("Auto-lock should start enabled.");
                autoLock.IsChecked = false;
                Dispatcher.UIThread.RunJobs();
                if (minutes.IsEnabled) throw new InvalidOperationException("Timeout input should be disabled when auto-lock is off.");
                autoLock.IsChecked = true;
                Dispatcher.UIThread.RunJobs();
                if (!minutes.IsEnabled) throw new InvalidOperationException("Timeout input should return when auto-lock is on.");
                CheckUpdateNotice(window, tabs, output, width);
                CheckUpdateMiningControls(window);
                CheckWindowChrome(window);
                if (OperatingSystem.IsLinux())
                    window.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "CloseWindow")
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                else window.Close();
                Dispatcher.UIThread.RunJobs();
                Console.WriteLine($"PASS: Linux GUI renders all tabs and toggles auto-lock at {width}px.");
            }
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("FAIL: GUI render test: " + e.Message);
            return 1;
        }
    }

    private static void CheckWindowChrome(MainWindow window)
    {
        if (!OperatingSystem.IsLinux()) return;
        var controls = window.GetVisualDescendants().OfType<Control>().ToArray();
        var logo = controls.OfType<Image>().Single(c => c.Name == "SlithyTitlebarLogo");
        var maximize = controls.OfType<Button>().Single(c => c.Name == "MaximizeWindow");
        var grips = controls.Where(c => c.Name?.StartsWith("WindowResize", StringComparison.Ordinal) == true).ToArray();
        if (window.SystemDecorations != SystemDecorations.None || !ReferenceEquals(logo.Source, Branding.ApplicationImage) || grips.Length != 8)
            throw new InvalidOperationException("The Linux titlebar needs its logo and resize handles.");
        maximize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        if (window.WindowState != WindowState.Maximized || grips.Any(c => c.IsVisible))
            throw new InvalidOperationException("A maximized window must hide its resize handles.");
        maximize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        if (window.WindowState != WindowState.Normal || grips.Any(c => !c.IsVisible))
            throw new InvalidOperationException("Restoring the window must restore its resize handles.");
        Console.WriteLine("PASS: branded Linux titlebar and maximize/restore controls.");
    }

    private static void CheckUpdateMiningControls(MainWindow window)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var fields = new[] { "_ready", "_busy", "_installingUpdate", "_updateAllowsStop", "_stoppingMiningForUpdate", "_mining" }
            .ToDictionary(name => name, name => typeof(MainWindow).GetField(name, flags)
                ?? throw new InvalidOperationException("Missing mining state field: " + name));
        var original = fields.ToDictionary(entry => entry.Key, entry => entry.Value.GetValue(window));
        var refresh = typeof(MainWindow).GetMethod("UpdateControls", flags)
            ?? throw new InvalidOperationException("Mining controls could not be refreshed for the UI test.");
        var button = window.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "MiningToggle");
        try
        {
            fields["_ready"].SetValue(window, true);
            fields["_busy"].SetValue(window, true);
            fields["_installingUpdate"].SetValue(window, true);
            fields["_updateAllowsStop"].SetValue(window, true);
            fields["_stoppingMiningForUpdate"].SetValue(window, false);
            fields["_mining"].SetValue(window, new Slithy_Tove.MiningInfo(true, 250, 4, "test-address", "yespower", 2));
            refresh.Invoke(window, null);
            if (!button.IsEnabled || (string?)button.Content != "Stop mining")
                throw new InvalidOperationException("Active mining must remain stoppable during the update download.");

            fields["_updateAllowsStop"].SetValue(window, false);
            refresh.Invoke(window, null);
            if (button.IsEnabled) throw new InvalidOperationException("Mining controls must wait during the update handoff.");

            fields["_updateAllowsStop"].SetValue(window, true);
            fields["_stoppingMiningForUpdate"].SetValue(window, true);
            refresh.Invoke(window, null);
            if (button.IsEnabled) throw new InvalidOperationException("A mining stop already in progress must not be sent again.");

            fields["_stoppingMiningForUpdate"].SetValue(window, false);
            fields["_mining"].SetValue(window, new Slithy_Tove.MiningInfo(false, 0, 0, "", "yespower", 2));
            refresh.Invoke(window, null);
            if (button.IsEnabled) throw new InvalidOperationException("An update must not allow mining to start while the wallet is busy.");
        }
        finally
        {
            foreach (var entry in original) fields[entry.Key].SetValue(window, entry.Value);
            refresh.Invoke(window, null);
        }
        Console.WriteLine("PASS: Stop mining stays available during download and waits during handoff or an existing stop request.");
    }

    private static void CheckUpdateNotice(MainWindow window, TabControl tabs, string output, int width)
    {
        var controls = window.GetVisualDescendants().OfType<Control>().ToArray();
        var automatic = controls.OfType<CheckBox>().Single(c => c.Name == "AutomaticUpdateChecks");
        var badge = controls.OfType<Button>().Single(c => c.Name == "UpdateAvailableBadge");
        var notice = controls.OfType<Border>().Single(c => c.Name == "UpdateNotification");
        if (automatic.IsChecked != true || badge.IsVisible || notice.IsVisible)
            throw new InvalidOperationException("Update checks must start enabled without an available-update notice.");
        var update = new LinuxGuiUpdateManifest("0.1.99-beta.1",
            "https://slithy.io/downloads/slithy-linux-gui-test-fixture.tar.gz", new string('a', 64),
            "UI fixture", DateTimeOffset.UtcNow, LinuxGuiUpdateService.Platform,
            LinuxGuiUpdateService.Network, LinuxGuiUpdateService.Genesis);
        window.ApplyUpdateNotice(update with { Version = Branding.Release });
        if (badge.IsVisible || notice.IsVisible)
            throw new InvalidOperationException("The current release must not show an update notice.");
        window.ApplyUpdateNotice(update);
        Dispatcher.UIThread.RunJobs();
        var updateCard = automatic.GetVisualAncestors().OfType<Border>().First();
        var settingsScroll = automatic.GetVisualAncestors().OfType<ScrollViewer>().First();
        settingsScroll.Offset = new Avalonia.Vector(0, updateCard.Bounds.Top);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using (var image = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No update settings frame."))
            image.Save(Path.Combine(output, $"linux-{width}-update-settings.png"));
        tabs.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();
        if (!badge.IsVisible || !notice.IsVisible)
            throw new InvalidOperationException("A newer release must show the update badge and notification.");
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using (var image = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No update notification frame."))
            image.Save(Path.Combine(output, $"linux-{width}-update.png"));
        var later = notice.GetVisualDescendants().OfType<Button>().Single(c => c.Content as string == "Later");
        later.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        if (notice.IsVisible || !badge.IsVisible)
            throw new InvalidOperationException("Later must dismiss the notice without hiding the update badge.");
        window.ApplyUpdateNotice(update);
        if (notice.IsVisible || !badge.IsVisible)
            throw new InvalidOperationException("A repeat check must not show the same notice again.");
        Console.WriteLine($"PASS: update badge, dismissible notification and repeat-check behavior at {width}px.");
    }

    private static void CheckWalletPicker(string output)
    {
        foreach (var fixture in new[]
        {
            (Names: new[] { "test2", "dan1", "dan1" }, Preferred: "test2", Expected: "test2"),
            (Names: new[] { "test2", "dan1" }, Preferred: "missing", Expected: "dan1"),
            (Names: Array.Empty<string>(), Preferred: "missing", Expected: (string?)null)
        })
        {
            var dialog = Dialogs.CreateWalletDialog("Open wallet", fixture.Names, fixture.Preferred);
            dialog.Show(); Dispatcher.UIThread.RunJobs();
            var controls = dialog.GetVisualDescendants().OfType<Control>().ToArray();
            var picker = controls.OfType<ComboBox>().Single(c => c.Name == "WalletPicker");
            var submit = controls.OfType<Button>().Single(c => c.Name == "WalletSubmit");
            if (OperatingSystem.IsLinux() && (dialog.SystemDecorations != SystemDecorations.None ||
                !controls.OfType<Image>().Any(c => c.Name == "SlithyTitlebarLogo" && ReferenceEquals(c.Source, Branding.ApplicationImage))))
                throw new InvalidOperationException("The wallet dialog titlebar is missing its Slithy logo.");
            if ((string?)picker.SelectedItem != fixture.Expected)
                throw new InvalidOperationException("The wallet picker did not select the saved or first wallet.");
            if (controls.OfType<TextBox>().Any(c => c.Name == "WalletName"))
                throw new InvalidOperationException("Opening an existing wallet must not ask for a typed name.");
            if (picker.ItemCount != fixture.Names.Distinct(StringComparer.Ordinal).Count() || submit.IsEnabled != (fixture.Expected != null))
                throw new InvalidOperationException("The wallet picker mishandled duplicates or an empty list.");
            if (!controls.OfType<Button>().Any(c => c.Content as string == "Create wallet") ||
                !controls.OfType<Button>().Any(c => c.Content as string == "Restore recovery words") ||
                !controls.OfType<Button>().Any(c => c.Content as string == "Restore wallet file"))
                throw new InvalidOperationException("Wallet creation or restore is missing from the open screen.");
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var image = dialog.CaptureRenderedFrame() ?? throw new InvalidOperationException("No wallet picker frame.");
            image.Save(Path.Combine(output, fixture.Expected == null ? "wallet-picker-empty.png" : "wallet-picker.png"));
            dialog.Close(); Dispatcher.UIThread.RunJobs();
        }
        var create = Dialogs.CreateWalletDialog("Create wallet");
        create.Show(); Dispatcher.UIThread.RunJobs();
        if (!create.GetVisualDescendants().OfType<TextBox>().Any(c => c.Name == "WalletName"))
            throw new InvalidOperationException("New wallets still need an editable name.");
        create.Close(); Dispatcher.UIThread.RunJobs();
        Console.WriteLine("PASS: existing-wallet dropdown, saved selection, empty list and create/restore routes.");
    }
}
