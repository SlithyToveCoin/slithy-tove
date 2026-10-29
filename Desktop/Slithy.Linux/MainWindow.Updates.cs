using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Slithy.Linux;

internal sealed partial class MainWindow
{
    private readonly LinuxGuiUpdateService _updates = new();
    private readonly LinuxGuiUpdateInstaller _updateInstaller = new();
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly CancellationTokenSource _updateLifetime = new();
    private CancellationTokenSource? _downloadCancellation;
    private LinuxGuiUpdateManifest? _availableUpdate;
    private bool _checkingUpdate, _installingUpdate, _updateStoppedNode;
    private bool _updateAllowsStop, _stoppingMiningForUpdate;
    private DateTimeOffset _lastUpdateCheck = DateTimeOffset.MinValue;
    private string _notifiedUpdate = "";
    private readonly Button _updateBadge = Dialogs.Button("Update available");
    private readonly TextBlock _updateStatus = Dialogs.Text("Check for a newer Linux GUI release.");
    private readonly TextBlock _updateNoticeText = Dialogs.Text("");
    private readonly Border _updateNotice = new() { IsVisible = false };
    private readonly Button _checkUpdate = Dialogs.Button("Check now");
    private readonly Button _installUpdate = Dialogs.Button("Review update");
    private readonly Button _cancelUpdate = Dialogs.Button("Cancel download");
    private readonly CheckBox _automaticUpdates = new() { Content = "Check for updates when Slithy opens and while it is running" };
    private readonly ProgressBar _updateProgress = new() { Minimum = 0, Maximum = 100, Height = 6, IsVisible = false };

