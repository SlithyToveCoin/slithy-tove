//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Main Window Logic
//===============================================
using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;

namespace Slithy_Tove;

// This is the main Windows Form. It connects the buttons and labels on screen
// to the wallet, node, mining, update, and treasury code.
public partial class Form1 : Form
{
    // These fields hold app-wide helpers and the current UI state.
    // Most button clicks below change one or more of these values.
    private static readonly bool InstallerUpdatesEnabled = true;
    private const bool RealCpuMinerAvailable = true;
    private const int BetaDifficultyRetargetBlocks = 30;
    private const long MinimumFreeBytesForMining = 2L * 1024L * 1024L * 1024L;
    private readonly AppSettingsStore _settingsStore = new();
    private readonly NodeRpcClient _rpcClient = new();
    private readonly TreasuryStatusService _treasuryStatusService = new();
    private readonly UpdateService _updateService;
    private readonly LocalNodeManager _localNode;
    private readonly WalletRpcClient _walletRpcClient = new();
    private readonly UserActivityFilter _activityFilter = new();
    private AppSettings _settings;
    private WalletSnapshot? _walletSnapshot;
    private string _openWalletName = "";
    private bool _refreshing;
    private bool _diskScanInProgress;
    private DateTime _lastDiskScanUtc;
    private long _cachedBlockchainBytes;
    private bool _walletRefreshing;
    private bool _walletOpening;
    private bool _walletDialogOpen;
    private bool _loadingSettingsIntoControls;
    private bool _checkingForUpdates;
    private bool _exitRequested;
    private bool _cleanupComplete;
    private bool _miningStartedByApp;
    private bool _miningChangeInProgress;
    private bool _networkLockedByUpdate;
    private bool _updateHandoffRequested;
    private string _activeRemoteNodeAddress = "";
    private int _consecutiveNodeRefreshFailures;
    private DateTimeOffset _lastSuccessfulNodeRefreshUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastMiningWalletRefreshUtc = DateTimeOffset.MinValue;
    private DateTimeOffset? _lastMiningRewardUtc;
    private long _lastWalletRefreshHeight;
    private long _lastMiningBlocksCount = -1;
    private int _operationProgressPercent;

    private sealed record DiskSpaceSnapshot(
        string DriveName,
        string DataDirectory,
        long BlockchainBytes,
        long FreeBytes,
        long TotalBytes);

    public Form1()
    {
        // Keep this startup order stable:
        // 1. Build the controls from the designer.
        // 2. Load icons, layout helpers, and styling.
        // 3. Load saved settings.
        // 4. Prepare services like wallet RPC, updates, and the tray icon.
        InitializeComponent();
        LoadApplicationIcon();
        LoadBrandMark();
        InitializeActivityViews();
        InitializeMiningAnimation();
        InitializeResponsiveHome();
        InitializeWebsiteLinks();
        InitializeVersionLabel();
        InitializeOperationProgress();
        _settings = _settingsStore.Load();
        ArchivePreSeedWallets();
        _localNode = new LocalNodeManager(AppendLog);
        _updateService = new UpdateService(_settingsStore.UpdateDirectory);
        Application.AddMessageFilter(_activityFilter);
        _trayIcon.Icon = Icon ?? Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        LoadSettingsIntoControls();
        ApplyCozyStyling();
    }

    private void InitializeOperationProgress()
    {
        // The progress bar is laid out in the designer. This syncs the fill.
        _operationProgressTrack.Resize += (_, _) => UpdateOperationProgressFill();
        updateProgressTrackPanel.Resize += (_, _) => UpdateUpdateProgressFill(_operationProgressPercent);

        _operationProgressPanel.BringToFront();
    }

    private void InitializeActivityViews()
    {
        // Activity controls are laid out in the designer. This wires live behavior.
        _walletActivityListView.Resize += (_, _) => ResizeActivityColumns();
        _activityEmptyLabel.BringToFront();
        _viewAllActivityButton.Click += (_, _) => mainTabControl.SelectedTab = _walletActivityTab;
        _viewAllActivityButton.BringToFront();

        ApplyRoundedRegion(_activityCard, 20);
        _activityCard.Resize += (_, _) => ApplyRoundedRegion(_activityCard, 20);
    }

    private void InitializeVersionLabel()
    {
        // Shows the app version in the header.
        _headerVersionLabel.Text = $"v{CurrentVersion}";
        _headerVersionLabel.BringToFront();
        _headerBetaLabel.BringToFront();
    }

    private void ArchivePreSeedWallets()
    {
        IReadOnlyList<string> archived = WalletCompatibilityTools.ArchivePreSeedWallets(
            _settingsStore.WalletDirectory,
            _settingsStore.AppDataDirectory);
        if (archived.Count == 0)
        {
            return;
        }

        if (archived.Contains(_settings.LastWalletName, StringComparer.OrdinalIgnoreCase))
        {
            _settings.LastWalletName = "";
        }
        foreach (string walletName in archived)
        {
            _settings.WalletBalanceCache.Remove(walletName);
        }
        _settingsStore.Save(_settings);
        AppendLog($"Archived pre-seed wallets: {string.Join(", ", archived)}.");
    }


    private void InitializeResponsiveHome()
    {
        // Home controls are laid out in the designer. This updates rounded cards as the form changes.
        _homeLayout.BringToFront();

        ApplyRoundedRegion(_homeMissionCard, 22);
        _homeMissionCard.Resize += (_, _) => ApplyRoundedRegion(_homeMissionCard, 22);
    }

    private void InitializeWebsiteLinks()
    {
        // Makes the slithy.io labels behave like clickable website links.
        ConfigureLink(_homeWebsiteLinkLabel, "https://slithy.io");
        ConfigureLink(supportEmailLinkLabel, "mailto:support@slithy.io?subject=Slithy%20Tove%20support");
    }

