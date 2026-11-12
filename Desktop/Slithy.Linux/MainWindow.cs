using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Slithy_Tove;
using Slithy.Diagnostics;

namespace Slithy.Linux;

internal sealed partial class MainWindow : Window
{
    private readonly LinuxSettingsStore _store;
    private readonly LinuxSettings _settings;
    private readonly string? _settingsError;
    private readonly LinuxNodeHost _host;
    private readonly AppDiagnosticLog? _diagnostics;
    private readonly Func<LinuxStorageInfo> _readStorage;
    private readonly NodeRpcClient _node = new();
    private readonly WalletRpcClient _wallet = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly bool _preview, _regtest;
    private Uri? _endpoint;
    private bool _busy, _polling, _closing, _locked = true, _ready, _trayConfirmed, _paymentBlocked = true;
    private bool _startupStarted;
    private CancellationTokenSource? _pollCancellation;
    private long _refreshGeneration;
    private string _walletName = "";
    private WalletSnapshot? _snapshot;
    private MiningInfo? _mining;
    private bool _miningStatusUnknown;
    private NodeInfo? _chain;
    private DateTimeOffset _lastInput = DateTimeOffset.UtcNow, _lastTreasury;
    private PreparedTransfer? _pending;
    private string _pendingAddress = "";
    private decimal _pendingAmount;
    private TrayIcon? _tray;