    private Control GetUpdateBadge()
    {
        _updateBadge.Name = "UpdateAvailableBadge";
        _updateBadge.FontSize = 12;
        _updateBadge.MinHeight = 28;
        _updateBadge.Padding = new Thickness(8, 4);
        _updateBadge.HorizontalAlignment = HorizontalAlignment.Right;
        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        label.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M8,1 L8,11 M3,6 L8,11 L13,6 M2,12 L2,15 L14,15 L14,12"),
            Stroke = Dialogs.Ink, StrokeThickness = 1.7, Width = 16, Height = 16
        });
        label.Children.Add(Dialogs.Text("Update available", 12));
        _updateBadge.Content = label;
        _updateBadge.IsVisible = false;
        ToolTip.SetTip(_updateBadge, "Review and install the available update");
        _updateBadge.Click += async (_, _) => await ReviewUpdate();
        return _updateBadge;
    }

    private Control GetUpdateNotice()
    {
        _updateNotice.Name = "UpdateNotification";
        _updateNotice.Background = Dialogs.Gold;
        _updateNotice.Padding = new Thickness(18, 8);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 10 };
        _updateNoticeText.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(_updateNoticeText);
        var review = Dialogs.Button("Review update");
        review.Name = "ReviewUpdateNotice";
        review.Click += async (_, _) => await ReviewUpdate();
        Grid.SetColumn(review, 1); row.Children.Add(review);
        var later = Dialogs.Button("Later");
        later.Click += (_, _) => _updateNotice.IsVisible = false;
        Grid.SetColumn(later, 2); row.Children.Add(later);
        _updateNotice.Child = row;
        return _updateNotice;
    }

    private Control BuildUpdatesCard()
    {
        _automaticUpdates.Name = "AutomaticUpdateChecks";
        _automaticUpdates.IsChecked = _settings.AutoCheckUpdates;
        _automaticUpdates.IsCheckedChanged += (_, _) =>
        {
            _settings.AutoCheckUpdates = _automaticUpdates.IsChecked == true;
            Save();
            if (_settings.AutoCheckUpdates) _ = CheckForUpdates(manual: false);
        };
        _checkUpdate.Name = "CheckForUpdates";
        _checkUpdate.Click += async (_, _) => await CheckForUpdates(manual: true);
        _installUpdate.Name = "InstallAvailableUpdate";
        _installUpdate.IsVisible = false;
        _installUpdate.Click += async (_, _) => await ReviewUpdate();
        _cancelUpdate.IsVisible = false;
        _cancelUpdate.Click += (_, _) => _downloadCancellation?.Cancel();
        _updateStatus.Name = "UpdateStatus";
        return Card("Updates", Dialogs.Text("Installed Linux GUI " + Branding.Release), _updateStatus,
            _automaticUpdates, _updateProgress, Row(_checkUpdate, _installUpdate, _cancelUpdate),
            Dialogs.Text("An update downloads first. Slithy then stops this app's node before opening the new version. Your wallets stay in their data folder.", 12));
    }

    private void StartUpdateChecks()
    {
        if (_preview || _regtest || _settingsError != null) return;
        _updateTimer.Tick += async (_, _) =>
        {
            if (_settings.AutoCheckUpdates && DateTimeOffset.UtcNow - _lastUpdateCheck >= TimeSpan.FromHours(1))
                await CheckForUpdates(manual: false);
        };
        _updateTimer.Start();
        if (_settings.AutoCheckUpdates) _ = CheckForUpdates(manual: false);
    }

    private async Task CheckForUpdates(bool manual)
    {
        if (_preview || _regtest || _closing || _checkingUpdate || _installingUpdate) return;
        _checkingUpdate = true;
        _lastUpdateCheck = DateTimeOffset.UtcNow;
        _checkUpdate.IsEnabled = false;
        if (manual) _updateStatus.Text = "Checking for updates...";
        try
        {
            var update = await _updates.CheckAsync(Branding.Release, _updateLifetime.Token);
            if (_closing) return;
            if (update != null) ApplyUpdateNotice(update);
            else if (_availableUpdate == null) _updateStatus.Text = "Slithy is up to date. Checked " + DateTime.Now.ToString("t") + ".";
            else _updateStatus.Text = "Update " + _availableUpdate.Version + " is available. You can install it when you are ready.";
        }
        catch (OperationCanceledException) when (_updateLifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (_closing) return;
            _updateStatus.Text = _availableUpdate == null
                ? "Update check could not finish. Try again later or visit slithy.io/downloads.html."
                : "The latest check could not finish. Update " + _availableUpdate.Version + " is still available.";
            Log(ex is System.Security.Cryptography.CryptographicException or InvalidDataException
                ? "Rejected an invalid update notice. Nothing was installed." : "Update check could not finish. Your node is still running.");
        }
        finally { _checkingUpdate = false; _checkUpdate.IsEnabled = !_installingUpdate; }
    }

    internal void ApplyUpdateNotice(LinuxGuiUpdateManifest update)
    {
        if (LinuxGuiVersion.Parse(update.Version).CompareTo(LinuxGuiVersion.Parse(Branding.Release)) <= 0) return;
        if (_availableUpdate != null && LinuxGuiVersion.Parse(update.Version).CompareTo(LinuxGuiVersion.Parse(_availableUpdate.Version)) < 0) return;
        _availableUpdate = update;
        _updateBadge.IsVisible = true;
        _installUpdate.IsVisible = true;
        _updateStatus.Text = "Update " + update.Version + " is available.";
        if (_notifiedUpdate == update.Version) return;
        _notifiedUpdate = update.Version;
        _updateNoticeText.Text = "Slithy " + update.Version + " is available. You can update without closing it first.";
        _updateNotice.IsVisible = true;
        if (!_preview && !_regtest) _ = NotifyDesktop(update.Version);
    }

    private static async Task NotifyDesktop(string version)
    {
        // Some Linux desktops have no notification service. The in-app notice stays visible.
        if (!OperatingSystem.IsLinux() || !File.Exists("/usr/bin/notify-send")) return;
        try
        {
            using var notification = Process.Start(new ProcessStartInfo("/usr/bin/notify-send")
            {
                UseShellExecute = false,
                ArgumentList = { "--app-name=Slithy Tove", "--icon=io.slithy.Tove", "Slithy update available", "Version " + version + " is ready. Open Slithy to review and install it." }
            });
            if (notification != null)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                try { await notification.WaitForExitAsync(timeout.Token); }
                catch (OperationCanceledException) { if (!notification.HasExited) notification.Kill(); }
            }
        }
        catch { /* A missing desktop notification must not interrupt the wallet. */ }
    }

    private async Task ReviewUpdate()
    {
        if (_availableUpdate == null || _installingUpdate || _closing) return;
        if (_busy) { _updateStatus.Text = "Finish the open wallet action, then choose Review update."; return; }
        if (_pending != null || !_paymentBlocked && _pendingAddress.Length > 0)
        { await Dialogs.Message(this, "Payment still open", "Finish or cancel the payment before updating."); return; }
        if (_preview || _regtest) return;
        string? archive = null;
        _updateStoppedNode = false;
        _installingUpdate = true;
        _updateAllowsStop = true;
        _checkUpdate.IsEnabled = false;
        _installUpdate.IsEnabled = false;
        _busy = true; UpdateControls();
        try
        {
            _updateStatus.Text = "Checking the current release before installing...";
            var update = await _updates.CheckAsync(Branding.Release, _updateLifetime.Token);
            if (update == null)
            {
                _availableUpdate = null;
                _updateBadge.IsVisible = false; _updateNotice.IsVisible = false; _installUpdate.IsVisible = false;
                _updateStatus.Text = "No newer release is currently offered. Nothing was installed.";
                return;
            }
            string message = update.ReleaseNotes + "\n\nThe download can run while you mine. After it is checked, Slithy stops mining and saves the local node before opening the new version. Mining will not restart automatically.";
            if (!await Dialogs.Confirm(this, "Update to " + update.Version + "?", message, "Download and update")) return;
            _cancelUpdate.IsVisible = true; _updateProgress.IsVisible = true; _updateProgress.Value = 0;
            _downloadCancellation = CancellationTokenSource.CreateLinkedTokenSource(_updateLifetime.Token);
            _updateStatus.Text = "Downloading update...";
            archive = await _updates.DownloadAsync(update, new Progress<int>(value => _updateProgress.Value = value), _downloadCancellation.Token);
            _updateStatus.Text = "Checking the downloaded files...";
            var preparedUpdate = await _updateInstaller.StageAsync(update, archive, _downloadCancellation.Token);
            _downloadCancellation.Token.ThrowIfCancellationRequested();
            _updateAllowsStop = false;
            UpdateControls();
            _cancelUpdate.IsVisible = false;
            _updateStatus.Text = "Saving the local node and opening the update...";
            await _updateInstaller.ActivateAndLaunchAsync(preparedUpdate, async () =>
            {
                _timer.Stop();
                while (_polling || _stoppingMiningForUpdate) await Task.Delay(50);
                try { if (_walletName.Length > 0 && _endpoint != null) await _wallet.LockWalletOnlyAsync(_endpoint); }
                catch { Log("Wallet lock was not confirmed. Stopping this app's local node before updating."); }
                await _host.StopOwnedAsync();
                _updateStoppedNode = true;
            }, _updateLifetime.Token);
            _closing = true;
            StopUpdateChecks();
            _tray?.Dispose(); _wallet.Dispose(); _node.Dispose(); await _host.DisposeAsync();
            Close();
        }
        catch (OperationCanceledException) when (!_updateLifetime.IsCancellationRequested)
        { _updateStatus.Text = "Update canceled. Your current app and wallet files have not been replaced."; }
        catch (Exception ex)
        {
            _updateStatus.Text = "The update could not finish. Your previous installation and wallets are still in place.";
            if (ex is LinuxGuiUpdateLaunchUncertainException)
            {
                _ready = false;
                _updateStatus.Text = "The replacement app did not confirm that it closed. This app has not restarted mining or its node. Close both windows before reopening Slithy.";
            }
            else if (_updateStoppedNode)
            {
                try
                {
                    await _host.StartAsync(); _endpoint = await _host.GetEndpointAsync();
                    _walletName = ""; _snapshot = null; _locked = true;
                    _balance.Text = "Open a wallet"; _walletStatus.Text = "The update did not start. Open your wallet again.";
                    await Refresh();
                }
                catch { _ready = false; _status.Text = "The update did not start. Close Slithy and reopen your previous version."; }
            }
            await Dialogs.Message(this, "Update did not finish", _updateStatus.Text + "\nIf mining stopped, start it again when you are ready.");
        }
        finally
        {
            if (archive != null)
            {
                try { _updates.RemoveCachedDownload(archive); }
                catch { Log("The update archive could not be removed from the download cache."); }
            }
            _updateAllowsStop = false;
            while (_stoppingMiningForUpdate) await Task.Delay(50);
            _downloadCancellation?.Dispose(); _downloadCancellation = null;
            _installingUpdate = false; _busy = false;
            _cancelUpdate.IsVisible = false; _updateProgress.IsVisible = false;
            _checkUpdate.IsEnabled = true; _installUpdate.IsEnabled = true;
            if (!_closing && _ready) _timer.Start();
            UpdateControls();
        }
    }

    private async Task StopMiningForUpdate()
    {
        if (!_updateAllowsStop || _stoppingMiningForUpdate || _mining?.Active != true || _endpoint == null) return;
        _stoppingMiningForUpdate = true;
        UpdateControls();
        try
        {
            while (_polling) await Task.Delay(50);
            await _node.StopMiningAsync(_endpoint);
            await Refresh();
            Log("Mining stopped. The update download can continue.");
        }
        catch { Log("Mining did not confirm that it stopped. Cancel the update download to try again."); }
        finally { _stoppingMiningForUpdate = false; UpdateControls(); }
    }

    private void StopUpdateChecks()
    {
        _updateTimer.Stop();
        _updateLifetime.Cancel();
        _updates.Dispose();
    }
}