    private static void ConfigureLink(LinkLabel linkLabel, string target)
    {
        linkLabel.LinkBehavior = LinkBehavior.HoverUnderline;
        linkLabel.TabStop = true;
        linkLabel.LinkClicked += (_, e) =>
        {
            if (e.Link is not null)
            {
                e.Link.Visited = true;
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
        };
    }

    private void InitializeMiningAnimation()
    {
        // The book animation is hidden when mining is off and shown while mining is on.
        _bookMiningAnimation.BringToFront();
    }

    private void LoadBrandMark()
    {
        // Loads the simple Slithy logo into the header if the embedded asset exists.
        using Stream? stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Slithy_Tove.Assets.slithy-mark.png");
        if (stream is not null)
        {
            brandPictureBox.Image = Image.FromStream(stream);
        }
    }

    private void LoadApplicationIcon()
    {
        using Stream? stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Slithy_Tove.Assets.slithy.ico");
        if (stream is null)
        {
            return;
        }

        Icon = new Icon(stream);
    }

    private void ResizeActivityColumns()
    {
        if (_walletActivityListView.Columns.Count != 3)
        {
            return;
        }

        int width = Math.Max(420, _walletActivityListView.ClientSize.Width -
                                  SystemInformation.VerticalScrollBarWidth - 6);
        _walletActivityListView.Columns[0].Width = (int)(width * .34);
        _walletActivityListView.Columns[1].Width = (int)(width * .28);
        _walletActivityListView.Columns[2].Width =
            width - _walletActivityListView.Columns[0].Width -
            _walletActivityListView.Columns[1].Width;
    }

    private async void Form1_Shown(object? sender, EventArgs e)
    {
        // This runs after the window first appears, so the user sees the app
        // before node checks, update checks, and last-wallet loading start.
        ResizeActivityColumns();
        UpdateInstaller.MarkCurrentVersionHealthy(
            _settingsStore.UpdateDirectory,
            Version.Parse(CurrentVersion));
        AppendLog("Slithy Tove desktop started.");
        AppendLog($"Version {CurrentVersion}");
        if (Environment.GetCommandLineArgs().Contains(
                "--update-rolled-back",
                StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show(this,
                "The attempted update did not start correctly, so Slithy restored the previous application files. Your wallet files were not changed.",
                "Update rolled back",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        await RefreshNodeStatusAsync();
        await ShowWalletAccessAsync(startupPrompt: true);

        if (InstallerUpdatesEnabled && _settings.CheckForUpdatesAutomatically)
        {
            await CheckForUpdatesAsync(showCurrentMessage: false);
        }
    }

    private string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";

    private async void refreshButton_Click(object? sender, EventArgs e)
    {
        SaveControlsToSettings();
        await RefreshNodeStatusAsync();
    }

    private async void statusTimer_Tick(object? sender, EventArgs e)
    {
        await RefreshNodeStatusAsync();
        if (_walletSnapshot is not null && !_walletOpening)
        {
            if (DateTimeOffset.UtcNow - _activityFilter.LastActivityUtc >=
                TimeSpan.FromMinutes(_settings.AutoLockMinutes))
            {
                await LockWalletAsync("Wallet locked after inactivity.");
                return;
            }
            await RefreshMiningStatusAsync();
            if (!_miningStartedByApp)
            {
                await RefreshWalletAsync();
            }
        }
    }

    private async void connectionModeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (connectionModeComboBox.SelectedIndex < 0)
        {
            return;
        }

        if (_walletSnapshot is not null)
        {
            try
            {
                await _walletRpcClient.CloseWalletAsync(WalletRpcUri);
            }
            catch
            {
                // The wallet service may already have stopped.
            }
            ClearWallet();
        }
        if (connectionModeComboBox.SelectedIndex == 2)
        {
            connectionModeComboBox.SelectedIndex = 0;
            return;
        }
        nodeAddressTextBox.Enabled = IsCustomNodeMode();
        nodeAddressTextBox.ReadOnly = !IsCustomNodeMode();
        officialNodeComboBox.Enabled = IsAutomaticOfficialNodeMode();
        startNodeButton.Enabled = false;
        stopNodeButton.Enabled = false;
        nodeAddressTextBox.Text = GetDisplayedRemoteNodeAddress();
        SetConnectionState("Checking connection", StatusKind.Neutral);
    }

    private async void officialNodeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_loadingSettingsIntoControls || officialNodeComboBox.SelectedIndex < 0)
        {
            return;
        }

        officialNodeComboBox.Enabled = false;
        miningToggleButton.Enabled = false;
        _settings.PublicNodePreference = officialNodeComboBox.SelectedIndex switch
        {
            1 => "borogove",
            2 => "mome",
            3 => "rath",
            _ => "auto"
        };
        _settingsStore.Save(_settings);
        AppendLog($"Public node preference changed to {officialNodeComboBox.Text}.");

        try
        {
            SetConnectionState("Checking connection", StatusKind.Neutral);
            nodeAddressTextBox.Text = GetDisplayedRemoteNodeAddress();

            if (_localNode.IsRunning)
            {
                await RestartLocalNodeAfterPublicNodeChangeAsync();
            }

            await RefreshNodeStatusAsync();
        }
        catch (Exception ex)
        {
            AppendLog($"Public node change failed: {ex.Message}");
            SetConnectionState("Connection needs a check", StatusKind.Error);
            statusMessageLabel.Text =
                "Slithy could not finish changing public nodes. Check the network tab and try again.";
            MessageBox.Show(this,
                $"Slithy could not finish changing public nodes.\n\n{ex.Message}",
                "Node change unavailable",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            officialNodeComboBox.Enabled = IsAutomaticOfficialNodeMode();
            miningToggleButton.Enabled = CanMine();
        }
    }

    private async Task RestartLocalNodeAfterPublicNodeChangeAsync()
    {
        Uri localNode = BuildLocalNodeUri();
        bool wasMining = _miningStartedByApp ||
                         miningToggleButton.Text.StartsWith("Stop", StringComparison.OrdinalIgnoreCase);

        _miningChangeInProgress = true;
        try
        {
            if (wasMining)
            {
                statusMessageLabel.Text = "Stopping mining before changing public nodes...";
                AppendLog("Stopping CPU mining before changing public nodes.");
                try
                {
                    await _rpcClient.StopMiningAsync(localNode);
                }
                catch (Exception ex) when (IsLocalNodeUnavailable(ex))
                {
                    AppendLog("Local node was already unavailable while stopping mining.");
                }

                _miningStartedByApp = false;
                _lastMiningBlocksCount = -1;
                _lastMiningRewardUtc = null;
                ShowMiningStopped();
                await RefreshWalletAfterMiningChangeAsync();
            }

            statusMessageLabel.Text = "Restarting the local node with the selected public peer...";
            AppendLog("Restarting local node so the selected public peer takes effect.");
            await _localNode.StopAsync(localNode);
            await Task.Delay(750);
            await SelectAutomaticPublicNodeAsync();
            await _localNode.StartAsync(_settings);
            startNodeButton.Enabled = false;
            stopNodeButton.Enabled = true;
            await WaitForLocalNodeAsync();
            await CatchUpLocalNodeAsync(localNode);
            await EnsureLocalChainMatchesPublicCheckpointAsync(localNode);
            await SyncLocalNodeToPublicCheckpointAsync(localNode);

            if (wasMining)
            {
                statusMessageLabel.Text =
                    "Public node changed. Mining is off so you can start it again when ready.";
                AppendLog("Public node changed. CPU mining remains off until you start it again.");
            }
        }
        finally
        {
            _miningChangeInProgress = false;
        }
    }

    private async void startNodeButton_Click(object? sender, EventArgs e)
    {
        await RefreshNodeStatusAsync();
    }

    private async void stopNodeButton_Click(object? sender, EventArgs e)
    {
        await RefreshNodeStatusAsync();
    }

    private async void walletButton_Click(object? sender, EventArgs e)
    {
        if (_walletSnapshot is null)
        {
            await ShowWalletAccessAsync(startupPrompt: false);
            return;
        }

        using WalletManageDialog dialog = new(_openWalletName);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        switch (dialog.SelectedAction)
        {
            case WalletManageAction.Backup:
                await BackUpOpenWalletAsync();
                break;
            case WalletManageAction.Restore:
                ImportWalletBackup();
                break;
            case WalletManageAction.Lock:
                await LockWalletAsync($"Locked wallet {_openWalletName}.");
                break;
            case WalletManageAction.OpenOther:
                await LockWalletAsync($"Closed wallet {_openWalletName}.");
                await ShowWalletAccessAsync(startupPrompt: false);
                break;
        }
    }

    private async Task ShowWalletAccessAsync(bool startupPrompt)
    {
        // Opens the wallet dialog. From here the user can create, open, or restore.
        if (_walletDialogOpen)
        {
            return;
        }

        _walletDialogOpen = true;
        walletButton.Enabled = false;
        WalletAccessDialog? dialog = null;
        try
        {
            dialog = new WalletAccessDialog(
                GetTransparentWalletDirectory(),
                _settings.LastWalletName,
                ReadLocalWalletNames());
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                if (startupPrompt)
                {
                    AppendLog("Wallet startup prompt was dismissed.");
                }
                return;
            }

            _walletOpening = true;
            ShowCachedWalletBalance(dialog.WalletName);
            SetWalletBusy(true, "Checking the local Slithy node...", 5);
            await EnsureWalletServiceAsync();
            Uri endpoint = WalletRpcUri;
            SetWalletBusy(true, "Opening the Slithy wallet service...", 5);
            if (_walletSnapshot is not null)
            {
                SetWalletBusy(true, "Closing the previous wallet...", 55);
                await _walletRpcClient.CloseWalletAsync(endpoint);
            }
            if (dialog.AccessMode == WalletAccessMode.Create)
            {
                SetWalletBusy(true, "Creating your new wallet...", 65);
                // Record this before creation so an interrupted recovery screen
                // cannot erase the reminder. Recovery words are not stored here.
                _settings.WalletsNeedingBackup ??= [];
                if (!_settings.WalletsNeedingBackup.Contains(dialog.WalletName))
                    _settings.WalletsNeedingBackup.Add(dialog.WalletName);
                _settingsStore.Save(_settings);
                string recoveryWords = await _walletRpcClient.CreateSeedWalletAsync(
                    endpoint,
                    dialog.WalletName,
                    dialog.WalletPassword,
                    TimeSpan.FromMinutes(_settings.AutoLockMinutes));
                WalletCompatibilityTools.MarkSeedWallet(GetTransparentWalletDirectory(), dialog.WalletName);
                AppendLog($"Created wallet {dialog.WalletName}.");
                using RecoveryWordsDialog recoveryDialog = new(dialog.WalletName, recoveryWords);
                if (recoveryDialog.ShowDialog(this) != DialogResult.OK)
                {
                    await _walletRpcClient.CloseWalletAsync(endpoint);
                    throw new OperationCanceledException("Recovery backup was not confirmed. Make a file backup before using this wallet.");
                }
                _settings.WalletsNeedingBackup.Remove(dialog.WalletName);
                _settingsStore.Save(_settings);
            }
            else if (dialog.AccessMode == WalletAccessMode.RestoreBackup)
            {
                SetWalletBusy(true, "Restoring wallet backup...", 65);
                WalletBackupTools.RestoreBackup(
                    dialog.RestoreSourceDirectory,
                    GetTransparentWalletDirectory(),
                    dialog.WalletName);
                SetWalletBusy(true, "Unlocking restored wallet...", 70);
                await _walletRpcClient.OpenWalletAsync(
                    endpoint,
                    dialog.WalletName,
                    dialog.WalletPassword,
                    TimeSpan.FromMinutes(_settings.AutoLockMinutes));
                AppendLog($"Restored wallet {dialog.WalletName}.");
            }
            else if (dialog.AccessMode == WalletAccessMode.RestoreWords)
            {
                SetWalletBusy(true, "Restoring recovery words...", 65);
                await _walletRpcClient.RestoreWalletAsync(
                    endpoint,
                    dialog.WalletName,
                    dialog.WalletPassword,
                    dialog.RestoreRecoveryWords,
                    0);
                WalletCompatibilityTools.MarkSeedWallet(GetTransparentWalletDirectory(), dialog.WalletName);
                AppendLog($"Restored wallet {dialog.WalletName} from recovery words.");
            }
            else
            {
                SetWalletBusy(true, "Unlocking your wallet...", 70);
                await _walletRpcClient.OpenWalletAsync(
                    endpoint,
                    dialog.WalletName,
                    dialog.WalletPassword,
                    TimeSpan.FromMinutes(_settings.AutoLockMinutes));
                AppendLog($"Opened wallet {dialog.WalletName}.");
            }
            _openWalletName = dialog.WalletName;
            _settings.LastWalletName = dialog.WalletName;
            _settingsStore.Save(_settings);
            SetWalletBusy(true, "Loading balance...", 88);
            await RefreshWalletAsync(forceRefresh: true, waitForSettledBalance: true);
            SetWalletBusy(true, "Wallet ready.", 100);
            walletButton.Text = "Manage";
            if (_settings.WalletsNeedingBackup?.Contains(_openWalletName) == true)
                MessageBox.Show(this, "This wallet's recovery backup was not confirmed. Open Manage and make a file backup before receiving funds.",
                    "Wallet backup needed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            lockWalletButton.Enabled = true;
            await ReconcilePendingSendAsync();
            sendButton.Enabled = !_networkLockedByUpdate && string.IsNullOrEmpty(_settings.PendingSendTransactionId);
            receiveButton.Enabled = true;
            miningToggleButton.Enabled = CanMine();
        }
        catch (Exception ex)
        {
            AppendLog($"Wallet open failed: {ex.Message}");
            MessageBox.Show(this,
                $"The wallet could not be opened.\n\n{FriendlyWalletError(ex.Message)}",
                "Could not open wallet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ClearWallet();
        }
        finally
        {
            dialog?.Dispose();
            _walletOpening = false;
            _walletDialogOpen = false;
            walletButton.Enabled = true;
            SetWalletBusy(false);
            if (_walletSnapshot is not null && !string.IsNullOrWhiteSpace(_openWalletName))
            {
                ApplyWalletSnapshotToDisplay(_walletSnapshot, saveCache: false);
            }
        }
    }

    private string[] ReadLocalWalletNames()
    {
        string walletDirectory = GetTransparentWalletDirectory();
        return Directory.EnumerateDirectories(walletDirectory)
            .Where(WalletCompatibilityTools.IsSeedWallet)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToArray();
    }

    private async void sendButton_Click(object? sender, EventArgs e)
    {
        // Send flow:
        // validate the address, prepare the transfer, ask for confirmation, then relay it.
        if (_walletSnapshot is null)
        {
            walletButton.PerformClick();
            return;
        }
        if (_networkLockedByUpdate)
        {
            MessageBox.Show(this,
                "A required Slithy update must be installed before sending.",
                "Update required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!string.IsNullOrEmpty(_settings.PendingSendTransactionId))
        {
            try
            {
                await ReconcilePendingSendAsync();
                if (string.IsNullOrEmpty(_settings.PendingSendTransactionId)) return;
                if (_settings.PendingSendWallet != _openWalletName || string.IsNullOrEmpty(_settings.PendingSendHex))
                    throw new InvalidOperationException($"Open wallet {_settings.PendingSendWallet} and check payment {_settings.PendingSendTransactionId} before making another payment.");
                if (MessageBox.Show(this,
                    $"The previous send has not been confirmed. Resubmit that same signed transaction? This cannot create a second payment.\n\n{_settings.PendingSendTransactionId}",
                    "Retry previous payment", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                SetWalletBusy(true, "Checking previous payment...", 50);
                await _walletRpcClient.RelayTransferAsync(WalletRpcUri,
                    new PreparedTransfer(_settings.PendingSendTransactionId, _settings.PendingSendFeeAtomic, _settings.PendingSendHex));
                ClearPendingSend();
                await RefreshWalletAsync(forceRefresh: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Previous payment still needs checking", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { SetWalletBusy(false); }
            return;
        }
        using SendDialog dialog = new(_walletSnapshot.UnlockedBalance);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        bool submitted = false;
        try
        {
            await ReconcilePendingSendAsync();
            if (!string.IsNullOrEmpty(_settings.PendingSendTransactionId))
                throw new InvalidOperationException($"Check previous payment {_settings.PendingSendTransactionId} before sending again.");
            decimal atomic = dialog.Amount * 100_000_000m;
            if (atomic <= 0 || atomic != decimal.Truncate(atomic))
                throw new InvalidOperationException("Enter a positive amount with at most eight decimal places.");
            ulong amountAtomic = checked((ulong)atomic);
            SetWalletBusy(true, "Checking address and network fee...", 30);
            AddressValidation validation = await _walletRpcClient.ValidateAddressAsync(
                WalletRpcUri, dialog.DestinationAddress);
            if (!validation.Valid)
            {
                MessageBox.Show(this,
                    "That is not a valid address for the current Slithy network.",
                    "Check the address", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            PreparedTransfer prepared = await _walletRpcClient.PrepareTransferAsync(
                WalletRpcUri, dialog.DestinationAddress, amountAtomic);
            decimal fee = prepared.FeeAtomic / 100_000_000m;
            DialogResult confirmation = MessageBox.Show(this,
                $"Send {dialog.Amount:N8} SLTHY to\n{dialog.DestinationAddress}?\n\nNetwork fee: {fee:N8} SLTHY\nTotal: {dialog.Amount + fee:N8} SLTHY",
                "Confirm transfer", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirmation != DialogResult.Yes)
            {
                AppendLog("Prepared transfer was not relayed.");
                return;
            }

            SetWalletBusy(true, "Sending Slithy...", 75);
            // Record the ID before broadcasting, so a lost response survives restart.
            _settings.PendingSendTransactionId = prepared.TransactionHash;
            _settings.PendingSendWallet = _openWalletName;
            _settings.PendingSendHex = prepared.TransactionMetadata;
            _settings.PendingSendFeeAtomic = prepared.FeeAtomic;
            _settingsStore.Save(_settings);
            submitted = true;
            TransferResult result = await _walletRpcClient.RelayTransferAsync(WalletRpcUri, prepared);
            ClearPendingSend();
            AppendLog($"Sent transaction {result.TransactionHash}.");
            MessageBox.Show(this,
                $"Transfer submitted.\n\nNetwork fee: {result.FeeAtomic / 100_000_000m:N8} SLTHY\nTransaction: {result.TransactionHash}",
                "Slithy sent", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await RefreshWalletAsync(forceRefresh: true);
        }
        catch (Exception ex)
        {
            AppendLog($"Transfer failed: {ex.Message}");
            MessageBox.Show(this, FriendlyWalletError(ex.Message),
                submitted ? "Check payment status before retrying" : "Transfer was not submitted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetWalletBusy(false);
        }
    }

    private async Task ReconcilePendingSendAsync()
    {
        if (string.IsNullOrEmpty(_settings.PendingSendTransactionId) || _settings.PendingSendWallet != _openWalletName) return;
        if (await _walletRpcClient.HasTransactionAsync(WalletRpcUri, _settings.PendingSendTransactionId))
        {
            AppendLog($"Confirmed previous payment {_settings.PendingSendTransactionId} in wallet history.");
            ClearPendingSend();
        }
        else AppendLog($"Previous send outcome is unknown: {_settings.PendingSendTransactionId}. Click Send to check or retry the same payment.");
    }

    private void ClearPendingSend()
    {
        _settings.PendingSendTransactionId = "";
        _settings.PendingSendWallet = "";
        _settings.PendingSendHex = "";
        _settings.PendingSendFeeAtomic = 0;
        _settingsStore.Save(_settings);
    }

    private async void lockWalletButton_Click(object? sender, EventArgs e)
        => await LockWalletAsync($"Locked wallet {_openWalletName}.");

    private async Task LockWalletAsync(string logMessage)
    {
        if (_walletSnapshot is null)
        {
            return;
        }
        try
        {
            await _walletRpcClient.LockWalletOnlyAsync(WalletRpcUri);
            await _walletRpcClient.CloseWalletAsync(WalletRpcUri);
            AppendLog(logMessage);
        }
        catch (Exception ex)
        {
            AppendLog($"Wallet lock warning: {ex.Message}");
            MessageBox.Show(this, "Wallet locking could not be confirmed. Check the local node before leaving this computer.",
                "Wallet lock not confirmed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        ClearWallet();
    }

    private void autoLockMinutesNumericUpDown_ValueChanged(object? sender, EventArgs e)
    {
        _settings.AutoLockMinutes = (int)autoLockMinutesNumericUpDown.Value;
        _settingsStore.Save(_settings);
    }

    private async void backupWalletButton_Click(object? sender, EventArgs e)
        => await BackUpOpenWalletAsync();

    private async Task BackUpOpenWalletAsync()
    {
        if (_walletSnapshot is null)
        {
            MessageBox.Show(this, "Open the wallet you want to back up first.",
                "Open a wallet", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using FolderBrowserDialog dialog = new()
        {
            Description = "Choose a private location for the wallet backup",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string walletName = _openWalletName;
        try
        {
            SetWalletBusy(true, "Creating wallet backup...", 50);
            string backupFolder = Path.Combine(
                dialog.SelectedPath,
                $"{walletName}-backup-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(backupFolder);
            string walletBackup = Path.Combine(backupFolder, walletName);
            Directory.CreateDirectory(walletBackup);
            await _walletRpcClient.BackupWalletAsync(WalletRpcUri, Path.Combine(walletBackup, "wallet.dat"));
            WalletCompatibilityTools.MarkSeedWallet(backupFolder, walletName);
            _settings.WalletsNeedingBackup?.Remove(walletName);
            _settingsStore.Save(_settings);
            await RefreshWalletAsync(forceRefresh: true);
            AppendLog($"Wallet backup created at {backupFolder}.");
            MessageBox.Show(this,
                $"Wallet backup created in:\n{backupFolder}\n\nKeep this folder private.",
                "Backup complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Wallet backup cancelled.");
        }
        catch (Exception ex)
        {
            AppendLog($"Wallet backup failed: {ex.Message}");
            MessageBox.Show(this, "The wallet backup could not be completed.",
                "Backup failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetWalletBusy(false);
        }
    }

    private void importWalletButton_Click(object? sender, EventArgs e)
        => ImportWalletBackup();

    private void ImportWalletBackup()
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "Choose a Slithy wallet backup folder",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        if (!WalletBackupTools.TryFindBackupWalletDirectory(
                dialog.SelectedPath,
                out string walletDirectory,
                out string walletName))
        {
            MessageBox.Show(this,
                "That folder does not look like a Slithy wallet backup. Choose the folder that contains wallet.dat, or the backup folder created by Slithy.",
                "Wallet backup not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            WalletBackupTools.RestoreBackup(walletDirectory, _settingsStore.WalletDirectory, walletName);
            MessageBox.Show(this,
                $"Imported {walletName}. Use Wallet to open it.",
                "Wallet imported", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (IOException)
        {
            MessageBox.Show(this,
                "A wallet with that name already exists. Rename the backup folder before importing.",
                "Wallet already exists", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private string GetTransparentWalletDirectory()
    {
        Directory.CreateDirectory(_settingsStore.WalletDirectory);
        return _settingsStore.WalletDirectory;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (string directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
        }
        foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDirectory, file);
            File.Copy(file, Path.Combine(destinationDirectory, relative), overwrite: false);
        }
    }

    private void receiveButton_Click(object? sender, EventArgs e)
    {
        if (_walletSnapshot is null)
        {
            walletButton.PerformClick();
            return;
        }
        using ReceiveDialog dialog = new(_walletSnapshot.Address);
        dialog.ShowDialog(this);
    }

    private async void miningToggleButton_Click(object? sender, EventArgs e)
    {
        // Mining must happen on this computer. Public nodes verify and relay.
        if (_walletSnapshot is null && !mineToAddressCheckBox.Checked)
        {
            walletButton.PerformClick();
            return;
        }
        if (_networkLockedByUpdate)
        {
            MessageBox.Show(this,
                "A required Slithy update must be installed before mining.",
                "Update required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        bool stopRequested = false;
        try
        {
            _miningChangeInProgress = true;
            miningToggleButton.Enabled = false;
            Uri miningNode = BuildLocalNodeUri();
            bool shouldStop = _miningStartedByApp ||
                              miningToggleButton.Text.StartsWith("Stop", StringComparison.OrdinalIgnoreCase);
            stopRequested = shouldStop;
            if (shouldStop)
            {
                await _rpcClient.StopMiningAsync(miningNode);
                _miningStartedByApp = false;
                _lastMiningBlocksCount = -1;
                AppendLog("CPU mining stopped.");
                ShowMiningStopped();
                await RefreshWalletAfterMiningChangeAsync();
            }
            else
            {
                EnsureEnoughDiskSpaceForMining();
                ShowMiningStarting();
                await EnsureLocalMiningNodeAsync();
                string miningAddress = await GetMiningRewardAddressAsync(miningNode);
                int threads = MiningThreadCount();
                await _rpcClient.StartMiningAsync(miningNode, miningAddress, threads);
                _miningStartedByApp = true;
                _lastMiningWalletRefreshUtc = DateTimeOffset.MinValue;
                _lastMiningBlocksCount = -1;
                _lastMiningRewardUtc = null;
                AppendLog($"CPU mining started with {threads} worker{(threads == 1 ? "" : "s")}.");
                ShowMiningStarted(threads, 0);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Mining control failed: {ex.Message}");
            if (stopRequested && IsLocalNodeUnavailable(ex))
            {
                miningTitleLabel.Text = "Stop not confirmed";
                miningDescriptionLabel.Text = "The local node did not answer. Slithy will check again.";
                miningToggleButton.Text = "Stop mining";
                _bookMiningAnimation.Mining = false;
                SetConnectionState("Checking connection", StatusKind.Neutral);
                statusMessageLabel.Text =
                    "The local node did not answer. Mining state could not be confirmed.";
                AppendLog("The stop request could not be confirmed. Mining state is unknown.");
                return;
            }

            MessageBox.Show(this,
                $"Mining could not be changed.\n\nSlithy mines on this computer, not on the public nodes.\n\n{ex.Message}",
                "Mining unavailable",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            _miningChangeInProgress = false;
            miningToggleButton.Enabled = CanMine();
        }
    }

    private void EnsureEnoughDiskSpaceForMining()
    {
        DiskSpaceSnapshot disk = GetDiskSpaceSnapshot();
        ApplyDiskSpaceSnapshot(disk);
        if (disk.FreeBytes >= MinimumFreeBytesForMining)
        {
            return;
        }

        decimal neededGb = MinimumFreeBytesForMining / 1024m / 1024m / 1024m;
        throw new InvalidOperationException(
            $"Windows has {FormatBytes(disk.FreeBytes)} free on {disk.DriveName}. " +
            $"The local blockchain folder is using {FormatBytes(disk.BlockchainBytes)}. " +
            $"Free at least {neededGb:N0} GB before mining.");
    }

    private async void UpdateDiskSpaceDisplay()
    {
        if (_diskScanInProgress || IsDisposed || _exitRequested) return;
        _diskScanInProgress = true;
        try
        {
            bool scan = DateTime.UtcNow - _lastDiskScanUtc > TimeSpan.FromMinutes(1);
            DiskSpaceSnapshot snapshot = await Task.Run(() => GetDiskSpaceSnapshot(scan));
            if (IsDisposed || _exitRequested) return;
            if (scan) _lastDiskScanUtc = DateTime.UtcNow;
            ApplyDiskSpaceSnapshot(snapshot);
        }
        catch (Exception ex)
        {
            if (IsDisposed || _exitRequested) return;
            blockchainDataValueLabel.Text = "Could not read blockchain folder size.";
            freeDiskValueLabel.Text = "Could not read free disk space.";
            freeDiskValueLabel.ForeColor = Color.FromArgb(181, 76, 69);
            Debug.WriteLine(ex);
        }
        finally { _diskScanInProgress = false; }
    }

    private void ApplyDiskSpaceSnapshot(DiskSpaceSnapshot disk)
    {
        blockchainDataValueLabel.Text =
            $"{FormatBytes(disk.BlockchainBytes)} in {disk.DataDirectory}";
        freeDiskValueLabel.Text =
            $"{FormatBytes(disk.FreeBytes)} free on {disk.DriveName} " +
            $"of {FormatBytes(disk.TotalBytes)} total";
        freeDiskValueLabel.ForeColor = disk.FreeBytes < MinimumFreeBytesForMining
            ? Color.FromArgb(181, 76, 69)
            : Color.FromArgb(42, 48, 41);
    }

    private DiskSpaceSnapshot GetDiskSpaceSnapshot(bool scanFolder = false)
    {
        string dataDirectory = _settingsStore.NodeDataDirectory;
        string fullDataDirectory = Path.GetFullPath(dataDirectory);
        string root = Path.GetPathRoot(fullDataDirectory) ?? "";
        DriveInfo drive = new(root);
        if (scanFolder)
        {
            long size = Directory.Exists(fullDataDirectory) ? GetDirectorySize(fullDataDirectory) : 0;
            Interlocked.Exchange(ref _cachedBlockchainBytes, size);
        }
        long blockchainBytes = Interlocked.Read(ref _cachedBlockchainBytes);

        return new DiskSpaceSnapshot(
            drive.Name,
            fullDataDirectory,
            blockchainBytes,
            drive.AvailableFreeSpace,
            drive.TotalSize);
    }

    private static long GetDirectorySize(string directory)
    {
        long total = 0;
        try
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch
                {
                    // The live node can lock or remove files while the UI reads sizes.
                }
            }
        }
        catch
        {
            // The folder may not exist yet on a first launch.
        }

        return total;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        decimal value = Math.Max(0, bytes);
        int unit = 0;
        while (value >= 1024m && unit < units.Length - 1)
        {
            value /= 1024m;
            unit++;
        }

        return unit == 0
            ? $"{value:N0} {units[unit]}"
            : $"{value:N2} {units[unit]}";
    }

    private static bool IsLocalNodeUnavailable(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException ||
                current is SocketException socketException &&
                socketException.SocketErrorCode is SocketError.ConnectionRefused or SocketError.ConnectionReset)
            {
                return true;
            }

            if (current.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("connection refused", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<string> GetMiningRewardAddressAsync(Uri miningNode)
    {
        if (!mineToAddressCheckBox.Checked)
        {
            if (_walletSnapshot is null)
            {
                throw new InvalidOperationException("Open a wallet before mining.");
            }

            return _walletSnapshot.Address;
        }

        string address = miningAddressTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException("Paste the Slithy address that should receive mining rewards.");
        }

        AddressValidation validation = await _walletRpcClient.ValidateAddressAsync(miningNode, address);
        if (!validation.Valid)
        {
            throw new InvalidOperationException("The mining reward address is not valid for this Slithy network.");
        }

        return address;
    }

    private async void checkUpdatesButton_Click(object? sender, EventArgs e) =>
        await CheckForUpdatesAsync(showCurrentMessage: true);

    private void automaticUpdatesCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_loadingSettingsIntoControls)
        {
            return;
        }

        _settings.CheckForUpdatesAutomatically =
            InstallerUpdatesEnabled && automaticUpdatesCheckBox.Checked;
        _settingsStore.Save(_settings);
    }

    private void minimizeToTrayOnCloseCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_loadingSettingsIntoControls)
        {
            return;
        }

        _settings.MinimizeToTrayOnClose = minimizeToTrayOnCloseCheckBox.Checked;
        _settingsStore.Save(_settings);
    }

    private async Task CheckForUpdatesAsync(bool showCurrentMessage)
    {
        // Update flow:
        // download signed manifest, verify signature, download package, verify hash, then restart into updater.
        if (!InstallerUpdatesEnabled)
        {
            updateStatusLabel.Text = "Automatic updates are inactive.";
            if (showCurrentMessage)
            {
                MessageBox.Show(this,
                    "Automatic updates are inactive in this build.",
                    "Updates inactive",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            return;
        }

        if (_checkingForUpdates)
        {
            return;
        }

        _checkingForUpdates = true;
        checkUpdatesButton.Enabled = false;
        SetUpdateProgress("Checking for Slithy updates...", 15);
        updateStatusLabel.Text = "Checking for updates...";
        try
        {
            if (!_settings.UpdatesConfigured)
            {
                updateStatusLabel.Text = "Updates are waiting for the release feed.";
                if (showCurrentMessage)
                {
                    MessageBox.Show(this,
                        "The update system is ready, but the signed release feed is not available yet.",
                        "Updates not configured", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            SetUpdateProgress("Verifying the signed release feed...", 30);
            UpdateCheckResult result = await _updateService.CheckAsync(
                new Uri(_settings.UpdateManifestUrl),
                ReleaseTrust.UpdatePublicKeyPem,
                Version.Parse(CurrentVersion));

            _settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            _settingsStore.Save(_settings);

            if (!result.UpdateAvailable)
            {
                _networkLockedByUpdate = false;
                updateStatusLabel.Text = $"Slithy is up to date. Version {CurrentVersion}.";
                SetUpdateProgress("Slithy is up to date.", 100);
                if (showCurrentMessage)
                {
                    MessageBox.Show(this, updateStatusLabel.Text, "No update needed",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            UpdateManifest manifest = result.Manifest!;
            string updateLevel = UpdateService.GetEffectiveUpdateLevel(manifest);
            updateStatusLabel.Text = $"Version {manifest.Version} is available.";
            bool shouldInstall = PromptForUpdateInstall(manifest, updateLevel);
            if (!shouldInstall)
            {
                HoldNetworkForDeclinedUpdate(updateLevel);
                return;
            }

            if (shouldInstall)
            {
                SetUpdateProgress($"Downloading Slithy {manifest.Version}...", 35);
                Progress<int> progress = new(percent =>
                {
                    int scaled = 35 + Math.Clamp(percent, 0, 100) * 40 / 100;
                    SetUpdateProgress($"Downloading update... {percent}%", scaled);
                    updateStatusLabel.Text = $"Downloading Slithy {manifest.Version}... {percent}%";
                });
                string packagePath = await _updateService.DownloadVerifiedPackageAsync(manifest, progress);
                SetUpdateProgress("Verifying update package...", 80);
                updateStatusLabel.Text = "Update downloaded and verified.";
                AppendLog($"Verified update saved to {packagePath}");
                SetUpdateProgress("Preparing to restart with the update...", 92);
                await StartUpdateHandoffAsync(packagePath, manifest);
            }
        }
        catch (Exception ex)
        {
            updateStatusLabel.Text = "Update check could not be completed.";
            AppendLog($"Update check failed: {ex.Message}");
            if (showCurrentMessage)
            {
                MessageBox.Show(this,
                    "Slithy could not securely check for updates. No files were changed.",
                    "Update check failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _checkingForUpdates = false;
            checkUpdatesButton.Enabled = true;
            if (!_updateHandoffRequested)
            {
                SetWalletBusy(false);
                UpdateUpdateProgressFill(0);
            }
        }
    }

    private bool PromptForUpdateInstall(UpdateManifest manifest, string updateLevel)
    {
        string title = updateLevel switch
        {
            "required" => "Slithy update required",
            "blocked" => "Slithy update required",
            "recommended" => "Slithy update recommended",
            _ => "Slithy update available"
        };
        string reason = updateLevel switch
        {
            "required" => "Install this update before mining or sending.",
            "blocked" => "Install this update before using the Slithy network.",
            "recommended" => "Install now, or choose No and update later from Settings.",
            _ => "Install now, or choose No and update later from Settings."
        };
        DialogResult choice = MessageBox.Show(this,
            $"Slithy {manifest.Version} is available.\n\n{manifest.ReleaseNotes}\n\n{reason}\n\nDownload, install, and restart now?",
            title,
            MessageBoxButtons.YesNo,
            updateLevel is "required" or "blocked"
                ? MessageBoxIcon.Warning
                : MessageBoxIcon.Information);
        return choice == DialogResult.Yes;
    }

    private void HoldNetworkForDeclinedUpdate(string updateLevel)
    {
        if (updateLevel is not ("required" or "blocked"))
        {
            _networkLockedByUpdate = false;
            updateStatusLabel.Text = "Update skipped. You can check again from Settings.";
            return;
        }

        _networkLockedByUpdate = true;
        string message = updateLevel == "blocked"
            ? "This version must update before using the Slithy network."
            : "Update required before mining or sending.";
        updateStatusLabel.Text = message;
        statusMessageLabel.Text = message;
        AppendLog(message);
        sendButton.Enabled = false;
        miningToggleButton.Enabled = false;
    }

    private void openDataFolderButton_Click(object? sender, EventArgs e)
    {
        Directory.CreateDirectory(_settingsStore.AppDataDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _settingsStore.AppDataDirectory,
            UseShellExecute = true
        });
    }

    private void exportLogButton_Click(object? sender, EventArgs e)
    {
        using SaveFileDialog dialog = new()
        {
            Title = "Export Slithy diagnostics",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"slithy-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, BuildDiagnosticsReport());
        AppendLog($"Diagnostics exported to {dialog.FileName}.");
    }

    private void reportBugButton_Click(object? sender, EventArgs e)
    {
        string diagnostics = BuildDiagnosticsReport();
        string body = string.Join(Environment.NewLine,
        [
            "Please describe what broke:",
            "",
            "What were you doing?",
            "",
            "What did you expect?",
            "",
            "What happened instead?",
            "",
            "Diagnostics:",
            diagnostics.Length > 7000 ? diagnostics[..7000] + Environment.NewLine + "[Diagnostics trimmed. Export diagnostics and attach the full file if needed.]" : diagnostics
        ]);

        string mailto =
            "mailto:support@slithy.io?subject=" +
            Uri.EscapeDataString("Slithy Tove bug report") +
            "&body=" +
            Uri.EscapeDataString(body);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = mailto,
                UseShellExecute = true
            });
            AppendLog("Opened a bug report email to support@slithy.io.");
        }
        catch (Exception ex)
        {
            Clipboard.SetText(body);
            AppendLog($"Could not open email app: {ex.Message}");
            MessageBox.Show(
                this,
                "Windows could not open an email app. The bug report text was copied to the clipboard. Send it to support@slithy.io.",
                "Bug report copied",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private string BuildDiagnosticsReport()
    {
        return string.Join(Environment.NewLine,
        [
            "Slithy Tove diagnostics",
            $"Created: {DateTimeOffset.Now:O}",
            $"App version: {CurrentVersion}",
            $"Core version: {versionValueLabel.Text}",
            $"Connection: {connectionModeComboBox.Text}",
            $"Public node: {GetPublicNodePreferenceDisplayText()}",
            $"Chain height: {heightValueLabel.Text}",
            $"Difficulty: {difficultyValueLabel.Text}",
            $"Network: {networkValueLabel.Text}",
            $"Sync: {syncValueLabel.Text}",
            $"Blockchain data: {blockchainDataValueLabel.Text}",
            $"Free disk space: {freeDiskValueLabel.Text}",
            $"Mining: {miningTitleLabel.Text}",
            $"Wallet open: {(_walletSnapshot is null ? "No" : "Yes")}",
            "",
            "Technical log",
            logTextBox.Text
        ]);
    }

    private void clearLogButton_Click(object? sender, EventArgs e) => logTextBox.Clear();

    private Uri WalletRpcUri => BuildLocalNodeUri();

    private async Task EnsureWalletServiceAsync()
    {
        SetWalletBusy(true, "Starting the local Slithy node...", 10);
        await StartLocalNodeIfNeededAsync();
        SetWalletBusy(true, "Wallet service is ready on this computer...", 60);
    }

    private async Task RefreshWalletAsync(
        bool forceRefresh = false,
        bool waitForSettledBalance = false,
        long? rescanStartHeight = null)
    {
        // Refresh the open wallet balance. A cached balance is shown first when possible
        // so the app does not look empty while the real value is loading.
        if (_walletRefreshing)
        {
            if (!waitForSettledBalance)
            {
                return;
            }

            for (int attempt = 0; attempt < 50 && _walletRefreshing; attempt++)
            {
                await Task.Delay(100);
            }

            if (_walletRefreshing)
            {
                AppendLog("Wallet refresh is still busy.");
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(_openWalletName) ||
            (_walletOpening && !waitForSettledBalance))
        {
            return;
        }

        _walletRefreshing = true;
        bool refreshFlagStillOwned = true;
        try
        {
            if (forceRefresh)
            {
                await _walletRpcClient.RefreshAsync(WalletRpcUri, rescanStartHeight);
            }
            _walletSnapshot = waitForSettledBalance
                ? await GetSettledWalletSnapshotAsync()
                : await _walletRpcClient.GetSnapshotAsync(WalletRpcUri);
            if (_walletOpening && !waitForSettledBalance)
            {
                return;
            }
            ApplyWalletSnapshotToDisplay(_walletSnapshot, saveCache: true);
            _walletRefreshing = false;
            refreshFlagStillOwned = false;
            try
            {
                IReadOnlyList<WalletTransfer> transfers = await _walletRpcClient.GetTransfersAsync(WalletRpcUri);
                PopulateRecentActivity(transfers);
            }
            catch (Exception ex)
            {
                AppendLog($"Activity refresh failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Wallet refresh failed: {ex.Message}");
            if (waitForSettledBalance) throw;
        }
        finally
        {
            if (refreshFlagStillOwned)
            {
                _walletRefreshing = false;
            }
        }
    }

    private void ShowCachedWalletBalance(string walletName)
    {
        walletNameLabel.Text = walletName;
        walletAddressPreviewLabel.Text = "Address loading...";
        if (_settings.WalletBalanceCache.TryGetValue(walletName, out WalletBalanceCacheEntry? cached))
        {
            balanceValueLabel.Text = $"{cached.Balance:N4} SLTHY";
            availableBalanceLabel.Text =
                $"Last known balance from {cached.UpdatedUtc.LocalDateTime:g}. Updating wallet history...";
            if (!string.IsNullOrWhiteSpace(cached.Address))
            {
                _walletRpcClient.UseKnownAddress(cached.Address);
                walletAddressPreviewLabel.Text = ShortenAddress(cached.Address);
            }
            return;
        }

        balanceValueLabel.Text = "Balance updating...";
        availableBalanceLabel.Text = "Opening your wallet and checking its history.";
    }

    private void ApplyWalletSnapshotToDisplay(WalletSnapshot snapshot, bool saveCache)
    {
        // Takes wallet data from RPC and copies it into the labels on the Home tab.
        balanceValueLabel.Text = $"{snapshot.Balance:N4} SLTHY";
        availableBalanceLabel.Text = snapshot.BlocksToUnlock < 0
            ? $"{snapshot.UnlockedBalance:N4} ready to use. Mining rewards are still maturing."
            : snapshot.BlocksToUnlock > 0
            ? $"{snapshot.UnlockedBalance:N4} ready to use. More available in {snapshot.BlocksToUnlock} blocks."
            : $"{snapshot.UnlockedBalance:N4} ready to use";
        walletNameLabel.Text = _openWalletName;
        walletAddressPreviewLabel.Text = ShortenAddress(snapshot.Address);

        if (!saveCache || string.IsNullOrWhiteSpace(_openWalletName))
        {
            return;
        }

        _settings.WalletBalanceCache[_openWalletName] = new WalletBalanceCacheEntry
        {
            WalletName = _openWalletName,
            Address = snapshot.Address,
            BalanceAtomic = snapshot.BalanceAtomic,
            UnlockedBalanceAtomic = snapshot.UnlockedBalanceAtomic,
            BlocksToUnlock = snapshot.BlocksToUnlock,
            NodeHeight = snapshot.LastProcessedBlockHeight,
            UpdatedUtc = DateTimeOffset.UtcNow
        };
        if (snapshot.LastProcessedBlockHeight > 0)
        {
            _lastWalletRefreshHeight = snapshot.LastProcessedBlockHeight;
        }
        _settingsStore.Save(_settings);
    }

    private async Task<WalletSnapshot> GetSettledWalletSnapshotAsync()
    {
        NodeInfo nodeInfo = await _rpcClient.GetInfoAsync(BuildLocalNodeUri(), TimeSpan.FromSeconds(5));
        long targetHeight = nodeInfo.Height;
        SetWalletBusy(true, "Scanning wallet history...", 90);
        for (int attempt = 0; attempt < 120; attempt++)
        {
            WalletScanStatus scan = await _walletRpcClient.GetScanStatusAsync(WalletRpcUri);
            if (!scan.Scanning)
            {
                break;
            }

            decimal heightProgress = targetHeight <= 0
                ? scan.Progress
                : Math.Clamp(scan.LastProcessedBlockHeight / (decimal)targetHeight, 0m, 1m);
            decimal progressValue = Math.Max(scan.Progress, heightProgress);
            int scanProgress = 90 + (int)Math.Round(progressValue * 6m, MidpointRounding.AwayFromZero);
            SetWalletBusy(true,
                $"Syncing wallet history {Math.Min(scan.LastProcessedBlockHeight, targetHeight):N0}/{targetHeight:N0} blocks...",
                Math.Clamp(scanProgress, 90, 96));
            await Task.Delay(500);
        }

        SetWalletBusy(true, "Loading current balance...", 98);
        return await _walletRpcClient.GetSnapshotAsync(WalletRpcUri);
    }

    private async Task RefreshMiningStatusAsync()
    {
        if (_miningChangeInProgress)
        {
            return;
        }

        try
        {
            MiningInfo mining = await _rpcClient.GetMiningInfoAsync(BuildLocalNodeUri());
            if (mining.Active)
            {
                TrackMiningBlocks(mining.Blocks);
                _miningStartedByApp = true;
                ShowMiningStarted(Math.Max(1, mining.Threads), mining.Speed);
                await RefreshWalletForMiningProgressAsync();
            }
            else
            {
                _miningStartedByApp = false;
                ShowMiningStopped();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            AppendLog($"Mining status refresh failed: {ex.Message}");
            miningTitleLabel.Text = "Checking miner";
            miningDescriptionLabel.Text = "The local node did not answer. Mining state is unknown.";
            miningStatusLineLabel.Text = "Slithy will check again. You can still request a stop.";
            miningToggleButton.Text = "Stop mining";
            _bookMiningAnimation.Mining = false;
        }
    }

    private async Task RefreshWalletAfterMiningChangeAsync()
    {
        try
        {
            await RefreshWalletAsync(forceRefresh: true);
        }
        catch (Exception ex)
        {
            AppendLog($"Wallet refresh after mining change failed: {ex.Message}");
        }
    }

    private async Task RefreshWalletForMiningProgressAsync()
    {
        if (_walletSnapshot is null ||
            _walletOpening ||
            DateTimeOffset.UtcNow - _lastMiningWalletRefreshUtc < TimeSpan.FromSeconds(4))
        {
            return;
        }

        _lastMiningWalletRefreshUtc = DateTimeOffset.UtcNow;
        NodeInfo nodeInfo = await _rpcClient.GetInfoAsync(BuildLocalNodeUri(), TimeSpan.FromSeconds(3));
        if (nodeInfo.Height <= _lastWalletRefreshHeight)
        {
            return;
        }

        long startHeight = Math.Max(0, _lastWalletRefreshHeight + 1);
        await _walletRpcClient.RefreshAsync(WalletRpcUri, startHeight);
        ulong priorBalance = _walletSnapshot.BalanceAtomic;
        _walletSnapshot = await _walletRpcClient.GetSnapshotAsync(WalletRpcUri);
        if (_walletSnapshot.BalanceAtomic > priorBalance)
        {
            _lastMiningRewardUtc = DateTimeOffset.Now;
        }
        ApplyWalletSnapshotToDisplay(_walletSnapshot, saveCache: true);
        if (_miningStartedByApp)
        {
            miningStatusLineLabel.Text = BuildMiningDestinationText(active: true);
        }
    }

    private void TrackMiningBlocks(long blocks)
    {
        if (_lastMiningBlocksCount >= 0 && blocks > _lastMiningBlocksCount)
        {
            _lastMiningRewardUtc = DateTimeOffset.Now;
        }

        _lastMiningBlocksCount = blocks;
    }

    private void ShowMiningStarted(int threads, ulong speed)
    {
        miningTitleLabel.Text = "Mining is on";
        miningDescriptionLabel.Text = speed > 0
            ? $"Mining at {speed:N0} H/s with {threads} worker{(threads == 1 ? "" : "s")}."
            : $"Mining with {threads} worker{(threads == 1 ? "" : "s")}. Waiting for a block.";
        miningStatusLineLabel.Text = BuildMiningDestinationText(active: true);
        miningToggleButton.Text = "Stop mining";
        miningEffortComboBox.Enabled = false;
        _bookMiningAnimation.Mining = true;
    }

    private void ShowMiningStarting()
    {
        miningTitleLabel.Text = "Starting miner";
        miningDescriptionLabel.Text = "Starting miner...";
        miningStatusLineLabel.Text = BuildMiningDestinationText(active: true);
        miningToggleButton.Text = "Starting...";
        miningEffortComboBox.Enabled = false;
        _bookMiningAnimation.Mining = true;
    }

    private void ShowMiningStopped()
    {
        miningTitleLabel.Text = "Earn Slithy";
        miningDescriptionLabel.Text =
            mineToAddressCheckBox.Checked
                ? "Use this computer's CPU and send rewards to the address below."
                : "Use this computer's CPU and send rewards to this wallet.";
        miningStatusLineLabel.Text = BuildMiningDestinationText(active: false);
        miningToggleButton.Text = "Start mining";
        miningToggleButton.Enabled = CanMine();
        miningEffortComboBox.Enabled = true;
        _bookMiningAnimation.Mining = false;
    }

    private string BuildMiningDestinationText(bool active)
    {
        string destination = mineToAddressCheckBox.Checked
            ? "another Slithy address"
            : _walletSnapshot is null
                ? "this wallet after it opens"
                : "this wallet";
        string prefix = active ? "To " : "Ready. To ";
        string reward = _lastMiningRewardUtc.HasValue
            ? $" Last reward: {_lastMiningRewardUtc.Value.LocalDateTime:h:mm tt}."
            : "";
        return $"{prefix}{destination}.{reward} Rewards mature after 100 blocks.";
    }

    private void PopulateRecentActivity(IReadOnlyList<WalletTransfer> transfers)
    {
        // Fills the Activity tab. The newest transactions are shown at the top.
        PopulateActivityList(_walletActivityListView, transfers);
        _activityEmptyLabel.Visible = transfers.Count == 0;
        _walletActivityListView.Visible = transfers.Count > 0;
        UpdateLatestActivity(transfers.FirstOrDefault());
    }

    private void UpdateLatestActivity(WalletTransfer? transfer)
    {
        // Home page shows one short recent event to keep it simple.
        if (transfer is null)
        {
            _latestActivityDot.BackColor = Color.FromArgb(151, 145, 130);
            _latestActivitySummaryLabel.Text =
                "Nothing has happened yet. Your first payment will appear here.";
            return;
        }

        bool sent = transfer.Direction is "Sent" or "Sending";
        string amount = $"{transfer.Amount:N4} SLTHY";
        _latestActivityDot.BackColor = sent
            ? Color.FromArgb(201, 130, 71)
            : Color.FromArgb(75, 139, 94);
        string summary = sent
            ? $"You sent {amount}"
            : $"You received {amount}";
        _latestActivitySummaryLabel.Text = transfer.Timestamp is null
            ? $"{summary} and it is waiting for confirmation."
            : $"{summary} on {transfer.Timestamp.Value.LocalDateTime:g}.";
    }

    private static void PopulateActivityList(
        ListView listView,
        IEnumerable<WalletTransfer> transfers)
    {
        WalletTransfer[] activity = transfers.ToArray();
        listView.BeginUpdate();
        listView.Items.Clear();
        foreach (WalletTransfer transfer in activity)
        {
            string sign = transfer.Direction is "Sent" or "Sending" ? "-" : "+";
            ListViewItem item = new(transfer.Direction);
            item.SubItems.Add($"{sign}{transfer.Amount:N4} SLTHY");
            item.SubItems.Add(transfer.Timestamp?.LocalDateTime.ToString("g") ?? "Pending");
            item.ToolTipText = transfer.TransactionHash;
            item.Tag = transfer;
            listView.Items.Add(item);
        }
        if (activity.Length == 0)
        {
            ListViewItem empty = new("No activity yet");
            empty.SubItems.Add("");
            empty.SubItems.Add("");
            listView.Items.Add(empty);
        }
        listView.EndUpdate();
    }

    private void recentActivityListView_DoubleClick(object? sender, EventArgs e)
        => OpenSelectedActivity(recentActivityListView);

    private void activityListView_DoubleClick(object? sender, EventArgs e)
        => OpenSelectedActivity(_walletActivityListView);

    private void OpenSelectedActivity(ListView listView)
    {
        if (listView.SelectedItems.Count == 1 &&
            listView.SelectedItems[0].Tag is WalletTransfer transfer)
        {
            using TransactionDetailsDialog dialog = new(
                _walletRpcClient, WalletRpcUri, transfer);
            dialog.ShowDialog(this);
        }
    }

    private int MiningThreadCount()
    {
        int processors = Math.Max(1, Environment.ProcessorCount);
        return miningEffortComboBox.SelectedIndex switch
        {
            0 => 1,
            2 => processors,
            _ => Math.Max(1, processors / 2)
        };
    }

    private void SetWalletBusy(bool busy, string? message = null, int progressPercent = -1)
    {
        // Turns wallet controls off during long work so the user cannot start two operations at once.
        UseWaitCursor = busy;
        walletButton.Enabled = !busy;
        lockWalletButton.Enabled = !busy && _walletSnapshot is not null;
        sendButton.Enabled = !busy && !_networkLockedByUpdate && _walletSnapshot is not null;
        receiveButton.Enabled = !busy && _walletSnapshot is not null;
        miningToggleButton.Enabled = !busy && CanMine();
        if (!string.IsNullOrWhiteSpace(message))
        {
            availableBalanceLabel.Text = message;
            _operationProgressLabel.Text = message;
        }
        if (progressPercent >= 0)
        {
            _operationProgressPercent = Math.Clamp(progressPercent, 0, 100);
        }
        else if (busy && _operationProgressPercent == 0)
        {
            _operationProgressPercent = 10;
        }
        else if (!busy)
        {
            _operationProgressPercent = 0;
        }
        UpdateOperationProgressFill();
        _operationProgressPanel.Visible = busy;
        if (busy)
        {
            _operationProgressPanel.BringToFront();
            _operationProgressPanel.Refresh();
            _operationProgressLabel.Refresh();
            _operationProgressTrack.Refresh();
        }
    }

    private void SetUpdateProgress(string message, int progressPercent)
    {
        SetWalletBusy(true, message, progressPercent);
        updateStatusLabel.Text = message;
        UpdateUpdateProgressFill(progressPercent);
    }

    private void UpdateUpdateProgressFill(int progressPercent)
    {
        int availableWidth = Math.Max(0, updateProgressTrackPanel.ClientSize.Width);
        updateProgressFillPanel.Width = availableWidth * Math.Clamp(progressPercent, 0, 100) / 100;
        updateProgressTrackPanel.Refresh();
    }

    private void UpdateOperationProgressFill()
    {
        int availableWidth = Math.Max(0, _operationProgressTrack.ClientSize.Width);
        _operationProgressFill.Width = availableWidth * Math.Clamp(_operationProgressPercent, 0, 100) / 100;
    }

    private void ClearWallet()
    {
        // Clears wallet labels and buttons when no wallet is open.
        _walletSnapshot = null;
        _openWalletName = "";
        _lastWalletRefreshHeight = 0;
        balanceValueLabel.Text = "0.0000 SLTHY";
        availableBalanceLabel.Text = "Open a wallet to see your balance.";
        walletNameLabel.Text = "No wallet open";
        walletAddressPreviewLabel.Text = "Your address will appear here.";
        walletButton.Text = "Open wallet";
        lockWalletButton.Enabled = false;
        sendButton.Enabled = false;
        receiveButton.Enabled = false;
        miningToggleButton.Enabled = CanMine();
        _bookMiningAnimation.Mining = false;
        recentActivityListView.Items.Clear();
        _walletActivityListView.Items.Clear();
        _activityEmptyLabel.Visible = true;
        _walletActivityListView.Visible = false;
        UpdateLatestActivity(null);
    }

    private static string ShortenAddress(string address) =>
        address.Length > 34 ? $"{address[..18]}...{address[^14..]}" : address;

    private static string FriendlyWalletError(string message)
    {
        if (message.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            return "The password was not accepted.";
        }
        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return "The wallet file could not be found.";
        }
        if (message.Contains("not enough", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("insufficient", StringComparison.OrdinalIgnoreCase))
        {
            return "There is not enough unlocked Slithy for this transfer and its network fee.";
        }
        return message;
    }

    private async Task WaitForLocalNodeAsync()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                await _rpcClient.GetInfoAsync(BuildLocalNodeUri(), TimeSpan.FromSeconds(2));
                return;
            }
            catch
            {
                // Wait until the local RPC endpoint is ready.
            }

            if (!_localNode.IsRunning)
            {
                throw new InvalidOperationException("The local node stopped before it was ready.");
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("The local node did not become ready within 15 seconds.");
    }

    private async Task StartLocalNodeIfNeededAsync()
    {
        Uri localNode = BuildLocalNodeUri();
        try
        {
            await _rpcClient.GetInfoAsync(localNode, TimeSpan.FromSeconds(2));
            await CatchUpLocalNodeAsync(localNode);
            await EnsureLocalChainMatchesPublicCheckpointAsync(localNode);
            await SyncLocalNodeToPublicCheckpointAsync(localNode);
            return;
        }
        catch
        {
            // No local node is answering yet.
        }

        await SelectAutomaticPublicNodeAsync();
        await _localNode.StartAsync(_settings);
        startNodeButton.Enabled = false;
        stopNodeButton.Enabled = true;
        await WaitForLocalNodeAsync();
        await CatchUpLocalNodeAsync(localNode);
        await EnsureLocalChainMatchesPublicCheckpointAsync(localNode);
        await SyncLocalNodeToPublicCheckpointAsync(localNode);
    }

    private async Task SelectAutomaticPublicNodeAsync()
    {
        if (!_settings.PublicNodePreference.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        List<(PublicNodeChoice Node, long Milliseconds)> reachable = [];
        foreach (PublicNodeChoice node in AppSettings.PublicNodes)
        {
            long? time = await MeasurePublicNodeAsync(node);
            if (time.HasValue)
            {
                reachable.Add((node, time.Value));
            }
        }

        if (reachable.Count == 0)
        {
            AppendLog("Automatic public node check found no reachable public nodes.");
            return;
        }

        (PublicNodeChoice selected, long milliseconds) = reachable
            .OrderBy(item => item.Milliseconds)
            .First();
        if (!_settings.LastAutomaticPublicNodeId.Equals(selected.Id, StringComparison.OrdinalIgnoreCase))
        {
            _settings.LastAutomaticPublicNodeId = selected.Id;
            _settingsStore.Save(_settings);
        }

        AppendLog($"Automatic public node chose {selected.Name} ({selected.Region}) in {milliseconds:N0} ms.");
    }

    private static async Task<long?> MeasurePublicNodeAsync(PublicNodeChoice node)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            using TcpClient client = new();
            await client.ConnectAsync(node.Host, node.P2pPort).WaitAsync(TimeSpan.FromSeconds(3));
            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }
        catch
        {
            return null;
        }
    }

    private async Task CatchUpLocalNodeAsync(Uri localNode)
    {
        try
        {
            ChainProgress progress = await _rpcClient.GetChainProgressAsync(localNode, TimeSpan.FromSeconds(3));
            if (progress.Headers > progress.Height)
            {
                bool fetched = await _rpcClient.TryFetchHeaderKnownBlocksAsync(localNode, 200);
                if (fetched)
                {
                    AppendLog("Local node fetched missing blocks from a peer.");
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Local node catch-up check skipped: {ex.Message}");
        }
    }

    private async Task EnsureLocalChainMatchesPublicCheckpointAsync(Uri localNode)
    {
        NodeInfo info = await _rpcClient.GetInfoAsync(localNode, TimeSpan.FromSeconds(5));
        if (info.Height < AppSettings.PublicCheckpointHeight)
        {
            return;
        }

        string localCheckpointHash = await _rpcClient.GetBlockHashAsync(
            localNode,
            AppSettings.PublicCheckpointHeight,
            TimeSpan.FromSeconds(5));
        if (localCheckpointHash.Equals(AppSettings.PublicCheckpointHash, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            "This node belongs to a different beta network. Close the previous Slithy app and install the matching beta release. Your wallet and chain files were not moved.");
    }

    private async Task SyncLocalNodeToPublicCheckpointAsync(Uri localNode)
    {
        for (int attempt = 0; attempt < 90; attempt++)
        {
            ChainProgress progress = await _rpcClient.GetChainProgressAsync(localNode, TimeSpan.FromSeconds(5));
            if (progress.Height >= AppSettings.PublicCheckpointHeight)
            {
                string checkpointHash = await _rpcClient.GetBlockHashAsync(
                    localNode,
                    AppSettings.PublicCheckpointHeight,
                    TimeSpan.FromSeconds(5));
                if (checkpointHash.Equals(AppSettings.PublicCheckpointHash, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "The running node is on a different beta network. Install the matching release before opening this wallet. No chain files were moved.");
            }

            SetWalletBusy(true,
                $"Syncing local chain {progress.Height:N0}/{AppSettings.PublicCheckpointHeight:N0} blocks...",
                Math.Clamp(35 + (int)(progress.Height * 40 / Math.Max(1, AppSettings.PublicCheckpointHeight)), 35, 75));

            if (progress.Headers > progress.Height)
            {
                await _rpcClient.TryFetchHeaderKnownBlocksAsync(localNode, 25);
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException("The local node has not reached the public Slithy checkpoint yet. Try again after it syncs.");
    }

    private async Task EnsureLocalMiningNodeAsync()
    {
        Uri localNode = BuildLocalNodeUri();
        AppendLog("Checking the local Slithy mining node on this computer...");
        await StartLocalNodeIfNeededAsync();

        for (int attempt = 0; attempt < 60; attempt++)
        {
            NodeInfo info = await _rpcClient.GetInfoAsync(localNode, TimeSpan.FromSeconds(3));
            if (info.Synchronized)
            {
                await CatchUpLocalNodeAsync(localNode);
                NodeInfo current = await _rpcClient.GetInfoAsync(localNode, TimeSpan.FromSeconds(3));
                if (!current.Synchronized) continue;
                await EnsureLocalMiningPeerAsync(localNode, current.Height);
                AppendLog("Local Slithy mining node is ready.");
                return;
            }

            miningDescriptionLabel.Text =
                $"Starting local mining node, synced to block {info.Height:N0}.";
            await Task.Delay(1000);
        }

        throw new TimeoutException("The local mining node is still catching up. Try mining again in a moment.");
    }

    private async Task EnsureLocalMiningPeerAsync(Uri localNode, long localHeight)
    {
        PeerNetworkStatus peers = await _rpcClient.GetPeerNetworkStatusAsync(localNode, localHeight);
        string? problem = NodeRpcClient.MiningPeerProblem(peers, localHeight);
        if (problem is not null) throw new InvalidOperationException(problem);
    }

    private async Task<bool> RefreshNodeStatusAsync()
    {
        // Picks a healthy node, shows chain status, and refreshes treasury information.
        if (_refreshing)
        {
            return false;
        }

        _refreshing = true;
        try
        {
            NodeSelection selection = await GetHealthyNodeAsync();
            NodeInfo info = selection.Info;
            UpdateDiskSpaceDisplay();
            _consecutiveNodeRefreshFailures = 0;
            _lastSuccessfulNodeRefreshUtc = DateTimeOffset.UtcNow;
            SetConnectionState(
                info.Synchronized ? "Everything is ready" : info.Height == 0 ? "Waiting for beta peers" : "Catching up",
                info.Synchronized ? StatusKind.Success : StatusKind.Neutral);
            string nodeKind = selection.Uri.IsLoopback ? "local node" : "custom node";
            string peerDisplay = selection.Uri.IsLoopback
                ? $" Public peer preference: {GetPublicNodePreferenceDisplayText()}."
                : "";
            statusMessageLabel.Text = info.Synchronized
                ? $"Slithy is connected to the {nodeKind} at {selection.DisplayName}.{peerDisplay} Difficulty retargets every {BetaDifficultyRetargetBlocks} blocks."
                : info.Height == 0
                    ? "Waiting for compatible beta peers. Your app and the nodes must use the same beta network."
                    : $"Slithy is connected to the {nodeKind} at {selection.DisplayName} and catching up.{peerDisplay}";
            heightValueLabel.Text = info.Height.ToString("N0");
            difficultyValueLabel.Text = FormatDifficulty(info.Difficulty);
            networkValueLabel.Text = FormatNetwork(info.NetworkType);
            syncValueLabel.Text = info.Synchronized ? "Ready" : info.Height == 0 ? "Waiting for peers" : "Catching up";
            versionValueLabel.Text = info.Version;
            poolValueLabel.Text = info.TransactionPoolSize.ToString("N0");
            targetValueLabel.Text = $"{info.Target} seconds";
            lastCheckedValueLabel.Text = DateTime.Now.ToString("h:mm:ss tt");
            try
            {
                PublicTreasuryStatus? publicTreasury =
                    await _treasuryStatusService.TryGetPublicStatusAsync(_settings.TreasuryStatusUrl);
                if (publicTreasury is not null && publicTreasury.ChainHeight >= info.Height)
                {
                    treasuryStatusLabel.Text =
                        $"Literacy fund: {publicTreasury.CurrentBalance:N2} SLTHY";
                }
                else
                {
                    TreasuryStatus treasury = TreasuryStatusCalculator.FromChainHeight(info.Height);
                    treasuryStatusLabel.Text =
                        $"Literacy fund: {treasury.Accrued:N2} SLTHY est.";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                TreasuryStatus treasury = TreasuryStatusCalculator.FromChainHeight(info.Height);
                treasuryStatusLabel.Text =
                    $"Literacy fund: {treasury.Accrued:N2} SLTHY est.";
            }
            miningToggleButton.Enabled = info.Synchronized && CanMine();
            return true;
        }
        catch (Exception ex)
        {
            _consecutiveNodeRefreshFailures++;
            UpdateDiskSpaceDisplay();
            bool recentlyHealthy =
                DateTimeOffset.UtcNow - _lastSuccessfulNodeRefreshUtc < TimeSpan.FromMinutes(1);
            bool miningLikelyStillRunning = _miningStartedByApp || _bookMiningAnimation.Mining;
            bool shouldShowHardFailure =
                !miningLikelyStillRunning &&
                (_consecutiveNodeRefreshFailures >= 3 || !recentlyHealthy);
            if (shouldShowHardFailure)
            {
                SetConnectionState("Local node is offline", StatusKind.Error);
                statusMessageLabel.Text = IsCustomNodeMode()
                    ? "Slithy cannot reach the custom node. Check the address, port, and firewall, then try Refresh."
                    : "Slithy cannot reach the local node on this computer. Check free disk space, then try Refresh or restart Slithy.";
                ClearNodeDetails();
                miningToggleButton.Enabled = false;
            }
            else
            {
                SetConnectionState("Checking connection", StatusKind.Neutral);
                statusMessageLabel.Text =
                    miningLikelyStillRunning
                        ? "Mining status could not be confirmed. Slithy is retrying the local node."
                        : $"Slithy is retrying the network. Last good connection was {_lastSuccessfulNodeRefreshUtc.LocalDateTime:h:mm:ss tt}.";
            }
            lastCheckedValueLabel.Text = DateTime.Now.ToString("h:mm:ss tt");
            startNodeButton.Enabled = false;
            stopNodeButton.Enabled = false;
            Debug.WriteLine(ex);
            return false;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async Task<NodeSelection> GetHealthyNodeAsync()
    {
        // Automatic mode uses the local node. It connects to public peers over P2P.
        Uri localUri = BuildLocalNodeUri();
        try
        {
            NodeInfo localInfo = await _rpcClient.GetInfoAsync(localUri, TimeSpan.FromSeconds(3));
            return new NodeSelection(localUri, localInfo, GetNodeDisplayName(localUri));
        }
        catch (Exception ex) when (_localNode.IsRunning)
        {
            AppendLog($"Local node unavailable: {ex.Message}");
        }

        if (IsCustomNodeMode())
        {
            Uri customUri = BuildNodeUri();
            NodeInfo customInfo = await _rpcClient.GetInfoAsync(customUri, TimeSpan.FromSeconds(5));
            await RememberRemoteNodeAsync(customUri);
            return new NodeSelection(customUri, customInfo, GetNodeDisplayName(customUri));
        }

        await StartLocalNodeIfNeededAsync();
        NodeInfo startedLocalInfo = await _rpcClient.GetInfoAsync(localUri, TimeSpan.FromSeconds(5));
        return new NodeSelection(localUri, startedLocalInfo, GetNodeDisplayName(localUri));
    }

    private List<string> BuildOfficialNodeCandidates()
    {
        List<string> candidates = [];
        void Add(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                !candidates.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(value.Trim());
            }
        }

        if (!LooksLikeUnauthenticatedKnownOfficialNode(_settings.LastSuccessfulRemoteNodeAddress))
        {
            Add(_settings.LastSuccessfulRemoteNodeAddress);
        }
        foreach (string node in _settings.OfficialNodeAddresses)
        {
            Add(node);
        }
        Add(_settings.RemoteNodeAddress);
        return candidates;
    }

    private static bool LooksLikeUnauthenticatedKnownOfficialNode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(uri.UserInfo) &&
               (uri.Host.Equals("192.168.2.75", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("node1.slithy.io", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("borogove.slithy.io", StringComparison.OrdinalIgnoreCase)) &&
               uri.Port == 53426;
    }

    private async Task RememberRemoteNodeAsync(Uri uri)
    {
        string address = uri.ToString().TrimEnd('/');
        bool changed = !address.Equals(_activeRemoteNodeAddress, StringComparison.OrdinalIgnoreCase) &&
                       !address.Equals(_settings.LastSuccessfulRemoteNodeAddress, StringComparison.OrdinalIgnoreCase);
        _activeRemoteNodeAddress = address;
        _settings.LastSuccessfulRemoteNodeAddress = address;
        if (IsAutomaticOfficialNodeMode())
        {
            nodeAddressTextBox.Text = GetDisplayedRemoteNodeAddress();
        }
        _settingsStore.Save(_settings);

        await Task.CompletedTask;
    }

    private sealed record NodeSelection(Uri Uri, NodeInfo Info, string DisplayName);

    private static string GetNodeDisplayName(Uri uri)
    {
        return uri.Host.ToLowerInvariant() switch
        {
            "borogove.slithy.io" => "Borogove",
            "mome.slithy.io" => "Mome",
            "rath.slithy.io" => "Rath",
            "127.0.0.1" => "this computer",
            "localhost" => "this computer",
            "node1.slithy.io" => "Borogove",
            "node2.slithy.io" => "Mome",
            "node3.slithy.io" => "Rath",
            _ => uri.Authority
        };
    }

    private Uri BuildNodeUri()
    {
        // This is the one place that decides which node URL the app should use right now.
        string value = IsCustomNodeMode()
            ? nodeAddressTextBox.Text.Trim()
            : GetDisplayedRemoteNodeAddress();
        if (!TryBuildNodeUri(value, out Uri? uri))
        {
            throw new InvalidOperationException("Enter a valid node address.");
        }
        return uri!;
    }

    private Uri BuildBetaTestnetNodeUri()
    {
        if (!TryBuildNodeUri(_settings.BetaNodeAddress, out Uri? uri))
        {
            throw new InvalidOperationException("The shared Slithy node address is invalid.");
        }
        return uri!;
    }

    private Uri BuildLocalNodeUri()
    {
        string user = Uri.EscapeDataString(_settings.RpcUser);
        string password = Uri.EscapeDataString(_settings.RpcPassword);
        return new Uri($"http://{user}:{password}@127.0.0.1:{_settings.LocalRpcPort}");
    }

    private static bool TryBuildNodeUri(string value, out Uri? uri)
    {
        value = value.Trim();
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = $"http://{value}";
        }

        return Uri.TryCreate(value.TrimEnd('/'), UriKind.Absolute, out uri);
    }

    private void LoadSettingsIntoControls()
    {
        // Copies saved settings into checkboxes, text boxes, and dropdowns.
        _loadingSettingsIntoControls = true;
        try
        {
            connectionModeComboBox.SelectedIndex = _settings.NodeConnectionMode switch
            {
                "Custom" => 1,
                _ => 0
            };
            officialNodeComboBox.SelectedIndex = _settings.PublicNodePreference.ToLowerInvariant() switch
            {
                "borogove" => 1,
                "mome" => 2,
                "rath" => 3,
                _ => 0
            };
            officialNodeComboBox.Enabled = IsAutomaticOfficialNodeMode();
            nodeAddressTextBox.Text = GetDisplayedRemoteNodeAddress();
            nodeAddressTextBox.Enabled = IsCustomNodeMode();
            nodeAddressTextBox.ReadOnly = !IsCustomNodeMode();
            startNodeButton.Enabled = false;
            stopNodeButton.Enabled = false;
            automaticUpdatesCheckBox.Checked = InstallerUpdatesEnabled && _settings.CheckForUpdatesAutomatically;
            automaticUpdatesCheckBox.Enabled = InstallerUpdatesEnabled;
            minimizeToTrayOnCloseCheckBox.Checked = _settings.MinimizeToTrayOnClose;
            autoLockMinutesNumericUpDown.Value = Math.Clamp(_settings.AutoLockMinutes, 1, 120);
            miningEffortComboBox.SelectedIndex = 1;
            mineToAddressCheckBox.Checked = _settings.MineToCustomAddress;
            miningAddressTextBox.Text = _settings.CustomMiningAddress;
            UpdateCustomMiningAddressControls();
            ClearWallet();
            installedVersionLabel.Text = $"Installed version {CurrentVersion}";
            updateStatusLabel.Text = InstallerUpdatesEnabled
                ? _settings.UpdatesConfigured
                    ? "Updates are checked securely."
                    : "Update service awaiting release configuration."
                : "Automatic updates are inactive.";
            UpdateDiskSpaceDisplay();
        }
        finally
        {
            _loadingSettingsIntoControls = false;
        }
    }

    private void SaveControlsToSettings()
    {
        // Copies the current controls back into the settings object and writes it to disk.
        _settings.NodeConnectionMode = connectionModeComboBox.SelectedIndex switch
        {
            1 => "Custom",
            _ => "AutomaticOfficial"
        };
        _settings.PublicNodePreference = officialNodeComboBox.SelectedIndex switch
        {
            1 => "borogove",
            2 => "mome",
            3 => "rath",
            _ => "auto"
        };
        _settings.UseLocalNode = false;
        if (IsCustomNodeMode())
        {
            _settings.RemoteNodeAddress = nodeAddressTextBox.Text.Trim();
        }
        _settings.CheckForUpdatesAutomatically =
            InstallerUpdatesEnabled && automaticUpdatesCheckBox.Checked;
        _settings.MinimizeToTrayOnClose = minimizeToTrayOnCloseCheckBox.Checked;
        _settings.MineToCustomAddress = mineToAddressCheckBox.Checked;
        _settings.CustomMiningAddress = miningAddressTextBox.Text.Trim();
        _settingsStore.Save(_settings);
    }

    private void mineToAddressCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_loadingSettingsIntoControls)
        {
            return;
        }

        UpdateCustomMiningAddressControls();
        SaveControlsToSettings();
    }

    private void miningAddressTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_loadingSettingsIntoControls)
        {
            return;
        }

        miningToggleButton.Enabled = CanMine();
        _settings.CustomMiningAddress = miningAddressTextBox.Text.Trim();
        _settingsStore.Save(_settings);
    }

    private void UpdateCustomMiningAddressControls()
    {
        bool custom = mineToAddressCheckBox.Checked;
        miningAddressLabel.Enabled = custom;
        miningAddressTextBox.Enabled = custom;
        if (!_miningStartedByApp)
        {
            ShowMiningStopped();
        }
        else
        {
            miningStatusLineLabel.Text = BuildMiningDestinationText(active: true);
        }
        miningToggleButton.Enabled = CanMine();
    }

    private void SetBusy(bool busy)
    {
        UseWaitCursor = busy;
        refreshButton.Enabled = !busy;
        connectionModeComboBox.Enabled = !busy;
        officialNodeComboBox.Enabled = !busy && IsAutomaticOfficialNodeMode();
        nodeAddressTextBox.Enabled = !busy && IsCustomNodeMode();
        nodeAddressTextBox.ReadOnly = !IsCustomNodeMode();
    }

    private bool IsAutomaticOfficialNodeMode() => connectionModeComboBox.SelectedIndex == 0;

    private bool IsCustomNodeMode() => connectionModeComboBox.SelectedIndex == 1;



    private bool CanMine()
    {
        if (_networkLockedByUpdate || _miningChangeInProgress || !IsMiningAvailable())
        {
            return false;
        }

        if (mineToAddressCheckBox.Checked)
        {
            return !string.IsNullOrWhiteSpace(miningAddressTextBox.Text);
        }

        return _walletSnapshot is not null;
    }

    private static bool IsMiningAvailable() => RealCpuMinerAvailable;

    private string GetDisplayedRemoteNodeAddress()
    {
        if (IsCustomNodeMode())
        {
            return _settings.RemoteNodeAddress;
        }

        return GetPublicNodePreferenceDisplayText();
    }

    private string GetPublicNodePreferenceDisplayText()
    {
        if (_settings.PublicNodePreference.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            PublicNodeChoice? selected = AppSettings.PublicNodes.FirstOrDefault(
                node => node.Id.Equals(_settings.LastAutomaticPublicNodeId, StringComparison.OrdinalIgnoreCase));
            return selected is null
                ? "Automatic closest"
                : $"Automatic closest, currently {selected.DisplayText}";
        }

        PublicNodeChoice? preferred = AppSettings.PublicNodes.FirstOrDefault(
            node => node.Id.Equals(_settings.PublicNodePreference, StringComparison.OrdinalIgnoreCase));
        return preferred?.DisplayText ?? "Automatic closest";
    }

    private void ClearNodeDetails()
    {
        heightValueLabel.Text = "0";
        difficultyValueLabel.Text = "0";
        networkValueLabel.Text = "Unknown";
        syncValueLabel.Text = "Unknown";
        versionValueLabel.Text = "Unknown";
        poolValueLabel.Text = "0";
        targetValueLabel.Text = "Unknown";
    }

    private void SetConnectionState(string text, StatusKind kind)
    {
        connectionStatusLabel.Text = text;
        homeStatusLabel.Text = text;
        Color color = kind switch
        {
            StatusKind.Success => Color.FromArgb(54, 133, 91),
            StatusKind.Error => Color.FromArgb(181, 76, 69),
            _ => Color.FromArgb(128, 121, 108)
        };
        connectionDotPanel.BackColor = color;
        homeStatusDotPanel.BackColor = color;
    }

    private static string FormatNetwork(string networkType) =>
        networkType.Equals("fakechain", StringComparison.OrdinalIgnoreCase)
            ? "Local test network"
            : networkType;

    private static string FormatDifficulty(decimal difficulty) =>
        difficulty switch
        {
            >= 1 => difficulty.ToString("N2"),
            > 0 => difficulty.ToString("0.########"),
            _ => "0"
        };

    private void ApplyCozyStyling()
    {
        // Rounded corners cannot be set in the WinForms designer, so they stay here.
        Control[] softPanels =
        [
            homeStatusCard,
            balanceCard,
            miningCard,
            recentActivityCard,
            updatesCard
        ];
        foreach (Control panel in softPanels)
        {
            ApplyRoundedRegion(panel, 22);
            panel.Resize += (_, _) => ApplyRoundedRegion(panel, 22);
        }

        Button[] softButtons =
        [
            walletButton,
            lockWalletButton,
            sendButton,
            receiveButton,
            miningToggleButton,
            refreshButton,
            startNodeButton,
            stopNodeButton,
            checkUpdatesButton,
            backupWalletButton,
            importWalletButton,
            openDataFolderButton,
            exportLogButton,
            clearLogButton
        ];
        foreach (Button button in softButtons)
        {
            ApplyRoundedRegion(button, 14);
            button.Resize += (_, _) => ApplyRoundedRegion(button, 14);
        }
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0)
        {
            return;
        }

        using System.Drawing.Drawing2D.GraphicsPath path = new();
        int diameter = radius * 2;
        Rectangle bounds = new(0, 0, control.Width, control.Height);
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        control.Region?.Dispose();
        control.Region = new Region(path);
    }

    private void AppendLog(string message)
    {
        // Adds a timestamped line to the Activity log box.
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(message));
            return;
        }
        logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private async Task StartUpdateHandoffAsync(string packagePath, UpdateManifest manifest)
    {
        // Creates the pending-update file, then starts a new copy of the app in updater mode.
        string pendingPath = UpdateInstaller.CreatePendingUpdate(
            _settingsStore.UpdateDirectory,
            CurrentVersion,
            manifest.Version,
            Application.ExecutablePath,
            manifest.Sha256);
        string helperDirectory = Path.Combine(_settingsStore.UpdateDirectory, "helper-" + Guid.NewGuid().ToString("N"));
        // Installed builds need their managed assemblies and runtime files beside
        // the app host. Copying the EXE by itself cannot start the updater.
        string helperExecutable = UpdateInstaller.CreateHelperCopy(AppContext.BaseDirectory, helperDirectory, Path.GetFileName(Application.ExecutablePath));
        ProcessStartInfo helper = new()
        {
            FileName = helperExecutable,
            WorkingDirectory = helperDirectory,
            UseShellExecute = false
        };
        helper.ArgumentList.Add("--apply-update");
        helper.ArgumentList.Add($"--installer={packagePath}");
        helper.ArgumentList.Add($"--pending={pendingPath}");
        helper.ArgumentList.Add($"--parent={Environment.ProcessId}");
        using Process helperProcess = Process.Start(helper)
            ?? throw new InvalidOperationException("Windows did not start the update helper.");
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (!File.Exists(pendingPath + ".ready"))
        {
            if (helperProcess.HasExited || DateTime.UtcNow >= deadline)
            {
                if (!helperProcess.HasExited)
                {
                    helperProcess.Kill(entireProcessTree: true);
                    await helperProcess.WaitForExitAsync();
                }
                throw new InvalidOperationException("The update helper did not become ready. Slithy has stayed open.");
            }
            await Task.Delay(100);
        }
        _updateHandoffRequested = true;
        _exitRequested = true;
        Close();
    }

    internal void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        ShowInTaskbar = true;
    }

    internal void ExitFromTray()
    {
        _exitRequested = true;
        Close();
    }

    private void trayIcon_DoubleClick(object? sender, EventArgs e)
    {
        RestoreFromTray();
    }

    private void openSlithyToveToolStripMenuItem_Click(object? sender, EventArgs e)
    {
        RestoreFromTray();
    }

    private void exitToolStripMenuItem_Click(object? sender, EventArgs e)
    {
        ExitFromTray();
    }

    private void MinimizeToTray()
    {
        Hide();
        ShowInTaskbar = false;
        _trayIcon.ShowBalloonTip(
            2500,
            "Slithy Tove is still running",
            "Mining and network activity can continue in the background. Use the tray icon to reopen or exit.",
            ToolTipIcon.Info);
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        // Clicking X can hide to the tray. The user can turn that off in Settings.
        if (!_exitRequested && e.CloseReason == CloseReason.UserClosing && _settings.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            MinimizeToTray();
            return;
        }

        if (_cleanupComplete)
        {
            base.OnFormClosing(e);
            return;
        }

        e.Cancel = true;
        Enabled = false;
        statusTimer.Stop();
        try
        {
            SaveControlsToSettings();
            if (_miningStartedByApp)
            {
                try
                {
                    await _rpcClient.StopMiningAsync(BuildLocalNodeUri());
                    _miningStartedByApp = false;
                    AppendLog("Mining stopped during application exit.");
                }
                catch (Exception ex)
                {
                    AppendLog($"Mining stop warning during exit: {ex.Message}");
                }
            }
            if (_walletSnapshot is not null)
            {
                try
                {
                    await _walletRpcClient.CloseWalletAsync(WalletRpcUri);
                }
                catch
                {
                    // The wallet service may already be stopping.
                }
            }
            if (_localNode.IsRunning)
            {
                await _localNode.StopAsync(BuildLocalNodeUri());
            }
            _rpcClient.Dispose();
            _walletRpcClient.Dispose();
            _updateService.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayMenu.Dispose();
            Application.RemoveMessageFilter(_activityFilter);
            _cleanupComplete = true;
            BeginInvoke(Close);
        }
        catch (Exception ex)
        {
            Enabled = true;
            AppendLog($"Shutdown failed: {ex.Message}");
            MessageBox.Show(this,
                $"Slithy could not finish shutting down.\n\n{ex.Message}",
                "Shutdown failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private enum StatusKind { Neutral, Success, Error }
}