    private readonly TextBlock _status = Dialogs.Text("Starting your local node...", 18);
    private readonly TextBlock _balance = Dialogs.Text("Open a wallet", 34);
    private readonly TextBlock _available = Dialogs.Text("Your coins stay on the chain when the wallet locks.");
    private readonly TextBlock _walletStatus = Dialogs.Text("No wallet open");
    private readonly TextBlock _miningTitle = Dialogs.Text("Earn Slithy", 23);
    private readonly TextBlock _miningStatus = Dialogs.Text("Mining runs on this computer's CPU.");
    private readonly TextBlock _treasury = Dialogs.Text("Literacy fund: checking...");
    private readonly TextBlock _network = Dialogs.Text("Waiting for the local node.");
    private readonly TextBlock _storage = Dialogs.Text("");
    private readonly TextBlock _log = Dialogs.Text("");
    private readonly TextBlock _activity = Dialogs.Text("Open a wallet to see transactions.");
    private readonly ProgressBar _sync = new() { Minimum = 0, Maximum = 100, Height = 6, Foreground = Dialogs.Ink };
    private readonly Button _walletButton = Dialogs.Button("Open wallet");
    private readonly Button _lockButton = Dialogs.Button("Lock");
    private readonly Button _sendButton = Dialogs.Button("Send");
    private readonly Button _receiveButton = Dialogs.Button("Receive");
    private readonly Button _miningButton = Dialogs.Button("Start mining");
    private readonly TextBlock _miningSettingsText = new()
    {
        Foreground = Dialogs.Ink, TextWrapping = TextWrapping.Wrap,
        TextDecorations = TextDecorations.Underline, FontSize = 13
    };
    private readonly Button _miningSettingsLink = new()
    {
        Name = "MiningSettingsLink", Background = Brushes.Transparent, BorderThickness = new Thickness(0),
        Padding = new Thickness(0, 4), MinHeight = 30, HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Left, Cursor = new Cursor(StandardCursorType.Hand)
    };
    private readonly TabControl _mainTabs = new() { Margin = new Thickness(8, 4, 8, 0) };
    private TabItem _settingsTab = null!;
    private Border _miningSettingsCard = null!;
    private readonly Slider _miningThreads = new()
    {
        Name = "MiningThreads", Minimum = 1, Maximum = Math.Max(1, Environment.ProcessorCount),
        TickFrequency = 1, IsSnapToTickEnabled = true, SmallChange = 1, LargeChange = 1,
        HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 36
    };
    private readonly TextBlock _threadCount = Dialogs.Text("");
    private readonly ComboBox _peers = new() { ItemsSource = new[] { "Automatic, closest", "Borogove (East Coast)", "Mome (West Coast)", "Rath (Europe)" }, MinWidth = 280 };
    private readonly CheckBox _custom = new() { Content = Dialogs.Text("Mine to another Slithy address") };
    private readonly TextBox _miningAddress = new() { Watermark = "Mining address", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox _autoLock = new() { Content = "Automatically lock my wallet after inactivity" };
    private readonly NumericUpDown _minutes = new() { Minimum = 1, Maximum = 1440, Increment = 1, Width = 110 };
    private readonly CheckBox _traySetting = new() { Content = "Closing the window sends Slithy to the notification area" };

    internal string DisplayedStatus => _status.Text ?? "";

    public MainWindow(bool preview = false, bool regtest = false, LinuxSettingsStore? store = null)
    {
        _preview = preview; _regtest = regtest;
        _store = store ?? new LinuxSettingsStore();
        _diagnostics = preview ? null : new AppDiagnosticLog(Path.Combine(_store.DataDirectory, "diagnostics"));
        try { _settings = preview ? new LinuxSettings() : _store.Load(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Leave the original file untouched and show the failure in a usable window.
            _settings = new LinuxSettings();
            _settingsError = ex.Message;
            _status.Text = "Settings could not be loaded. Your wallet files have not been changed.";
        }
        _settings.Validate();
        _host = new LinuxNodeHost(_store, _settings, regtest);
        _readStorage = _host.GetStorage;
        _host.ConfirmOrphanRecoveryAsync = () => Dialogs.Confirm(this, "Reopen your local node?",
            "A node from your previous Slithy session is still running. Stop it safely and reopen? Mining on that node will stop.",
            "Stop node and reopen");
        Title = "Slithy Tove | Linux GUI beta";
        Icon = Branding.WindowIcon;
        Width = 980; Height = 720; MinWidth = 760; MinHeight = 620;
        FontSize = 14;
        Background = Brush.Parse("#f3efe4"); Foreground = Dialogs.Ink;
        _balance.FontFamily = new FontFamily("Georgia, DejaVu Serif, Liberation Serif");
        SizeChanged += (_, _) => _balance.FontSize = Bounds.Width < 900 ? 26 : 34;
        _sendButton.Background = Dialogs.Ink; _sendButton.Foreground = Dialogs.Paper;
        _miningButton.Background = Dialogs.Ink; _miningButton.Foreground = Dialogs.Paper;
        _miningButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _walletButton.Name = "ManageWallet"; _miningButton.Name = "MiningToggle";
        _balance.Name = "WalletBalance"; _status.Name = "NetworkStatus";
        _miningStatus.Name = "MiningDetails";
        _miningSettingsLink.Content = _miningSettingsText;
        AutomationProperties.SetName(_miningSettingsLink, "Change CPU mining threads in Settings");
        AutomationProperties.SetHelpText(_miningSettingsLink, "Open the CPU mining slider. Changes apply the next time you start mining.");
        _miningSettingsLink.Click += (_, _) => ShowMiningSettings();
        _autoLock.Name = "AutoLockEnabled"; _minutes.Name = "AutoLockMinutes";
        Content = WindowChrome.Wrap(this, BuildLayout(), mainWindow: true);
        AddHandler(PointerPressedEvent, (_, _) => _lastInput = DateTimeOffset.UtcNow, RoutingStrategies.Tunnel, true);
        AddHandler(KeyDownEvent, (_, _) => _lastInput = DateTimeOffset.UtcNow, RoutingStrategies.Tunnel, true);
        _walletButton.Click += async (_, _) => await Run(_walletName.Length == 0 ? OpenWalletOnLaunch : ManageWallet);
        _lockButton.Click += async (_, _) => await Run(LockWallet);
        _sendButton.Click += async (_, _) => await Run(Send);
        _receiveButton.Click += async (_, _) => await Run(Receive);
        _miningButton.Click += async (_, _) => await HandleMiningClickAsync();
        _miningThreads.Value = _settings.GetMiningThreadCount();
        UpdateThreadCount();
        _miningThreads.ValueChanged += (_, _) =>
        {
            int selected = Math.Clamp((int)Math.Round(_miningThreads.Value), 1, Math.Max(1, Environment.ProcessorCount));
            if (_miningThreads.Value != selected) { _miningThreads.Value = selected; return; }
            _settings.MiningThreads = selected;
            UpdateThreadCount();
            Save();
        };
        _custom.IsChecked = _settings.MineToCustomAddress;
        _miningAddress.Text = _settings.CustomMiningAddress;
        _custom.IsCheckedChanged += (_, _) => { _settings.MineToCustomAddress = _custom.IsChecked == true; Save(); };
        _miningAddress.LostFocus += (_, _) => { _settings.CustomMiningAddress = _miningAddress.Text?.Trim() ?? ""; Save(); };
        _autoLock.IsChecked = _settings.AutoLockEnabled; _minutes.Value = _settings.AutoLockMinutes;
        _minutes.IsEnabled = _settings.AutoLockEnabled;
        _autoLock.IsCheckedChanged += (_, _) => { _settings.AutoLockEnabled = _autoLock.IsChecked == true; _minutes.IsEnabled = _settings.AutoLockEnabled; _lastInput = DateTimeOffset.UtcNow; Save(); };
        _minutes.ValueChanged += (_, _) => { _settings.AutoLockMinutes = (int)(_minutes.Value ?? 10); Save(); };
        _peers.SelectedIndex = _settings.PublicNodePreference switch { "borogove" => 1, "mome" => 2, "rath" => 3, _ => 0 };
        _peers.SelectionChanged += async (_, _) =>
        {
            _settings.PublicNodePreference = _peers.SelectedIndex switch { 1 => "borogove", 2 => "mome", 3 => "rath", _ => "auto" }; Save();
            if (_ready && !_preview) await Run(ApplyPeerPreference);
        };
        _traySetting.IsChecked = _settings.MinimizeToTrayOnClose;
        _traySetting.IsCheckedChanged += (_, _) => { _settings.MinimizeToTrayOnClose = _traySetting.IsChecked == true; Save(); };
        _timer.Tick += async (_, _) => await Poll();
        Opened += async (_, _) =>
        {
            // Restoring from the tray raises Opened again. Startup belongs to the first show.
            if (_startupStarted) return;
            _startupStarted = true;
            try { await LinuxGuiUpdateInstaller.AcknowledgeStartupAsync(Program.StartupArguments); }
            catch
            {
                _closing = true;
                Close();
                return;
            }
            if (_preview) { Preview(); return; }
            StartUpdateChecks();
            await Run(Initialize);
        };
        Closing += OnClosing;
        Closed += (_, _) => { StopUpdateChecks(); _diagnostics?.Dispose(); };
        UpdateControls();
    }

    private Control BuildLayout()
    {
        var root = new DockPanel();
        var headerText = new StackPanel { Spacing = 4 };
        headerText.Children.Add(new TextBlock { Text = "Slithy Tove", FontFamily = new FontFamily("Georgia, DejaVu Serif, Liberation Serif"), FontWeight = FontWeight.Bold, FontSize = 34, Foreground = Dialogs.Paper });
        headerText.Children.Add(new TextBlock { Text = "A simple wallet that helps support children's literacy", Foreground = Dialogs.Gold, FontSize = 14, TextWrapping = TextWrapping.Wrap });
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(22, 16), ColumnSpacing = 16 };
        var logo = new Image { Name = "SlithyHeaderLogo", Source = Branding.HeaderMark, Width = 48, Height = 48, VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(logo);
        Grid.SetColumn(headerText, 1); header.Children.Add(headerText);
        var tag = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        tag.Children.Add(new Border { Background = Dialogs.Gold, CornerRadius = new CornerRadius(5), Padding = new Thickness(10, 5), Child = Dialogs.Text("SLITHY TEST-NETWORK", 12) });
        tag.Children.Add(new TextBlock { Text = "Linux GUI " + Branding.Release, Foreground = Dialogs.Paper, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right });
        tag.Children.Add(GetUpdateBadge());
        Grid.SetColumn(tag, 2); header.Children.Add(tag);
        var headerBorder = new Border { Background = Dialogs.Ink, Child = header }; DockPanel.SetDock(headerBorder, Dock.Top); root.Children.Add(headerBorder);
        var updateNotice = GetUpdateNotice(); DockPanel.SetDock(updateNotice, Dock.Top); root.Children.Add(updateNotice);
        var footer = new StackPanel { Spacing = 6, Margin = new Thickness(18, 6, 18, 10) };
        footer.Children.Add(_sync); footer.Children.Add(Dialogs.Text("Beta test coins have no monetary value. Balances will reset before the live launch.", 12));
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        _mainTabs.Items.Add(Tab("Home", Home()));
        _mainTabs.Items.Add(Tab("Activity", BuildActivity()));
        _settingsTab = Tab("Settings", Settings());
        _mainTabs.Items.Add(_settingsTab);
        root.Children.Add(_mainTabs); return root;
    }

    private static TabItem Tab(string title, Control content) => new()
    {
        Header = title, Content = content, FontSize = 15, Padding = new Thickness(16, 9)
    };

    private Control Home()
    {
        var page = new StackPanel { Spacing = 12, Margin = new Thickness(16) };
        page.Children.Add(new Border { Background = Dialogs.Paper, CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 12), Child = _status });
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,2*"), ColumnSpacing = 12 };
        var walletHeading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8 };
        var caption = Dialogs.Text("Your balance", 18); caption.VerticalAlignment = VerticalAlignment.Center;
        walletHeading.Children.Add(caption);
        _walletButton.Padding = new Thickness(10, 6); _lockButton.Padding = new Thickness(10, 6);
        Grid.SetColumn(_walletButton, 1); walletHeading.Children.Add(_walletButton);
        Grid.SetColumn(_lockButton, 2); walletHeading.Children.Add(_lockButton);
        var money = new StackPanel { Spacing = 14 };
        money.Children.Add(walletHeading); money.Children.Add(_walletStatus); money.Children.Add(_balance); money.Children.Add(_available);
        var payments = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12, Margin = new Thickness(0, 4, 0, 0) };
        _sendButton.HorizontalAlignment = HorizontalAlignment.Stretch; _receiveButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _sendButton.MinHeight = _receiveButton.MinHeight = 42;
        payments.Children.Add(_sendButton); Grid.SetColumn(_receiveButton, 1); payments.Children.Add(_receiveButton);
        money.Children.Add(payments);
        grid.Children.Add(Card(null, money));
        var mine = new StackPanel { Spacing = 10 };
        mine.Children.Add(_miningTitle); mine.Children.Add(_miningStatus); mine.Children.Add(_custom);
        mine.Children.Add(_miningAddress); mine.Children.Add(_miningButton);
        mine.Children.Add(_miningSettingsLink);
        var mineCard = Card(null, mine); Grid.SetColumn(mineCard, 1); grid.Children.Add(mineCard); page.Children.Add(grid);
        var fund = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16 };
        fund.Children.Add(Dialogs.Text("10% of each block subsidy supports children's literacy", 16));
        _treasury.FontSize = 12; _treasury.MaxWidth = 270; _treasury.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(_treasury, 1); fund.Children.Add(_treasury);
        page.Children.Add(new Border { Background = Brush.Parse("#e7eedf"), CornerRadius = new CornerRadius(12), Padding = new Thickness(18, 14), Child = fund });
        return new ScrollViewer { Content = page };
    }

    private Control Settings()
    {
        var page = new StackPanel { Spacing = 14, Margin = new Thickness(24) };
        var refresh = Dialogs.Button("Refresh"); refresh.Click += async (_, _) => await Run(Refresh);
        page.Children.Add(Card("Network", _network, _peers, Dialogs.Text("Your local node connects to the official peers. Changing this preference does not move mining to a server."), refresh, _storage));
        page.Children.Add(MiningSettings());
        page.Children.Add(Card("Wallet locking", _autoLock, Row(Dialogs.Text("Minutes without activity"), _minutes),
            Dialogs.Text("Locking protects spending access. It does not stop mining or remove your coins. The last balance stays visible. Sending asks for your password."), _traySetting));
        var backup = Dialogs.Button("Back up open wallet"); backup.Click += async (_, _) => await Run(Backup);
        var restore = Dialogs.Button("Restore wallet"); restore.Click += async (_, _) => await Run(RestoreChoice);
        page.Children.Add(Card("Wallet files", Row(backup, restore), Dialogs.Text("This GUI uses its own wallet folder. Existing Linux terminal wallets are not modified. Restore a backup or recovery words here.")));
        page.Children.Add(BuildUpdatesCard());
        page.Children.Add(Card("Technical log", _log));
        return new ScrollViewer { Content = page };
    }

    private Control MiningSettings()
    {
        _threadCount.Name = "MiningThreadCount";
        AutomationProperties.SetName(_miningThreads, "CPU mining threads");
        AutomationProperties.SetHelpText(_miningThreads, "Choose how many CPU threads to use. Changes apply the next time you start mining.");
        var limits = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        limits.Children.Add(Dialogs.Text("1 thread", 12));
        var maximum = Dialogs.Text($"{Environment.ProcessorCount} available", 12);
        maximum.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(maximum, 1); limits.Children.Add(maximum);
        _miningSettingsCard = Card("CPU mining", _threadCount, _miningThreads, limits,
            Dialogs.Text("Changes apply the next time you start mining. More threads use more power and can make your computer warmer.", 13));
        _miningSettingsCard.Name = "MiningSettings";
        return _miningSettingsCard;
    }

    private void UpdateThreadCount()
    {
        int threads = _settings.GetMiningThreadCount();
        _threadCount.Text = $"{threads} CPU thread{(threads == 1 ? "" : "s")} selected";
        _miningSettingsText.Text = _mining?.Active == true || _miningStatusUnknown
            ? $"Next start: {threads} thread{(threads == 1 ? "" : "s")}. Settings"
            : $"{threads} CPU thread{(threads == 1 ? "" : "s")}. Change in Settings";
    }

    private void ShowMiningSettings()
    {
        _mainTabs.SelectedItem = _settingsTab;
        Dispatcher.UIThread.Post(() =>
        {
            _miningSettingsCard.BringIntoView();
            _miningThreads.Focus(NavigationMethod.Tab);
            _miningThreads.BringIntoView();
        }, DispatcherPriority.Loaded);
    }

    private static Border Card(string? title, params Control[] children)
    {
        var panel = new StackPanel { Spacing = 10 };
        if (title != null) panel.Children.Add(Dialogs.Text(title, 18));
        foreach (var child in children) panel.Children.Add(child);
        return new Border { Background = Dialogs.Paper, CornerRadius = new CornerRadius(14), Padding = new Thickness(18), Child = panel };
    }

    private static WrapPanel Row(params Control[] controls)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var control in controls) { control.Margin = new Thickness(0, 0, 10, 6); control.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(control); }
        return row;
    }

    private void Save()
    {
        if (_preview || _settingsError != null) return;
        try { _store.Save(_settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _status.Text = "Your preferences changed for this session, but could not be saved. Check the disk space and folder permissions.";
            Log("Preferences could not be saved.");
        }
    }

    private async Task Initialize()
    {
        if (_settingsError != null) throw new InvalidOperationException(_settingsError);
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Slithy.BetaTerms") ?? throw new InvalidOperationException("The bundled beta terms are missing.");
        using var reader = new StreamReader(stream); string terms = await reader.ReadToEndAsync();
        string version = "1.0:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(terms)));
        if (_settings.TermsAcceptedVersion != version)
        {
            if (!await Dialogs.Confirm(this, "Slithy beta terms", terms, "I agree")) { _closing = true; Close(); return; }
            _settings.TermsAcceptedVersion = version; Save();
        }
        await _host.StartAsync(); _endpoint = await _host.GetEndpointAsync();
        string genesis = await _node.GetBlockHashAsync(_endpoint, 0);
        if (!_regtest && genesis != NodeRpcClient.BetaGenesisHash) throw new InvalidOperationException("This node uses a different beta chain. Mining and wallet actions are disabled.");
        _ready = true; SetUpTray(); _timer.Start(); await Refresh();
        Log("Local node started. Wallets stay on this computer.");
        await OpenWalletOnLaunch();
    }

    private async Task ApplyPeerPreference()
    {
        if (_endpoint == null || _settings.PublicNodePreference == "auto") return;
        string peer = _settings.PublicNodePreference + ".slithy.io:53424";
        await _node.CallAsync(_endpoint, "", "addnode", new object[] { peer, "onetry" });
        Log("Requested a connection to the selected peer. Existing peer connections remain available.");
    }

    private async Task Poll()
    {
        if (_busy || _polling || !_ready || _closing) return;
        _polling = true;
        using var cancellation = new CancellationTokenSource();
        _pollCancellation = cancellation;
        long generation = _refreshGeneration;
        try
        {
            if (_settings.AutoLockEnabled && !_locked && DateTimeOffset.UtcNow - _lastInput >= TimeSpan.FromMinutes(_settings.AutoLockMinutes))
                await LockWalletCore(cancellation.Token).WaitAsync(cancellation.Token);
            await RefreshCore(cancellation.Token, generation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested || generation != _refreshGeneration) { }
        catch (Exception ex)
        {
            _status.Text = "The status check did not finish. Slithy will try again.";
            Log("Status refresh failed (" + ex.GetType().Name + ").");
        }
        finally
        {
            if (ReferenceEquals(_pollCancellation, cancellation)) _pollCancellation = null;
            _polling = false;
            UpdateControls();
        }
    }

    private void CancelBackgroundPoll()
    {
        // Cancel the reads, not the miner. The Stop RPC is sent after the poll leaves the UI.
        _refreshGeneration++;
        _pollCancellation?.Cancel();
    }

    private void CheckRefresh(CancellationToken cancellationToken, long generation)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (generation != _refreshGeneration || _closing) throw new OperationCanceledException(cancellationToken);
    }

    private Task Refresh() => RefreshCore(CancellationToken.None, _refreshGeneration);

    private async Task RefreshCore(CancellationToken cancellationToken, long generation)
    {
        if (_endpoint == null) return;
        CheckRefresh(cancellationToken, generation);
        try
        {
            var chain = await _node.GetInfoAsync(_endpoint, cancellationToken: cancellationToken).WaitAsync(cancellationToken);
            var peers = await _node.GetPeerNetworkStatusAsync(_endpoint, chain.Height, cancellationToken).WaitAsync(cancellationToken);
            CheckRefresh(cancellationToken, generation);
            _chain = chain;
            _status.Text = _chain.Synchronized && peers.Connections > 0 ? "Everything is ready" : $"Catching up: {_chain.Height:N0} / {_chain.Headers:N0} blocks, {peers.Connections} peers";
            _sync.IsIndeterminate = _chain.Headers == 0 && !_chain.Synchronized;
            _sync.Value = _chain.Headers > 0 ? Math.Min(100, 100d * _chain.Height / _chain.Headers) : 0;
            _network.Text = $"Local node: {_chain.Height:N0} blocks, {_chain.Headers:N0} headers\nConnected peers: {peers.Connections}\nNetwork: {_chain.NetworkType}\nData folder: {_store.DataDirectory}";
        }
        catch (Exception ex)
        {
            CheckRefresh(cancellationToken, generation);
            _chain = null;
            _sync.IsIndeterminate = true;
            _sync.Value = 0;
            _status.Text = "Network status is unavailable. Slithy will check again.";
            _network.Text = "Waiting for fresh network details.";
            Log("Network status refresh failed (" + ex.GetType().Name + ").");
        }

        // A balance, history or disk check cannot change what the miner just reported.
        try
        {
            var mining = await _node.GetMiningInfoAsync(_endpoint, cancellationToken).WaitAsync(cancellationToken);
            CheckRefresh(cancellationToken, generation);
            _mining = mining;
            _miningStatusUnknown = false;
            _miningTitle.Text = _mining.Active ? "Mining is on" : "Earn Slithy";
            _miningStatus.Text = MiningStatusText(_mining, _chain?.Height);
            ToolTip.SetTip(_miningStatus, _mining.Active ? "Mining address: " + _mining.Address : null);
        }
        catch (Exception ex)
        {
            CheckRefresh(cancellationToken, generation);
            _miningStatusUnknown = true;
            _miningTitle.Text = "Checking miner";
            _miningStatus.Text = "The local node did not answer. Mining state is unknown. You can still request a stop.";
            _status.Text = "Mining status is unavailable. Slithy will check again.";
            Log("Mining status check failed (" + ex.GetType().Name + ").");
        }
        UpdateControls();

        try
        {
            var storage = await Task.Run(_readStorage, cancellationToken).WaitAsync(cancellationToken);
            CheckRefresh(cancellationToken, generation);
            _storage.Text = storage.FreeBytes < 0
                ? "Free disk space could not be read."
                : $"Node files: {storage.NodeBytes / 1073741824d:N2} GiB. Free disk space: {storage.FreeBytes / 1073741824d:N1} GiB.";
            if (storage.FreeBytes is >= 0 and < 1073741824) _status.Text = "Low disk space: less than 1 GiB remains on the node drive.";
        }
        catch (Exception ex)
        {
            CheckRefresh(cancellationToken, generation);
            _storage.Text = "Disk usage could not be read. Slithy will try again.";
            Log("Disk usage check failed (" + ex.GetType().Name + ").");
        }
        if (!string.IsNullOrEmpty(_walletName))
        {
            try
            {
                var snapshot = await _wallet.GetSnapshotAsync(_endpoint, cancellationToken).WaitAsync(cancellationToken);
                CheckRefresh(cancellationToken, generation);
                string available = $"{snapshot.UnlockedBalance:N8} spendable\n{snapshot.ImmatureBalance:N8} immature mining rewards\n{snapshot.PendingBalance:N8} pending incoming";
                // Every amount comes from the same getbalances reply.
                _snapshot = snapshot;
                _balance.Text = $"{snapshot.Balance:N8} SLTHY";
                _available.Text = available;
                // The open UI session is separate from Core's short signing-key unlock.
                // Sending asks for the password again even when the wallet view stays open.
                _walletStatus.Text = _walletName + (_locked ? " | Locked for spending" : " | Open");
            }
            catch (Exception ex)
            {
                CheckRefresh(cancellationToken, generation);
                _walletStatus.Text = _walletName + " | Balance update delayed";
                if (_snapshot == null) _balance.Text = "Balance unavailable";
                _available.Text = _snapshot == null
                    ? "Waiting for the wallet balance."
                    : "Last balance shown. Waiting for a fresh balance.";
                Log("Wallet balance refresh failed (" + ex.GetType().Name + ").");
            }
            await RefreshHistoryIfNeeded(cancellationToken);
            CheckRefresh(cancellationToken, generation);
        }
        if (DateTimeOffset.UtcNow - _lastTreasury > TimeSpan.FromMinutes(1)) await RefreshTreasury(cancellationToken);
        CheckRefresh(cancellationToken, generation);
        UpdateControls();
    }

    internal static string MiningStatusText(MiningInfo mining, long? chainHeight) => mining.Active
        ? $"{mining.Speed:N0} H/s with {mining.Threads} workers.\n" +
            (chainHeight.HasValue ? $"Next block: {chainHeight + 1:N0}.\n" : "") +
            $"Blocks found this session: {mining.Blocks:N0}.\nTo: {AddressPreview(mining.Address)}"
        : "Mining is stopped. Start when you are ready.";

    private static string AddressPreview(string address) => address.Length > 30 ? address[..14] + "..." + address[^8..] : address;

    private async Task RefreshTreasury(CancellationToken cancellationToken = default)
    {
        _lastTreasury = DateTimeOffset.UtcNow;
        try
        {
            var result = await new TreasuryStatusService().TryGetPublicStatusAsync("https://slithy.io/data/treasury-beta-20260909.json").WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result == null) throw new InvalidDataException();
            _treasury.Text = $"Literacy fund: {result.CurrentBalance:N8} SLTHY\nUpdated {result.UpdatedAt.ToLocalTime():g}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { _treasury.Text = "Literacy fund: fresh data is unavailable."; }
    }

    private async Task ManageWallet()
    {
        var choices = Dialogs.CreateWindow("Manage wallet", 420);
        var body = new StackPanel { Spacing = 10, Margin = new Thickness(20) }; choices.Content = WindowChrome.Wrap(choices, body);
        foreach (string label in new[] { "Open wallet", "Create wallet", "Back up open wallet", "Check recovery words", "Combine mining rewards", "Restore recovery words", "Restore wallet file" })
        {
            var button = Dialogs.Button(label); button.HorizontalAlignment = HorizontalAlignment.Stretch;
            if (label is "Back up open wallet" or "Check recovery words" or "Combine mining rewards") button.IsEnabled = _walletName.Length > 0;
            button.Click += (_, _) => choices.Close(label); body.Children.Add(button);
        }
        string? action = await choices.ShowDialog<string?>(this);
        if (action != null) await RunWalletAction(action);
    }

    private Task OpenWalletOnLaunch() => RunWalletAction("Open wallet");

    private async Task RunWalletAction(string action)
    {
        if (action == "Back up open wallet") { await Backup(); return; }
        if (action == "Check recovery words") { await CheckWalletRecoveryWords(); return; }
        if (action == "Combine mining rewards") { await Consolidate(); return; }
        if (action == "Restore wallet file") { await RestoreFile(); return; }
        var names = action == "Open wallet" ? await _wallet.ListWalletNamesAsync(_endpoint!) : null;
        var input = await Dialogs.Wallet(this, action, names, _settings.LastWalletName); if (input == null) return;
        if (input.RequestedAction is { } next) { await RunWalletAction(next); return; }
        await CloseCurrentWallet();
        if (action == "Create wallet")
        {
            string words = await _wallet.CreateSeedWalletAsync(_endpoint!, input.Name, input.Password, TimeSpan.FromMinutes(2));
            await Dialogs.RecoveryWords(this, words);
        }
        else if (action == "Restore recovery words")
        {
            _status.Text = "Restoring your wallet from the start of the chain. A deeper address scan can take several minutes.";
            await _wallet.RestoreWalletAsync(_endpoint!, input.Name, input.Password, input.Words, 0, input.RecoveryRangeEnd);
            await _wallet.LockWalletOnlyAsync(_endpoint!);
        }
        else await _wallet.OpenWalletAsync(_endpoint!, input.Name, input.Password, TimeSpan.FromMinutes(2));
        _walletName = input.Name; _locked = false; _lastInput = DateTimeOffset.UtcNow;
        _settings.LastWalletName = input.Name; Save(); LoadPending();
        await Refresh(); Log("Wallet opened.");
    }

    private async Task CloseCurrentWallet()
    {
        _pending = null; _pendingAddress = ""; _pendingAmount = 0; _paymentBlocked = true;
        if (_walletName.Length == 0) return;
        await _wallet.LockWalletOnlyAsync(_endpoint!); await _wallet.CloseWalletAsync(_endpoint!);
        _walletName = ""; _locked = true; _snapshot = null;
        _balance.Text = "Open a wallet"; _walletStatus.Text = "No wallet open";
        _available.Text = "Open a wallet to view its balance."; ResetHistory();
    }

    private Task LockWallet() => LockWalletCore(CancellationToken.None);

    private async Task LockWalletCore(CancellationToken cancellationToken)
    {
        if (_walletName.Length == 0) return;
        await _wallet.LockWalletOnlyAsync(_endpoint!, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _locked = true;
        _walletStatus.Text = _walletName + " | Locked for spending";
        Log("Wallet locked. Mining and balance updates continue."); UpdateControls();
    }

    private async Task Send()
    {
        if (_walletName.Length == 0) return;
        if (_paymentBlocked) throw new InvalidOperationException("A pending payment record needs review. Check its transaction on the chain before sending again.");
        if (_pending != null)
        {
            if (await _wallet.HasTransactionAsync(_endpoint!, _pending.TransactionHash)) { DeletePending(); await Refresh(); return; }
            if (!await Dialogs.Confirm(this, "Unconfirmed send result", $"A previous send has an unknown result. Retry the same signed transaction?\n\n{_pending.TransactionHash}\n{_pendingAmount:N8} SLTHY to {_pendingAddress}", "Retry same transaction")) return;
        }
        else
        {
            var input = await Dialogs.Send(this, () => _wallet.GetSpendableOutputsAsync(_endpoint!)); if (input == null) return;
            var valid = await _wallet.ValidateAddressAsync(_endpoint!, input.Address); if (!valid.Valid) throw new InvalidOperationException("That address is not valid on this chain.");
            string? password = await Dialogs.Password(this); if (password == null) return;
            await _wallet.UnlockWalletAsync(_endpoint!, password, TimeSpan.FromMinutes(2));
            PreparedTransfer prepared;
            try
            {
                ulong amount = checked((ulong)(input.Amount * 100_000_000m));
                prepared = input.SelectedInputs is { Count: > 0 }
                    ? await _wallet.PrepareTransferAsync(_endpoint!, input.Address, amount, input.SelectedInputs)
                    : await _wallet.PrepareTransferAsync(_endpoint!, input.Address, amount);
            }
            finally { await _wallet.LockWalletOnlyAsync(_endpoint!); _locked = true; }
            ValidatePreparedPayment(prepared);
            if (!await Dialogs.Confirm(this, "Confirm payment", PaymentConfirmation(prepared), "Send payment")) return;
            RememberApprovedPayment(prepared);
        }
        await RelayPendingPayment();
    }

    private string PendingPath => Path.Combine(_store.ConfigDirectory, "pending-" + (_regtest ? "regtest-" : "beta-") + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_walletName))) + ".json");
    private sealed record PendingPayment(PreparedTransfer Transfer, string Address, decimal Amount);
    private void SavePending(PendingPayment payment)
    {
        string path = PendingPath, temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        LinuxSettingsStore.EnsurePrivateDirectory(_store.ConfigDirectory);
        LinuxSettingsStore.RejectLink(path);
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (OperatingSystem.IsLinux()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        try
        {
            using (var stream = new FileStream(temporary, options))
            {
                JsonSerializer.Serialize(stream, payment);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private void LoadPending()
    {
        _pending = null; _paymentBlocked = true;
        LinuxSettingsStore.RejectLink(PendingPath);
        if (!File.Exists(PendingPath)) { _paymentBlocked = false; return; }
        if (new FileInfo(PendingPath).Length > 2_000_000) throw new InvalidDataException("The pending payment record is too large. Check the transaction before sending again.");
        var pending = ReadPendingPayment(File.ReadAllText(PendingPath));
        _pending = pending.Transfer; _pendingAddress = pending.Address; _pendingAmount = pending.Amount; _paymentBlocked = false;
    }
    private static PendingPayment ReadPendingPayment(string json)
    {
        var pending = JsonSerializer.Deserialize<PendingPayment>(json) ?? throw new InvalidDataException("The pending payment record could not be read. Do not resend before checking the chain.");
        if (pending.Transfer == null || pending.Transfer.TransactionHash?.Length != 64 ||
            !pending.Transfer.TransactionHash.All(Uri.IsHexDigit) ||
            string.IsNullOrWhiteSpace(pending.Transfer.TransactionMetadata) ||
            pending.Transfer.TransactionMetadata.Length % 2 != 0 || !pending.Transfer.TransactionMetadata.All(Uri.IsHexDigit) ||
            string.IsNullOrWhiteSpace(pending.Address) || pending.Address.Length > 256 ||
            pending.Amount <= 0 || pending.Amount > 21_000_000_000m || decimal.Round(pending.Amount, 8) != pending.Amount)
            throw new InvalidDataException("The pending payment record is damaged. Check its transaction on the chain before sending again.");
        if ((pending.Transfer.AmountAtomic != 0 && pending.Transfer.AmountAtomic / 100_000_000m != pending.Amount) ||
            (!string.IsNullOrEmpty(pending.Transfer.RecipientAddress) && pending.Transfer.RecipientAddress != pending.Address) ||
            pending.Transfer.InputCount < 0)
            throw new InvalidDataException("The pending payment details do not agree. Check the transaction before sending again.");
        return pending;
    }
    internal static bool PendingRecordIsValid(string json)
    {
        try { ReadPendingPayment(json); return true; }
        catch (Exception ex) when (ex is JsonException or InvalidDataException) { return false; }
    }
    private void DeletePending()
    {
        File.Delete(PendingPath);
        _pending = null; _pendingAddress = ""; _pendingAmount = 0; _paymentBlocked = false;
    }

    private async Task Receive()
    {
        if (_snapshot == null) return;
        var addresses = await _wallet.GetReceivingAddressesAsync(_endpoint!);
        await Dialogs.ReceivingAddresses(this, _snapshot.Address, addresses, async label =>
        {
            var added = await _wallet.CreateReceivingAddressAsync(_endpoint!, label);
            _snapshot = _snapshot with { Address = added.Address };
            return added;
        });
        await Refresh();
    }

    private async Task Backup()
    {
        if (_walletName.Length == 0) { await Dialogs.Message(this, "Open a wallet", "Open the wallet you want to back up first."); return; }
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Save encrypted wallet backup", SuggestedFileName = _walletName + "-backup.dat", ShowOverwritePrompt = true });
        string? path = file?.TryGetLocalPath(); if (path == null) return;
        if (File.Exists(path)) throw new IOException("Choose a new backup filename. Existing backups are not overwritten.");
        string temporary = Path.Combine(_store.ConfigDirectory, "wallet-backup-" + Guid.NewGuid().ToString("N") + ".dat");
        try
        {
            // Core writes inside the private config folder before the chosen copy is created.
            await _wallet.BackupWalletAsync(_endpoint!, temporary);
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (OperatingSystem.IsLinux()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using var output = new FileStream(path, options);
            await using var input = File.OpenRead(temporary); await input.CopyToAsync(output);
        }
        finally { File.Delete(temporary); }
        await Dialogs.Message(this, "Backup saved", "Your wallet backup was saved. Store it somewhere private, separate from this computer.");
    }

    private Task RestoreChoice() => ManageWallet();

    private async Task RestoreFile()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Choose a wallet backup", AllowMultiple = false });
        string? path = files.FirstOrDefault()?.TryGetLocalPath(); if (path == null) return;
        var input = await Dialogs.Wallet(this, "Restore wallet file"); if (input == null) return;
        await CloseCurrentWallet();
        await _node.CallAsync(_endpoint!, "", "restorewallet", new object[] { input.Name, Path.GetFullPath(path), false }, TimeSpan.FromMinutes(3));
        await _wallet.OpenWalletAsync(_endpoint!, input.Name, input.Password, TimeSpan.FromMinutes(2));
        _walletName = input.Name; _locked = false; _lastInput = DateTimeOffset.UtcNow;
        _settings.LastWalletName = input.Name; Save(); LoadPending(); await Refresh();
    }

    private async Task HandleMiningClickAsync()
    {
        if (_installingUpdate) { await StopMiningForUpdate(); return; }
        bool stopRequested = _miningStatusUnknown || _mining?.Active == true;
        await Run(() => ChangeMining(stopRequested), interruptPolling: stopRequested);
    }

    private Task ToggleMining() => ChangeMining(_miningStatusUnknown || _mining?.Active == true);

    private async Task ChangeMining(bool stopRequested)
    {
        // A stop click remains a stop request if the status reply was lost.
        if (stopRequested)
        {
            await RequestMiningStop();
            return;
        }
        var current = await _node.GetMiningInfoAsync(_endpoint!);
        if (current.Active) { await RequestMiningStop(); return; }
        var chain = await _node.GetInfoAsync(_endpoint!);
        if (!_regtest)
        {
            if (chain.NetworkType != "test" || !chain.Synchronized) throw new InvalidOperationException("Wait for your local node to catch up before mining.");
            var peers = await _node.GetPeerNetworkStatusAsync(_endpoint!, chain.Height);
            string? problem = NodeRpcClient.MiningPeerProblem(peers, chain.Height); if (problem != null) throw new InvalidOperationException(problem);
        }
        var disk = await Task.Run(_readStorage);
        if (disk.FreeBytes < 0) throw new IOException("Free disk space could not be read. Check the node drive before starting mining.");
        if (disk.FreeBytes < 1073741824) throw new IOException("Less than 1 GiB of disk space remains. Free some space before starting mining.");
        string address = _custom.IsChecked == true ? _miningAddress.Text?.Trim() ?? "" : _snapshot?.Address ?? "";
        if (string.IsNullOrEmpty(address) || !(await _wallet.ValidateAddressAsync(_endpoint!, address)).Valid) throw new InvalidOperationException("Open a wallet or enter a valid mining address first.");
        int threads = _settings.GetMiningThreadCount();
        try { await _node.StartMiningAsync(_endpoint!, address, threads); }
        catch (Exception ex) { ShowMiningCommandUnknown(stopping: false, ex); throw; }
        await Refresh();
    }

    private async Task RequestMiningStop()
    {
        try { await _node.StopMiningAsync(_endpoint!); }
        catch (Exception ex) { ShowMiningCommandUnknown(stopping: true, ex); throw; }
        ShowMiningStopped();
    }

    private void ShowMiningCommandUnknown(bool stopping, Exception error)
    {
        // A lost reply does not tell us whether the node carried out the command.
        _miningStatusUnknown = true;
        _miningTitle.Text = "Checking miner";
        _miningStatus.Text = stopping
            ? "The stop request was not confirmed. You can request a stop again."
            : "The start request was not confirmed. Slithy will check again. You can still request a stop.";
        ToolTip.SetTip(_miningStatus, null);
        Log((stopping ? "Mining stop was not confirmed (" : "Mining start was not confirmed (") + error.GetType().Name + ").");
        UpdateControls();
    }

    private void ShowMiningStopped()
    {
        _mining = new MiningInfo(false, 0, 0, "", _mining?.Algorithm ?? "yespower", _mining?.Blocks ?? 0);
        _miningStatusUnknown = false;
        _miningTitle.Text = "Earn Slithy";
        _miningStatus.Text = "Mining is stopped. Start when you are ready.";
        ToolTip.SetTip(_miningStatus, null);
        Log("CPU mining stopped.");
        UpdateControls();
    }

    private void UpdateControls()
    {
        bool active = _mining?.Active == true || _miningStatusUnknown;
        _walletButton.IsEnabled = _ready && !_busy && !_historyBusy; _lockButton.IsEnabled = _ready && !_busy && _walletName.Length > 0 && !_locked;
        _sendButton.IsEnabled = _ready && !_busy && !_historyBusy && !_paymentBlocked && _walletName.Length > 0; _receiveButton.IsEnabled = _snapshot != null && !_busy && !_historyBusy;
        bool canStopDuringUpdate = _installingUpdate && _updateAllowsStop && active && !_stoppingMiningForUpdate;
        _miningButton.IsEnabled = _ready && (!_busy || canStopDuringUpdate);
        _miningButton.Content = canStopDuringUpdate ? "Stop mining" : _busy ? "Please wait..." : active ? "Stop mining" : "Start mining";
        _walletButton.Content = _walletName.Length == 0 ? "Open wallet" : "Manage wallet";
        _miningThreads.IsEnabled = !_busy && !_installingUpdate;
        _miningSettingsLink.IsEnabled = _miningThreads.IsEnabled;
        UpdateThreadCount();
        _custom.IsEnabled = !active && !_busy; _miningAddress.IsEnabled = !active && !_busy;
        UpdateHistoryControls();
    }

    private async Task Run(Func<Task> action, bool interruptPolling = false)
    {
        if (_busy || _preview || _closing) return;
        _busy = true; UpdateControls();
        try
        {
            if (interruptPolling) CancelBackgroundPoll();
            while (_polling && !_closing) await Task.Delay(50);
            if (!_closing) await action();
        }
        catch (Exception ex)
        {
            if (_closing) return;
            string message = FriendlyError(ex);
            _status.Text = message; Log(message);
            await Dialogs.Message(this, "Slithy could not finish that", message);
        }
        finally { _busy = false; _lastInput = DateTimeOffset.UtcNow; UpdateControls(); }
    }

    private static string FriendlyError(Exception exception)
    {
        string message = exception.Message;
        if (message.Contains("passphrase", StringComparison.OrdinalIgnoreCase) || message.Contains("password", StringComparison.OrdinalIgnoreCase)) return "The password was not accepted, or the wallet is locked. Open the wallet and try again.";
        if (message.Contains("insufficient", StringComparison.OrdinalIgnoreCase)) return "There are not enough spendable coins for this payment and its fee. Mining rewards need 100 blocks before spending.";
        if (message.Contains("http", StringComparison.OrdinalIgnoreCase) || message.Contains("cookie", StringComparison.OrdinalIgnoreCase) || exception is System.Net.Http.HttpRequestException) return "The local node connection failed. Check the node log and free disk space. Credentials are not included here.";
        return message.Length > 600 ? "The operation failed. Check the local node log for details." : message;
    }

    private void Log(string message)
    {
        _diagnostics?.RecordMessage(message);
        string entry = DateTime.Now.ToString("HH:mm:ss") + "  " + message;
        _log.Text = string.Join("\n", (entry + "\n" + _log.Text).Split('\n').Take(40));
    }

    private void SetUpTray()
    {
        try
        {
            var menu = new NativeMenu(); var show = new NativeMenuItem("Open Slithy"); var quit = new NativeMenuItem("Quit and stop this node");
            show.Click += (_, _) => { Show(); Activate(); };
            quit.Click += async (_, _) => { Show(); await Quit(); };
            menu.Items.Add(show); menu.Items.Add(quit);
            _tray = new TrayIcon { Icon = Branding.WindowIcon, ToolTipText = "Slithy Tove", Menu = menu, IsVisible = true };
            _tray.Clicked += (_, _) => { Show(); Activate(); };
            TrayIcon.SetIcons(Application.Current!, new TrayIcons { _tray });
        }
        catch { _traySetting.IsEnabled = false; _traySetting.Content = "Notification-area support is unavailable on this desktop"; }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closing) return;
        if (_preview) { _closing = true; return; }
        e.Cancel = true;
        if (_busy) return;
        if (_settings.MinimizeToTrayOnClose && _tray != null)
        {
            if (!_trayConfirmed)
                _trayConfirmed = await Dialogs.Confirm(this, "Hide to the notification area?", "Can you see the Slithy icon in your desktop's notification area? Confirm before hiding this window. Some Linux desktops need an indicator extension to show it.", "The icon is visible, hide window");
            if (_trayConfirmed) { Hide(); return; }
            return;
        }
        await Quit();
    }

    private async Task Quit()
    {
        if (_busy || _closing) return;
        if ((_mining?.Active == true || _miningStatusUnknown) && !await Dialogs.Confirm(this, "Quit Slithy?", "Quitting stops mining and closes the local node started by this app. Your wallet files stay on disk.", "Stop and quit")) return;
        _closing = true; _timer.Stop(); _historyCancellation?.Cancel();
        CancelBackgroundPoll();
        while (_historyBusy) await Task.Delay(50);
        while (_polling) await Task.Delay(50);
        try { if (_walletName.Length > 0) await _wallet.LockWalletOnlyAsync(_endpoint!); }
        catch { Log("Wallet lock was not confirmed. Stopping the local node."); }
        try { await _host.StopOwnedAsync(); }
        catch
        {
            _closing = false; _timer.Start();
            await Dialogs.Message(this, "Shutdown not confirmed", "The local node did not confirm shutdown. Check it before leaving this computer.");
            return;
        }
        _tray?.Dispose(); _wallet.Dispose(); _node.Dispose(); await _host.DisposeAsync(); Close();
    }

    private void Preview()
    {
        _ready = true; _walletName = "Preview wallet";
        _snapshot = new WalletSnapshot("preview-address", 27_000_000_000, 1_800_000_000, 100, 310);
        _mining = new MiningInfo(true, 240, 4, "preview-address", "yespower", 310);
        _status.Text = "Everything is ready"; _balance.Text = "270.00000000 SLTHY";
        _walletStatus.Text = "Preview wallet | Locked for spending";
        _available.Text = "18.00000000 spendable\n252.00000000 immature mining rewards\n0.00000000 pending incoming";
        _miningTitle.Text = "Mining is on";
        _miningStatus.Text = "Preview: 240 H/s with 4 workers.\nNext block: 2,154.\nBlocks found this session: 2.\nTo: tslithy1qdwf9t7...k3d9h4d5";
        ToolTip.SetTip(_miningStatus, "Preview mining address: tslithy1qdwf9t7fmczdzajy9wc5hjykt2k3d9h4d5");
        _treasury.Text = "Literacy fund: preview data"; _network.Text = "Preview mode. No node or wallet is opened.";
        _storage.Text = "Node storage and free space appear here."; _sync.Value = 100;
        var history = PreviewHistory(0); RenderHistory(history.Entries);
        _activity.Text = "Preview transactions. Select one for its details."; _historyWallet = _walletName; _historyPageLabel.Text = "Page 1";
        UpdateControls();
    }
}
