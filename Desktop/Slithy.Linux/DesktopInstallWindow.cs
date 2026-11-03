using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Slithy.Linux;

internal sealed class DesktopInstallWindow : Window
{
    private readonly DesktopInstallService _installer = new();
    private readonly CancellationTokenSource _cancel = new();
    private readonly TextBlock _status = Dialogs.Text("Checking the extracted package...", 14);
    private readonly Button _action = Dialogs.Button("Install for my account");
    private readonly Button _close = Dialogs.Button("Cancel");
    private readonly bool _uninstall;
    private readonly bool _preview;
    private DesktopInstallPlan? _plan;
    private bool _busy, _closeRequested, _finished;
    private string? _installed;

    internal DesktopInstallWindow(bool uninstall, bool preview = false)
    {
        _uninstall = uninstall;
        _preview = preview;
        Title = uninstall ? "Uninstall Slithy Tove" : "Install Slithy Tove";
        Icon = Branding.WindowIcon;
        Width = 620; Height = 540; MinWidth = 480; MinHeight = 450;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush.Parse("#f3efe4");
        var body = new StackPanel { Margin = new Thickness(28), Spacing = 18 };
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        title.Children.Add(new Image { Source = Branding.ApplicationImage, Width = 52, Height = 52 });
        title.Children.Add(Dialogs.Text("Slithy Tove", 30));
        body.Children.Add(title);
        body.Children.Add(Dialogs.Text(uninstall ? "Remove the installed app" : "A place for your Slithy wallet", 22));
        body.Children.Add(Dialogs.Text(uninstall
            ? "This removes this installed program version and its menu shortcut. Your wallets, settings and blockchain data stay on this computer. Older program versions are left in place."
            : "Install Slithy for your Linux account. You can open it from your application menu afterward and remove the extracted download folder. No administrator password is needed.", 15));
        body.Children.Add(new Border { Background = Dialogs.Paper, CornerRadius = new CornerRadius(12), Padding = new Thickness(18), Child = _status });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Right };
        _action.IsEnabled = false;
        _action.Name = "InstallAction";
        _action.Background = Dialogs.Ink; _action.Foreground = Dialogs.Paper;
        if (uninstall) _action.Content = "Remove program";
        buttons.Children.Add(_close); buttons.Children.Add(_action); body.Children.Add(buttons);
        body.Children.Add(Dialogs.Text("Test-network software. Test coins have no monetary value.", 12));
        Content = WindowChrome.Wrap(this, new ScrollViewer { Content = body });
        Opened += async (_, _) => await Inspect();
        _action.Click += async (_, _) => await Apply();
        _close.Click += (_, _) => Close();
        Closing += (_, e) =>
        {
            if (_busy) { e.Cancel = true; _closeRequested = true; _cancel.Cancel(); _status.Text = "Finishing the current file operation..."; }
        };
        Closed += (_, _) => _cancel.Dispose();
    }

    private async Task Inspect()
    {
        if (_preview)
        {
            _status.Text = _uninstall ? "Version " + Branding.Release + "\nWallet data will not be removed."
                : "Version " + Branding.Release + "\nProgram size: 150 MB\n\nInstall location:\n/home/reader/.local/opt/slithy-tove/" + Branding.Release;
            _action.IsEnabled = true;
            return;
        }
        _busy = true;
        try
        {
            if (!OperatingSystem.IsLinux() || DesktopInstallService.IsRoot)
                throw new InvalidOperationException("Run this installer from your normal Linux account, without sudo.");
            _plan = await _installer.InspectAsync(AppContext.BaseDirectory, _cancel.Token);
            if (!_uninstall) await DesktopInstallService.CheckNativeDependenciesAsync(_plan.Source, _cancel.Token);
            _installer.RequireOtherAppsClosed();
            if (_uninstall && _plan.Source != _plan.Target)
                throw new InvalidOperationException("Use Uninstall from the installed Slithy application's menu entry. This extracted download folder will not be removed.");
            _status.Text = $"Version {_plan.Version}\nProgram size: {_plan.Bytes / (1024d * 1024d):N0} MB\n\n" +
                (_uninstall ? "Wallet data will not be removed." : "Install location:\n" + _plan.Target);
            _action.IsEnabled = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _status.Text = ex.Message; _close.Content = "Close"; }
        finally { _busy = false; if (_closeRequested) Close(); }
    }

    private async Task Apply()
    {
        if (_preview) return;
        if (_finished)
        {
            if (_installed != null)
            {
                try { Process.Start(new ProcessStartInfo(Path.Combine(_installed, "slithy-desktop")) { UseShellExecute = false }); }
                catch { _status.Text = "Slithy could not open. Try its entry in your application menu."; return; }
            }
            Close(); return;
        }
        if (_plan == null || _busy) return;
        _busy = true; _action.IsEnabled = false;
        _status.Text = _uninstall ? "Removing program files..." : "Installing Slithy Tove...";
        try
        {
            if (_uninstall) await _installer.UninstallAsync(_plan.Source, _cancel.Token);
            else _installed = await _installer.InstallAsync(_plan, _cancel.Token);
            _finished = true;
            _status.Text = _uninstall ? "Slithy was removed from your application menu. Your wallet data is still in place."
                : "Slithy is installed. You can open it here or from your application menu. The app can check for future updates itself.";
            _action.Content = _uninstall ? "Done" : "Open Slithy";
            _action.IsEnabled = true; _close.Content = "Close";
        }
        catch (OperationCanceledException) { _status.Text = "Installation canceled."; }
        catch (Exception ex) { _status.Text = ex.Message; _action.IsEnabled = true; }
        finally { _busy = false; if (_closeRequested) Close(); }
    }
}
