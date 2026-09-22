//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Main Window Designer
//===============================================
#nullable disable

namespace Slithy_Tove;

// Visual Studio generated most of this file from the form designer.
// Prefer editing the layout in the designer when possible.
partial class Form1
{
    private System.ComponentModel.IContainer components = null!;
    private Panel headerPanel;
    private PictureBox brandPictureBox;
    private Label titleLabel;
    private Label subtitleLabel;
    private TabControl mainTabControl;
    private TabPage homeTabPage;
    private TabPage advancedTabPage;
    private Panel homeStatusCard;
    private Panel homeStatusDotPanel;
    private Label homeStatusLabel;
    private Label statusMessageLabel;
    private Label treasuryStatusLabel;
    private Panel balanceCard;
    private Label balanceCaptionLabel;
    private Label balanceValueLabel;
    private Label availableBalanceLabel;
    private Label walletNameLabel;
    private Label walletAddressPreviewLabel;
    private Button walletButton;
    private Button lockWalletButton;
    private Button sendButton;
    private Button receiveButton;
    private Panel miningCard;
    private Label miningTitleLabel;
    private Label miningDescriptionLabel;
    private ComboBox miningEffortComboBox;
    private CheckBox mineToAddressCheckBox;
    private Label miningAddressLabel;
    private TextBox miningAddressTextBox;
    private Button miningToggleButton;
    private Label miningStatusLineLabel;
    private Label homeNoteLabel;
    private Panel recentActivityCard;
    private Label recentActivityTitleLabel;
    private ListView recentActivityListView;
    private ColumnHeader activityTypeColumnHeader;
    private ColumnHeader activityAmountColumnHeader;
    private ColumnHeader activityDateColumnHeader;
    private Button _viewAllActivityButton;
    private Panel _latestActivityDot;
    private Label _latestActivitySummaryLabel;
    private Label _latestActivityDetailLabel;
    private TabPage _walletActivityTab;
    private Panel _walletActivityHeaderPanel;
    private Label _walletActivityHeadingLabel;
    private Label _walletActivityHelpLabel;
    private Panel _activityCard;
    private Label _activityEmptyLabel;
    private ListView _walletActivityListView;
    private ColumnHeader walletActivityTypeColumnHeader;
    private ColumnHeader walletActivityAmountColumnHeader;
    private ColumnHeader walletActivityDateColumnHeader;
    private TableLayoutPanel _homeLayout;
    private Panel _homeMissionCard;
    private Label _homeMissionTitle;
    private LinkLabel _homeWebsiteLinkLabel;
    private Label _headerVersionLabel;
    private Label _headerBetaLabel;
    private BookMiningAnimation _bookMiningAnimation;
    private Panel _operationProgressPanel;
    private Panel _operationProgressTrack;
    private Panel _operationProgressFill;
    private Label _operationProgressLabel;
    private TabControl advancedTabControl;
    private TabPage connectionTabPage;
    private TabPage updatesTabPage;
    private TabPage activityTabPage;
    private GroupBox connectionGroupBox;
    private Panel connectionDotPanel;
    private Label connectionStatusLabel;
    private ComboBox connectionModeComboBox;
    private ComboBox officialNodeComboBox;
    private TextBox nodeAddressTextBox;
    private Button refreshButton;
    private Button startNodeButton;
    private Button stopNodeButton;
    private TableLayoutPanel statusTableLayoutPanel;
    private Label heightCaptionLabel;
    private Label heightValueLabel;
    private Label difficultyCaptionLabel;
    private Label difficultyValueLabel;
    private Label networkCaptionLabel;
    private Label networkValueLabel;
    private Label syncCaptionLabel;
    private Label syncValueLabel;
    private Panel chainSyncPanel;
    private Label chainSyncLabel;
    private ProgressBar chainSyncProgressBar;
    private Label versionCaptionLabel;
    private Label versionValueLabel;
    private Label poolCaptionLabel;
    private Label poolValueLabel;
    private Label targetCaptionLabel;
    private Label targetValueLabel;
    private Label lastCheckedCaptionLabel;
    private Label lastCheckedValueLabel;
    private Label blockchainDataCaptionLabel;
    private Label blockchainDataValueLabel;
    private Label freeDiskCaptionLabel;
    private Label freeDiskValueLabel;
    private Panel updatesCard;
    private Label updatesTitleLabel;
    private Label installedVersionLabel;
    private Label updateStatusLabel;
    private Panel updateProgressTrackPanel;
    private Panel updateProgressFillPanel;
    private CheckBox automaticUpdatesCheckBox;
    private CheckBox minimizeToTrayOnCloseCheckBox;
    private Button checkUpdatesButton;
    private Label updateSafetyLabel;
    private LinkLabel supportEmailLinkLabel;
    private Label autoLockLabel;
    private NumericUpDown autoLockMinutesNumericUpDown;
    private Button backupWalletButton;
    private Button importWalletButton;
    private TextBox logTextBox;
    private Button openDataFolderButton;
    private Button exportLogButton;
    private Button reportBugButton;
    private Button clearLogButton;
    private NotifyIcon _trayIcon;
    private ContextMenuStrip _trayMenu;
    private ToolStripMenuItem openSlithyToveToolStripMenuItem;
    private ToolStripSeparator trayToolStripSeparator;
    private ToolStripMenuItem exitToolStripMenuItem;
    private System.Windows.Forms.Timer statusTimer;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        headerPanel = new Panel();
        _headerVersionLabel = new Label();
        _headerBetaLabel = new Label();
        brandPictureBox = new PictureBox();
        subtitleLabel = new Label();
        titleLabel = new Label();
        mainTabControl = new TabControl();
        homeTabPage = new TabPage();
        homeNoteLabel = new Label();
        _homeLayout = new TableLayoutPanel();
        homeStatusCard = new Panel();
        statusMessageLabel = new Label();
        treasuryStatusLabel = new Label();
        homeStatusLabel = new Label();
        homeStatusDotPanel = new Panel();
        balanceCard = new Panel();
        receiveButton = new Button();
        sendButton = new Button();
        walletButton = new Button();
        lockWalletButton = new Button();
        walletAddressPreviewLabel = new Label();
        walletNameLabel = new Label();
        availableBalanceLabel = new Label();
        balanceValueLabel = new Label();
        balanceCaptionLabel = new Label();
        miningCard = new Panel();
        _bookMiningAnimation = new BookMiningAnimation();
        miningStatusLineLabel = new Label();
        miningToggleButton = new Button();
        miningAddressTextBox = new TextBox();
        miningAddressLabel = new Label();
        mineToAddressCheckBox = new CheckBox();
        miningEffortComboBox = new ComboBox();
        miningDescriptionLabel = new Label();
        miningTitleLabel = new Label();
        _homeMissionCard = new Panel();
        _homeWebsiteLinkLabel = new LinkLabel();
        _homeMissionTitle = new Label();
        _walletActivityTab = new TabPage();
        _activityCard = new Panel();
        _activityEmptyLabel = new Label();
        _walletActivityListView = new ListView();
        walletActivityTypeColumnHeader = new ColumnHeader();
        walletActivityAmountColumnHeader = new ColumnHeader();
        walletActivityDateColumnHeader = new ColumnHeader();
        _walletActivityHeaderPanel = new Panel();
        _walletActivityHelpLabel = new Label();
        _walletActivityHeadingLabel = new Label();
        advancedTabPage = new TabPage();
        advancedTabControl = new TabControl();
        connectionTabPage = new TabPage();
        statusTableLayoutPanel = new TableLayoutPanel();
        heightCaptionLabel = new Label();
        heightValueLabel = new Label();
        difficultyCaptionLabel = new Label();
        difficultyValueLabel = new Label();
        networkCaptionLabel = new Label();
        networkValueLabel = new Label();
        syncCaptionLabel = new Label();
        syncValueLabel = new Label();
        chainSyncPanel = new Panel();
        chainSyncLabel = new Label();
        chainSyncProgressBar = new ProgressBar();
        versionCaptionLabel = new Label();
        versionValueLabel = new Label();
        poolCaptionLabel = new Label();
        poolValueLabel = new Label();
        targetCaptionLabel = new Label();
        targetValueLabel = new Label();
        lastCheckedCaptionLabel = new Label();
        lastCheckedValueLabel = new Label();
        blockchainDataCaptionLabel = new Label();
        blockchainDataValueLabel = new Label();
        freeDiskCaptionLabel = new Label();
        freeDiskValueLabel = new Label();
        connectionGroupBox = new GroupBox();
        stopNodeButton = new Button();
        startNodeButton = new Button();
        refreshButton = new Button();
        nodeAddressTextBox = new TextBox();
        officialNodeComboBox = new ComboBox();
        connectionModeComboBox = new ComboBox();
        connectionStatusLabel = new Label();
        connectionDotPanel = new Panel();
        updatesTabPage = new TabPage();
        updatesCard = new Panel();
        updateSafetyLabel = new Label();
        supportEmailLinkLabel = new LinkLabel();
        autoLockMinutesNumericUpDown = new NumericUpDown();
        autoLockLabel = new Label();
        importWalletButton = new Button();
        backupWalletButton = new Button();
        checkUpdatesButton = new Button();
        automaticUpdatesCheckBox = new CheckBox();
        minimizeToTrayOnCloseCheckBox = new CheckBox();
        updateProgressTrackPanel = new Panel();
        updateProgressFillPanel = new Panel();
        updateStatusLabel = new Label();
        installedVersionLabel = new Label();
        updatesTitleLabel = new Label();
        activityTabPage = new TabPage();
        clearLogButton = new Button();
        reportBugButton = new Button();
        exportLogButton = new Button();
        openDataFolderButton = new Button();
        logTextBox = new TextBox();
        recentActivityCard = new Panel();
        _latestActivityDetailLabel = new Label();
        _latestActivitySummaryLabel = new Label();
        _latestActivityDot = new Panel();
        _viewAllActivityButton = new Button();
        recentActivityListView = new ListView();
        activityTypeColumnHeader = new ColumnHeader();
        activityAmountColumnHeader = new ColumnHeader();
        activityDateColumnHeader = new ColumnHeader();
        recentActivityTitleLabel = new Label();
        _operationProgressPanel = new Panel();
        _operationProgressLabel = new Label();
        _operationProgressTrack = new Panel();
        _operationProgressFill = new Panel();
        _trayIcon = new NotifyIcon(components);
        _trayMenu = new ContextMenuStrip(components);
        openSlithyToveToolStripMenuItem = new ToolStripMenuItem();
        trayToolStripSeparator = new ToolStripSeparator();
        exitToolStripMenuItem = new ToolStripMenuItem();
        statusTimer = new System.Windows.Forms.Timer(components);
        headerPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)brandPictureBox).BeginInit();
        mainTabControl.SuspendLayout();
        homeTabPage.SuspendLayout();
        _homeLayout.SuspendLayout();
        homeStatusCard.SuspendLayout();
        balanceCard.SuspendLayout();
        miningCard.SuspendLayout();
        _homeMissionCard.SuspendLayout();
        _walletActivityTab.SuspendLayout();
        _activityCard.SuspendLayout();
        _walletActivityHeaderPanel.SuspendLayout();
        advancedTabPage.SuspendLayout();
        advancedTabControl.SuspendLayout();
        connectionTabPage.SuspendLayout();
        statusTableLayoutPanel.SuspendLayout();
        connectionGroupBox.SuspendLayout();
        updatesTabPage.SuspendLayout();
        updatesCard.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)autoLockMinutesNumericUpDown).BeginInit();
        updateProgressTrackPanel.SuspendLayout();
        activityTabPage.SuspendLayout();
        recentActivityCard.SuspendLayout();
        _operationProgressPanel.SuspendLayout();
        _operationProgressTrack.SuspendLayout();
        _trayMenu.SuspendLayout();
        SuspendLayout();
        // 
        // headerPanel
        // 
        headerPanel.BackColor = Color.FromArgb(48, 68, 56);
        headerPanel.Controls.Add(_headerVersionLabel);
        headerPanel.Controls.Add(_headerBetaLabel);
        headerPanel.Controls.Add(brandPictureBox);
        headerPanel.Controls.Add(subtitleLabel);
        headerPanel.Controls.Add(titleLabel);
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Location = new Point(0, 0);
        headerPanel.Margin = new Padding(5);
        headerPanel.Name = "headerPanel";
        headerPanel.Padding = new Padding(49, 29, 49, 19);
        headerPanel.Size = new Size(1495, 173);
        headerPanel.TabIndex = 0;
        // 
        // _headerVersionLabel
        // 
        _headerVersionLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _headerVersionLabel.AutoSize = true;
        _headerVersionLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _headerVersionLabel.ForeColor = Color.FromArgb(225, 226, 207);
        _headerVersionLabel.Location = new Point(1374, 118);
        _headerVersionLabel.Name = "_headerVersionLabel";
        _headerVersionLabel.Size = new Size(83, 32);
        _headerVersionLabel.TabIndex = 5;
        _headerVersionLabel.Text = "v0.0.0";
        // 
        // _headerBetaLabel
        // 
        _headerBetaLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _headerBetaLabel.AutoSize = false;
        _headerBetaLabel.BackColor = Color.FromArgb(236, 214, 166);
        _headerBetaLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _headerBetaLabel.ForeColor = Color.FromArgb(60, 48, 29);
        _headerBetaLabel.Location = new Point(1188, 78);
        _headerBetaLabel.Name = "_headerBetaLabel";
        _headerBetaLabel.Padding = new Padding(8, 3, 8, 3);
        _headerBetaLabel.Size = new Size(294, 36);
        _headerBetaLabel.TabIndex = 4;
        _headerBetaLabel.Text = "SLITHY TEST-NETWORK";
        _headerBetaLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // brandPictureBox
        // 
        brandPictureBox.Location = new Point(44, 34);
        brandPictureBox.Margin = new Padding(5);
        brandPictureBox.Name = "brandPictureBox";
        brandPictureBox.Size = new Size(91, 99);
        brandPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        brandPictureBox.TabIndex = 2;
        brandPictureBox.TabStop = false;
        // 
        // subtitleLabel
        // 
        subtitleLabel.AutoSize = true;
        subtitleLabel.Font = new Font("Segoe UI", 10F);
        subtitleLabel.ForeColor = Color.FromArgb(225, 226, 207);
        subtitleLabel.Location = new Point(156, 104);
        subtitleLabel.Margin = new Padding(5, 0, 5, 0);
        subtitleLabel.Name = "subtitleLabel";
        subtitleLabel.Size = new Size(631, 37);
        subtitleLabel.TabIndex = 1;
        subtitleLabel.Text = "A simple wallet that helps support children’s literacy";
        // 
        // titleLabel
        // 
        titleLabel.AutoSize = true;
        titleLabel.Font = new Font("Georgia", 23F, FontStyle.Bold);
        titleLabel.ForeColor = Color.FromArgb(255, 250, 238);
        titleLabel.Location = new Point(150, 26);
        titleLabel.Margin = new Padding(5, 0, 5, 0);
        titleLabel.Name = "titleLabel";
        titleLabel.Size = new Size(382, 71);
        titleLabel.TabIndex = 0;
        titleLabel.Text = "Slithy Tove";
        // 
        // mainTabControl
        // 
        mainTabControl.Controls.Add(homeTabPage);
        mainTabControl.Controls.Add(_walletActivityTab);
        mainTabControl.Controls.Add(advancedTabPage);
        mainTabControl.Dock = DockStyle.Fill;
        mainTabControl.Font = new Font("Segoe UI", 10F);
        mainTabControl.Location = new Point(0, 173);
        mainTabControl.Margin = new Padding(5);
        mainTabControl.Name = "mainTabControl";
        mainTabControl.Padding = new Point(20, 8);
        mainTabControl.SelectedIndex = 0;
        mainTabControl.Size = new Size(1495, 833);
        mainTabControl.TabIndex = 1;
        // 
        // homeTabPage
        // 
        homeTabPage.BackColor = Color.FromArgb(246, 241, 232);
        homeTabPage.Controls.Add(homeNoteLabel);
        homeTabPage.Controls.Add(_homeLayout);
        homeTabPage.Location = new Point(8, 59);
        homeTabPage.Margin = new Padding(5);
        homeTabPage.Name = "homeTabPage";
        homeTabPage.Padding = new Padding(42);
        homeTabPage.Size = new Size(1479, 766);
        homeTabPage.TabIndex = 0;
        homeTabPage.Text = "Home";
        // 
        // homeNoteLabel
        // 
        homeNoteLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        homeNoteLabel.Font = new Font("Segoe UI", 9.5F);
        homeNoteLabel.ForeColor = Color.FromArgb(111, 103, 88);
        homeNoteLabel.Location = new Point(49, 1064);
        homeNoteLabel.Margin = new Padding(5, 0, 5, 0);
        homeNoteLabel.Name = "homeNoteLabel";
        homeNoteLabel.Size = new Size(1384, 88);
        homeNoteLabel.TabIndex = 4;
        homeNoteLabel.Text = "By default, closing this window leaves Slithy running in the notification area. You can change that in Settings.";
        homeNoteLabel.TextAlign = ContentAlignment.MiddleCenter;
        homeNoteLabel.Visible = false;
        // 
        // _homeLayout
        // 
        _homeLayout.BackColor = Color.FromArgb(246, 241, 232);
        _homeLayout.ColumnCount = 2;
        _homeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
        _homeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
        _homeLayout.Controls.Add(homeStatusCard, 0, 0);
        _homeLayout.Controls.Add(balanceCard, 0, 1);
        _homeLayout.Controls.Add(miningCard, 1, 1);
        _homeLayout.Controls.Add(_homeMissionCard, 0, 2);
        _homeLayout.Dock = DockStyle.Fill;
        _homeLayout.Location = new Point(42, 42);
        _homeLayout.Name = "_homeLayout";
        _homeLayout.Padding = new Padding(18, 14, 18, 14);
        _homeLayout.RowCount = 3;
        _homeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
        _homeLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _homeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
        _homeLayout.Size = new Size(1395, 682);
        _homeLayout.TabIndex = 5;
        // 
        // homeStatusCard
        // 
        homeStatusCard.BackColor = Color.FromArgb(255, 253, 247);
        _homeLayout.SetColumnSpan(homeStatusCard, 2);
        homeStatusCard.Controls.Add(statusMessageLabel);
        homeStatusCard.Controls.Add(treasuryStatusLabel);
        homeStatusCard.Controls.Add(homeStatusLabel);
        homeStatusCard.Controls.Add(homeStatusDotPanel);
        homeStatusCard.Dock = DockStyle.Fill;
        homeStatusCard.Location = new Point(18, 14);
        homeStatusCard.Margin = new Padding(0, 0, 0, 12);
        homeStatusCard.Name = "homeStatusCard";
        homeStatusCard.Size = new Size(1359, 66);
        homeStatusCard.TabIndex = 0;
        // 
        // statusMessageLabel
        // 
        statusMessageLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        statusMessageLabel.Font = new Font("Segoe UI", 10F);
        statusMessageLabel.ForeColor = Color.FromArgb(91, 91, 78);
        statusMessageLabel.Location = new Point(88, 69);
        statusMessageLabel.Margin = new Padding(5, 0, 5, 0);
        statusMessageLabel.Name = "statusMessageLabel";
        statusMessageLabel.Size = new Size(1219, 42);
        statusMessageLabel.TabIndex = 2;
        statusMessageLabel.Text = "Connecting securely to the Slithy network.";
        // 
        // treasuryStatusLabel
        // 
        treasuryStatusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        treasuryStatusLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        treasuryStatusLabel.ForeColor = Color.FromArgb(132, 92, 51);
        treasuryStatusLabel.Location = new Point(886, 10);
        treasuryStatusLabel.Margin = new Padding(5, 0, 5, 0);
        treasuryStatusLabel.Name = "treasuryStatusLabel";
        treasuryStatusLabel.Size = new Size(431, 61);
        treasuryStatusLabel.TabIndex = 3;
        treasuryStatusLabel.Text = "Literacy fund: loading";
        treasuryStatusLabel.TextAlign = ContentAlignment.MiddleRight;
        // 
        // homeStatusLabel
        // 
        homeStatusLabel.AutoSize = true;
        homeStatusLabel.Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold);
        homeStatusLabel.ForeColor = Color.FromArgb(50, 58, 49);
        homeStatusLabel.Location = new Point(84, 6);
        homeStatusLabel.Margin = new Padding(5, 0, 5, 0);
        homeStatusLabel.Name = "homeStatusLabel";
        homeStatusLabel.Size = new Size(383, 51);
        homeStatusLabel.TabIndex = 1;
        homeStatusLabel.Text = "Checking connection";
        // 
        // homeStatusDotPanel
        // 
        homeStatusDotPanel.BackColor = Color.FromArgb(128, 121, 108);
        homeStatusDotPanel.Location = new Point(39, 22);
        homeStatusDotPanel.Margin = new Padding(5);
        homeStatusDotPanel.Name = "homeStatusDotPanel";
        homeStatusDotPanel.Size = new Size(26, 26);
        homeStatusDotPanel.TabIndex = 0;
        // 
        // balanceCard
        // 
        balanceCard.BackColor = Color.FromArgb(255, 253, 247);
        balanceCard.Controls.Add(receiveButton);
        balanceCard.Controls.Add(sendButton);
        balanceCard.Controls.Add(walletButton);
        balanceCard.Controls.Add(lockWalletButton);
        balanceCard.Controls.Add(walletAddressPreviewLabel);
        balanceCard.Controls.Add(walletNameLabel);
        balanceCard.Controls.Add(availableBalanceLabel);
        balanceCard.Controls.Add(balanceValueLabel);
        balanceCard.Controls.Add(balanceCaptionLabel);
        balanceCard.Dock = DockStyle.Fill;
        balanceCard.Location = new Point(18, 92);
        balanceCard.Margin = new Padding(0, 0, 7, 12);
        balanceCard.Name = "balanceCard";
        balanceCard.Size = new Size(835, 478);
        balanceCard.TabIndex = 1;
        // 
        // receiveButton
        // 
        receiveButton.BackColor = Color.FromArgb(239, 226, 198);
        receiveButton.FlatStyle = FlatStyle.Flat;
        receiveButton.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        receiveButton.ForeColor = Color.FromArgb(58, 68, 55);
        receiveButton.Location = new Point(437, 232);
        receiveButton.Margin = new Padding(5);
        receiveButton.Name = "receiveButton";
        receiveButton.Size = new Size(358, 88);
        receiveButton.TabIndex = 4;
        receiveButton.Text = "Receive";
        receiveButton.UseVisualStyleBackColor = false;
        receiveButton.Click += receiveButton_Click;
        // 
        // sendButton
        // 
        sendButton.BackColor = Color.FromArgb(71, 103, 80);
        sendButton.FlatStyle = FlatStyle.Flat;
        sendButton.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        sendButton.ForeColor = Color.White;
        sendButton.Location = new Point(46, 232);
        sendButton.Margin = new Padding(5);
        sendButton.Name = "sendButton";
        sendButton.Size = new Size(358, 88);
        sendButton.TabIndex = 3;
        sendButton.Text = "Send";
        sendButton.UseVisualStyleBackColor = false;
        sendButton.Click += sendButton_Click;
        // 
        // walletButton
        // 
        walletButton.BackColor = Color.FromArgb(239, 226, 198);
        walletButton.FlatStyle = FlatStyle.Flat;
        walletButton.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        walletButton.ForeColor = Color.FromArgb(58, 68, 55);
        walletButton.Location = new Point(536, 29);
        walletButton.Margin = new Padding(5);
        walletButton.Name = "walletButton";
        walletButton.Size = new Size(156, 67);
        walletButton.TabIndex = 5;
        walletButton.Text = "Open wallet";
        walletButton.UseVisualStyleBackColor = false;
        walletButton.Click += walletButton_Click;
        // 
        // lockWalletButton
        // 
        lockWalletButton.Enabled = false;
        lockWalletButton.Location = new Point(707, 29);
        lockWalletButton.Margin = new Padding(5);
        lockWalletButton.Name = "lockWalletButton";
        lockWalletButton.Size = new Size(114, 67);
        lockWalletButton.TabIndex = 6;
        lockWalletButton.Text = "Lock";
        lockWalletButton.UseVisualStyleBackColor = true;
        lockWalletButton.Click += lockWalletButton_Click;
        // 
        // walletAddressPreviewLabel
        // 
        walletAddressPreviewLabel.Font = new Font("Cascadia Mono", 8F);
        walletAddressPreviewLabel.ForeColor = Color.FromArgb(105, 105, 91);
        walletAddressPreviewLabel.Location = new Point(49, 283);
        walletAddressPreviewLabel.Margin = new Padding(5, 0, 5, 0);
        walletAddressPreviewLabel.Name = "walletAddressPreviewLabel";
        walletAddressPreviewLabel.Size = new Size(488, 59);
        walletAddressPreviewLabel.TabIndex = 4;
        walletAddressPreviewLabel.Text = "Your address will appear here.";
        walletAddressPreviewLabel.Visible = false;
        // 
        // walletNameLabel
        // 
        walletNameLabel.AutoSize = true;
        walletNameLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        walletNameLabel.ForeColor = Color.FromArgb(83, 104, 79);
        walletNameLabel.Location = new Point(49, 242);
        walletNameLabel.Margin = new Padding(5, 0, 5, 0);
        walletNameLabel.Name = "walletNameLabel";
        walletNameLabel.Size = new Size(180, 32);
        walletNameLabel.TabIndex = 3;
        walletNameLabel.Text = "No wallet open";
        walletNameLabel.Visible = false;
        // 
        // availableBalanceLabel
        // 
        availableBalanceLabel.Font = new Font("Segoe UI", 10F);
        availableBalanceLabel.ForeColor = Color.FromArgb(105, 105, 91);
        availableBalanceLabel.Location = new Point(49, 190);
        availableBalanceLabel.Margin = new Padding(5, 0, 5, 0);
        availableBalanceLabel.Name = "availableBalanceLabel";
        availableBalanceLabel.Size = new Size(744, 43);
        availableBalanceLabel.TabIndex = 2;
        availableBalanceLabel.Text = "Open a wallet to see your balance.";
        // 
        // balanceValueLabel
        // 
        balanceValueLabel.Font = new Font("Georgia", 23F, FontStyle.Bold);
        balanceValueLabel.ForeColor = Color.FromArgb(50, 58, 49);
        balanceValueLabel.Location = new Point(41, 104);
        balanceValueLabel.Margin = new Padding(5, 0, 5, 0);
        balanceValueLabel.Name = "balanceValueLabel";
        balanceValueLabel.Size = new Size(764, 83);
        balanceValueLabel.TabIndex = 1;
        balanceValueLabel.Text = "0.0000 SLTHY";
        // 
        // balanceCaptionLabel
        // 
        balanceCaptionLabel.AutoSize = true;
        balanceCaptionLabel.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        balanceCaptionLabel.ForeColor = Color.FromArgb(99, 99, 83);
        balanceCaptionLabel.Location = new Point(46, 40);
        balanceCaptionLabel.Margin = new Padding(5, 0, 5, 0);
        balanceCaptionLabel.Name = "balanceCaptionLabel";
        balanceCaptionLabel.Size = new Size(193, 41);
        balanceCaptionLabel.TabIndex = 0;
        balanceCaptionLabel.Text = "Your balance";
        // 
        // miningCard
        // 
        miningCard.BackColor = Color.FromArgb(255, 253, 247);
        miningCard.Controls.Add(_bookMiningAnimation);
        miningCard.Controls.Add(miningStatusLineLabel);
        miningCard.Controls.Add(miningToggleButton);
        miningCard.Controls.Add(miningAddressTextBox);
        miningCard.Controls.Add(miningAddressLabel);
        miningCard.Controls.Add(mineToAddressCheckBox);
        miningCard.Controls.Add(miningEffortComboBox);
        miningCard.Controls.Add(miningDescriptionLabel);
        miningCard.Controls.Add(miningTitleLabel);
        miningCard.Dock = DockStyle.Fill;
        miningCard.Location = new Point(867, 92);
        miningCard.Margin = new Padding(7, 0, 0, 12);
        miningCard.Name = "miningCard";
        miningCard.Size = new Size(510, 478);
        miningCard.TabIndex = 2;
        // 
        // _bookMiningAnimation
        // 
        _bookMiningAnimation.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _bookMiningAnimation.BackColor = Color.Transparent;
        _bookMiningAnimation.Location = new Point(412, 10);
        _bookMiningAnimation.Mining = false;
        _bookMiningAnimation.Name = "_bookMiningAnimation";
        _bookMiningAnimation.Size = new Size(80, 64);
        _bookMiningAnimation.TabIndex = 4;
        // 
        // miningStatusLineLabel
        // 
        miningStatusLineLabel.Font = new Font("Segoe UI", 8.5F);
        miningStatusLineLabel.ForeColor = Color.FromArgb(105, 105, 91);
        miningStatusLineLabel.Location = new Point(44, 423);
        miningStatusLineLabel.Name = "miningStatusLineLabel";
        miningStatusLineLabel.Size = new Size(422, 49);
        miningStatusLineLabel.TabIndex = 8;
        miningStatusLineLabel.Text = "Rewards need 100 blocks before they are ready to spend.";
        // 
        // miningToggleButton
        // 
        miningToggleButton.BackColor = Color.FromArgb(71, 103, 80);
        miningToggleButton.FlatStyle = FlatStyle.Flat;
        miningToggleButton.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        miningToggleButton.ForeColor = Color.White;
        miningToggleButton.Location = new Point(44, 351);
        miningToggleButton.Margin = new Padding(5);
        miningToggleButton.Name = "miningToggleButton";
        miningToggleButton.Size = new Size(422, 64);
        miningToggleButton.TabIndex = 3;
        miningToggleButton.Text = "Start mining";
        miningToggleButton.UseVisualStyleBackColor = false;
        miningToggleButton.Click += miningToggleButton_Click;
        // 
        // miningAddressTextBox
        // 
        miningAddressTextBox.Font = new Font("Cascadia Mono", 8.5F);
        miningAddressTextBox.Location = new Point(44, 309);
        miningAddressTextBox.Name = "miningAddressTextBox";
        miningAddressTextBox.PlaceholderText = "Paste a Slithy address";
        miningAddressTextBox.Size = new Size(422, 34);
        miningAddressTextBox.TabIndex = 7;
        miningAddressTextBox.TextChanged += miningAddressTextBox_TextChanged;
        // 
        // miningAddressLabel
        // 
        miningAddressLabel.AutoSize = true;
        miningAddressLabel.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        miningAddressLabel.ForeColor = Color.FromArgb(99, 99, 83);
        miningAddressLabel.Location = new Point(44, 274);
        miningAddressLabel.Name = "miningAddressLabel";
        miningAddressLabel.Size = new Size(174, 31);
        miningAddressLabel.TabIndex = 6;
        miningAddressLabel.Text = "Mining address";
        // 
        // mineToAddressCheckBox
        // 
        mineToAddressCheckBox.AutoSize = true;
        mineToAddressCheckBox.Font = new Font("Segoe UI", 9F);
        mineToAddressCheckBox.ForeColor = Color.FromArgb(77, 86, 75);
        mineToAddressCheckBox.Location = new Point(44, 234);
        mineToAddressCheckBox.Name = "mineToAddressCheckBox";
        mineToAddressCheckBox.Size = new Size(374, 36);
        mineToAddressCheckBox.TabIndex = 5;
        mineToAddressCheckBox.Text = "Mine to another Slithy address";
        mineToAddressCheckBox.UseVisualStyleBackColor = true;
        mineToAddressCheckBox.CheckedChanged += mineToAddressCheckBox_CheckedChanged;
        // 
        // miningEffortComboBox
        // 
        miningEffortComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        miningEffortComboBox.Font = new Font("Segoe UI", 10F);
        miningEffortComboBox.FormattingEnabled = true;
        miningEffortComboBox.Items.AddRange(new object[] { "Light and quiet", "Balanced", "Full power" });
        miningEffortComboBox.Location = new Point(44, 176);
        miningEffortComboBox.Margin = new Padding(5);
        miningEffortComboBox.Name = "miningEffortComboBox";
        miningEffortComboBox.Size = new Size(420, 45);
        miningEffortComboBox.TabIndex = 2;
        // 
        // miningDescriptionLabel
        // 
        miningDescriptionLabel.Font = new Font("Segoe UI", 10F);
        miningDescriptionLabel.ForeColor = Color.FromArgb(99, 99, 83);
        miningDescriptionLabel.Location = new Point(44, 101);
        miningDescriptionLabel.Margin = new Padding(5, 0, 5, 0);
        miningDescriptionLabel.Name = "miningDescriptionLabel";
        miningDescriptionLabel.Size = new Size(422, 62);
        miningDescriptionLabel.TabIndex = 1;
        miningDescriptionLabel.Text = "Use this computer's CPU to help the Slithy network and earn mining rewards.";
        // 
        // miningTitleLabel
        // 
        miningTitleLabel.AutoSize = true;
        miningTitleLabel.Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold);
        miningTitleLabel.ForeColor = Color.FromArgb(50, 58, 49);
        miningTitleLabel.Location = new Point(39, 38);
        miningTitleLabel.Margin = new Padding(5, 0, 5, 0);
        miningTitleLabel.Name = "miningTitleLabel";
        miningTitleLabel.Size = new Size(204, 51);
        miningTitleLabel.TabIndex = 0;
        miningTitleLabel.Text = "Earn Slithy";
        // 
        // _homeMissionCard
        // 
        _homeMissionCard.BackColor = Color.FromArgb(229, 239, 223);
        _homeLayout.SetColumnSpan(_homeMissionCard, 2);
        _homeMissionCard.Controls.Add(_homeWebsiteLinkLabel);
        _homeMissionCard.Controls.Add(_homeMissionTitle);
        _homeMissionCard.Dock = DockStyle.Fill;
        _homeMissionCard.Location = new Point(18, 582);
        _homeMissionCard.Margin = new Padding(0);
        _homeMissionCard.Name = "_homeMissionCard";
        _homeMissionCard.Size = new Size(1359, 86);
        _homeMissionCard.TabIndex = 3;
        // 
        // _homeWebsiteLinkLabel
        // 
        _homeWebsiteLinkLabel.ActiveLinkColor = Color.FromArgb(108, 133, 83);
        _homeWebsiteLinkLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _homeWebsiteLinkLabel.AutoSize = true;
        _homeWebsiteLinkLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        _homeWebsiteLinkLabel.LinkColor = Color.FromArgb(71, 103, 80);
        _homeWebsiteLinkLabel.Location = new Point(1165, 23);
        _homeWebsiteLinkLabel.Name = "_homeWebsiteLinkLabel";
        _homeWebsiteLinkLabel.Size = new Size(171, 37);
        _homeWebsiteLinkLabel.TabIndex = 2;
        _homeWebsiteLinkLabel.TabStop = true;
        _homeWebsiteLinkLabel.Text = "Visit slithy.io";
        _homeWebsiteLinkLabel.VisitedLinkColor = Color.FromArgb(71, 103, 80);
        // 
        // _homeMissionTitle
        // 
        _homeMissionTitle.AutoSize = true;
        _homeMissionTitle.Font = new Font("Georgia", 12.25F, FontStyle.Bold);
        _homeMissionTitle.ForeColor = Color.FromArgb(50, 75, 58);
        _homeMissionTitle.Location = new Point(26, 22);
        _homeMissionTitle.MaximumSize = new Size(900, 0);
        _homeMissionTitle.Name = "_homeMissionTitle";
        _homeMissionTitle.Size = new Size(802, 38);
        _homeMissionTitle.TabIndex = 0;
        _homeMissionTitle.Text = "10% of every block supports children's literacy";
        // 
        // _walletActivityTab
        // 
        _walletActivityTab.BackColor = Color.FromArgb(246, 241, 232);
        _walletActivityTab.Controls.Add(_activityCard);
        _walletActivityTab.Controls.Add(_walletActivityHeaderPanel);
        _walletActivityTab.Location = new Point(8, 59);
        _walletActivityTab.Name = "_walletActivityTab";
        _walletActivityTab.Padding = new Padding(22);
        _walletActivityTab.Size = new Size(1479, 766);
        _walletActivityTab.TabIndex = 2;
        _walletActivityTab.Text = "Activity";
        // 
        // _activityCard
        // 
        _activityCard.BackColor = Color.FromArgb(255, 253, 247);
        _activityCard.Controls.Add(_activityEmptyLabel);
        _activityCard.Controls.Add(_walletActivityListView);
        _activityCard.Dock = DockStyle.Fill;
        _activityCard.Location = new Point(22, 114);
        _activityCard.Margin = new Padding(0);
        _activityCard.Name = "_activityCard";
        _activityCard.Padding = new Padding(12);
        _activityCard.Size = new Size(1435, 630);
        _activityCard.TabIndex = 1;
        // 
        // _activityEmptyLabel
        // 
        _activityEmptyLabel.Dock = DockStyle.Fill;
        _activityEmptyLabel.Font = new Font("Georgia", 14F, FontStyle.Italic);
        _activityEmptyLabel.ForeColor = Color.FromArgb(102, 110, 92);
        _activityEmptyLabel.Location = new Point(12, 12);
        _activityEmptyLabel.Name = "_activityEmptyLabel";
        _activityEmptyLabel.Size = new Size(1411, 606);
        _activityEmptyLabel.TabIndex = 1;
        _activityEmptyLabel.Text = "No wallet activity yet\r\n\r\nSent and received payments will appear here.";
        _activityEmptyLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // _walletActivityListView
        // 
        _walletActivityListView.BackColor = Color.FromArgb(255, 253, 247);
        _walletActivityListView.BorderStyle = BorderStyle.None;
        _walletActivityListView.Columns.AddRange(new ColumnHeader[] { walletActivityTypeColumnHeader, walletActivityAmountColumnHeader, walletActivityDateColumnHeader });
        _walletActivityListView.Dock = DockStyle.Fill;
        _walletActivityListView.Font = new Font("Segoe UI", 10F);
        _walletActivityListView.FullRowSelect = true;
        _walletActivityListView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _walletActivityListView.Location = new Point(12, 12);
        _walletActivityListView.Name = "_walletActivityListView";
        _walletActivityListView.ShowItemToolTips = true;
        _walletActivityListView.Size = new Size(1411, 606);
        _walletActivityListView.TabIndex = 0;
        _walletActivityListView.UseCompatibleStateImageBehavior = false;
        _walletActivityListView.View = View.Details;
        _walletActivityListView.DoubleClick += activityListView_DoubleClick;
        // 
        // walletActivityTypeColumnHeader
        // 
        walletActivityTypeColumnHeader.Text = "What happened";
        walletActivityTypeColumnHeader.Width = 250;
        // 
        // walletActivityAmountColumnHeader
        // 
        walletActivityAmountColumnHeader.Text = "Amount";
        walletActivityAmountColumnHeader.Width = 230;
        // 
        // walletActivityDateColumnHeader
        // 
        walletActivityDateColumnHeader.Text = "Date";
        walletActivityDateColumnHeader.Width = 270;
        // 
        // _walletActivityHeaderPanel
        // 
        _walletActivityHeaderPanel.BackColor = Color.FromArgb(246, 241, 232);
        _walletActivityHeaderPanel.Controls.Add(_walletActivityHelpLabel);
        _walletActivityHeaderPanel.Controls.Add(_walletActivityHeadingLabel);
        _walletActivityHeaderPanel.Dock = DockStyle.Top;
        _walletActivityHeaderPanel.Location = new Point(22, 22);
        _walletActivityHeaderPanel.Name = "_walletActivityHeaderPanel";
        _walletActivityHeaderPanel.Size = new Size(1435, 92);
        _walletActivityHeaderPanel.TabIndex = 0;
        // 
        // _walletActivityHelpLabel
        // 
        _walletActivityHelpLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _walletActivityHelpLabel.Font = new Font("Segoe UI", 10F);
        _walletActivityHelpLabel.ForeColor = Color.FromArgb(99, 99, 83);
        _walletActivityHelpLabel.Location = new Point(10, 50);
        _walletActivityHelpLabel.Name = "_walletActivityHelpLabel";
        _walletActivityHelpLabel.Size = new Size(720, 30);
        _walletActivityHelpLabel.TabIndex = 1;
        _walletActivityHelpLabel.Text = "All sent and received payments. Double-click an entry for details.";
        // 
        // _walletActivityHeadingLabel
        // 
        _walletActivityHeadingLabel.AutoSize = true;
        _walletActivityHeadingLabel.Font = new Font("Georgia", 19F, FontStyle.Bold);
        _walletActivityHeadingLabel.ForeColor = Color.FromArgb(50, 58, 49);
        _walletActivityHeadingLabel.Location = new Point(8, 6);
        _walletActivityHeadingLabel.Name = "_walletActivityHeadingLabel";
        _walletActivityHeadingLabel.Size = new Size(401, 59);
        _walletActivityHeadingLabel.TabIndex = 0;
        _walletActivityHeadingLabel.Text = "Wallet activity";
        // 
        // advancedTabPage
        // 
        advancedTabPage.BackColor = Color.FromArgb(246, 241, 232);
        advancedTabPage.Controls.Add(advancedTabControl);
        advancedTabPage.Location = new Point(8, 59);
        advancedTabPage.Margin = new Padding(5);
        advancedTabPage.Name = "advancedTabPage";
        advancedTabPage.Padding = new Padding(26);
        advancedTabPage.Size = new Size(1479, 766);
        advancedTabPage.TabIndex = 1;
        advancedTabPage.Text = "Settings";
        // 
        // advancedTabControl
        // 
        advancedTabControl.Controls.Add(connectionTabPage);
        advancedTabControl.Controls.Add(updatesTabPage);
        advancedTabControl.Controls.Add(activityTabPage);
        advancedTabControl.Dock = DockStyle.Fill;
        advancedTabControl.Font = new Font("Segoe UI", 9.5F);
        advancedTabControl.Location = new Point(26, 26);
        advancedTabControl.Margin = new Padding(5);
        advancedTabControl.Name = "advancedTabControl";
        advancedTabControl.SelectedIndex = 0;
        advancedTabControl.Size = new Size(1427, 714);
        advancedTabControl.TabIndex = 0;
        // 
        // connectionTabPage
        // 
        connectionTabPage.BackColor = Color.FromArgb(246, 241, 232);
        connectionTabPage.Controls.Add(statusTableLayoutPanel);
        connectionTabPage.Controls.Add(connectionGroupBox);
        connectionTabPage.Location = new Point(8, 49);
        connectionTabPage.Margin = new Padding(5);
        connectionTabPage.Name = "connectionTabPage";
        connectionTabPage.Padding = new Padding(5);
        connectionTabPage.Size = new Size(1411, 657);
        connectionTabPage.TabIndex = 0;
        connectionTabPage.Text = "Network";
        // 
        // statusTableLayoutPanel
        // 
        statusTableLayoutPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        statusTableLayoutPanel.BackColor = Color.FromArgb(255, 253, 247);
        statusTableLayoutPanel.ColumnCount = 2;
        statusTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
        statusTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
        statusTableLayoutPanel.Controls.Add(heightCaptionLabel, 0, 0);
        statusTableLayoutPanel.Controls.Add(heightValueLabel, 1, 0);
        statusTableLayoutPanel.Controls.Add(difficultyCaptionLabel, 0, 1);
        statusTableLayoutPanel.Controls.Add(difficultyValueLabel, 1, 1);
        statusTableLayoutPanel.Controls.Add(networkCaptionLabel, 0, 2);
        statusTableLayoutPanel.Controls.Add(networkValueLabel, 1, 2);
        statusTableLayoutPanel.Controls.Add(syncCaptionLabel, 0, 3);
        statusTableLayoutPanel.Controls.Add(syncValueLabel, 1, 3);
        statusTableLayoutPanel.Controls.Add(versionCaptionLabel, 0, 4);
        statusTableLayoutPanel.Controls.Add(versionValueLabel, 1, 4);
        statusTableLayoutPanel.Controls.Add(poolCaptionLabel, 0, 5);
        statusTableLayoutPanel.Controls.Add(poolValueLabel, 1, 5);
        statusTableLayoutPanel.Controls.Add(targetCaptionLabel, 0, 6);
        statusTableLayoutPanel.Controls.Add(targetValueLabel, 1, 6);
        statusTableLayoutPanel.Controls.Add(lastCheckedCaptionLabel, 0, 7);
        statusTableLayoutPanel.Controls.Add(lastCheckedValueLabel, 1, 7);
        statusTableLayoutPanel.Controls.Add(blockchainDataCaptionLabel, 0, 8);
        statusTableLayoutPanel.Controls.Add(blockchainDataValueLabel, 1, 8);
        statusTableLayoutPanel.Controls.Add(freeDiskCaptionLabel, 0, 9);
        statusTableLayoutPanel.Controls.Add(freeDiskValueLabel, 1, 9);
        statusTableLayoutPanel.Location = new Point(32, 264);
        statusTableLayoutPanel.Margin = new Padding(5);
        statusTableLayoutPanel.Name = "statusTableLayoutPanel";
        statusTableLayoutPanel.RowCount = 10;
        statusTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
        statusTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
        statusTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
        statusTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
        statusTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
        statusTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
        statusTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
        statusTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
        statusTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
        statusTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
        statusTableLayoutPanel.Size = new Size(1336, 330);
        statusTableLayoutPanel.TabIndex = 1;
        // 
        // heightCaptionLabel
        // 
        heightCaptionLabel.Dock = DockStyle.Fill;
        heightCaptionLabel.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        heightCaptionLabel.ForeColor = Color.FromArgb(77, 86, 75);
        heightCaptionLabel.Location = new Point(0, 0);
        heightCaptionLabel.Margin = new Padding(0);
        heightCaptionLabel.Name = "heightCaptionLabel";
        heightCaptionLabel.Padding = new Padding(26, 0, 0, 0);
        heightCaptionLabel.Size = new Size(467, 41);
        heightCaptionLabel.TabIndex = 0;
        heightCaptionLabel.Text = "Block height";
        heightCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // heightValueLabel
        // 
        heightValueLabel.Dock = DockStyle.Fill;
        heightValueLabel.Font = new Font("Segoe UI", 9.5F);
        heightValueLabel.ForeColor = Color.FromArgb(42, 48, 41);
        heightValueLabel.Location = new Point(467, 0);
        heightValueLabel.Margin = new Padding(0);
        heightValueLabel.Name = "heightValueLabel";
        heightValueLabel.Padding = new Padding(26, 0, 0, 0);
        heightValueLabel.Size = new Size(869, 41);
        heightValueLabel.TabIndex = 1;
        heightValueLabel.Text = "0";
        heightValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // difficultyCaptionLabel
        // 
        difficultyCaptionLabel.Dock = DockStyle.Fill;
        difficultyCaptionLabel.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        difficultyCaptionLabel.ForeColor = Color.FromArgb(77, 86, 75);
        difficultyCaptionLabel.Location = new Point(0, 41);
        difficultyCaptionLabel.Margin = new Padding(0);
        difficultyCaptionLabel.Name = "difficultyCaptionLabel";
        difficultyCaptionLabel.Padding = new Padding(26, 0, 0, 0);
        difficultyCaptionLabel.Size = new Size(467, 41);
        difficultyCaptionLabel.TabIndex = 2;
        difficultyCaptionLabel.Text = "Difficulty";
        difficultyCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // difficultyValueLabel
        // 
        difficultyValueLabel.Dock = DockStyle.Fill;
        difficultyValueLabel.Font = new Font("Segoe UI", 9.5F);
        difficultyValueLabel.ForeColor = Color.FromArgb(42, 48, 41);
        difficultyValueLabel.Location = new Point(467, 41);
        difficultyValueLabel.Margin = new Padding(0);
        difficultyValueLabel.Name = "difficultyValueLabel";
        difficultyValueLabel.Padding = new Padding(26, 0, 0, 0);
        difficultyValueLabel.Size = new Size(869, 41);
        difficultyValueLabel.TabIndex = 3;
        difficultyValueLabel.Text = "0";
        difficultyValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // networkCaptionLabel
        // 
        networkCaptionLabel.Dock = DockStyle.Fill;
        networkCaptionLabel.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        networkCaptionLabel.ForeColor = Color.FromArgb(77, 86, 75);
        networkCaptionLabel.Location = new Point(0, 82);
        networkCaptionLabel.Margin = new Padding(0);
        networkCaptionLabel.Name = "networkCaptionLabel";
        networkCaptionLabel.Padding = new Padding(26, 0, 0, 0);
        networkCaptionLabel.Size = new Size(467, 41);
        networkCaptionLabel.TabIndex = 4;
        networkCaptionLabel.Text = "Network";
        networkCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // networkValueLabel
        // 
        networkValueLabel.Dock = DockStyle.Fill;
        networkValueLabel.Font = new Font("Segoe UI", 9.5F);
        networkValueLabel.ForeColor = Color.FromArgb(42, 48, 41);
        networkValueLabel.Location = new Point(467, 82);
        networkValueLabel.Margin = new Padding(0);
        networkValueLabel.Name = "networkValueLabel";
        networkValueLabel.Padding = new Padding(26, 0, 0, 0);
        networkValueLabel.Size = new Size(869, 41);
        networkValueLabel.TabIndex = 5;
        networkValueLabel.Text = "Unknown";
        networkValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // syncCaptionLabel
        // 
        syncCaptionLabel.Dock = DockStyle.Fill;
        syncCaptionLabel.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        syncCaptionLabel.ForeColor = Color.FromArgb(77, 86, 75);
        syncCaptionLabel.Location = new Point(0, 123);
        syncCaptionLabel.Margin = new Padding(0);
        syncCaptionLabel.Name = "syncCaptionLabel";
        syncCaptionLabel.Padding = new Padding(26, 0, 0, 0);
        syncCaptionLabel.Size = new Size(467, 41);
        syncCaptionLabel.TabIndex = 6;
        syncCaptionLabel.Text = "Status";
        syncCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // syncValueLabel
        // 
        syncValueLabel.Dock = DockStyle.Fill;
        syncValueLabel.Font = new Font("Segoe UI", 9.5F);
        syncValueLabel.ForeColor = Color.FromArgb(42, 48, 41);
        syncValueLabel.Location = new Point(467, 123);
        syncValueLabel.Margin = new Padding(0);
        syncValueLabel.Name = "syncValueLabel";
        syncValueLabel.Padding = new Padding(26, 0, 0, 0);
        syncValueLabel.Size = new Size(869, 41);
        syncValueLabel.TabIndex = 7;
        syncValueLabel.Text = "Unknown";
        syncValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // versionCaptionLabel
        // 
        versionCaptionLabel.Dock = DockStyle.Fill;
        versionCaptionLabel.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        versionCaptionLabel.ForeColor = Color.FromArgb(77, 86, 75);
        versionCaptionLabel.Location = new Point(0, 164);
        versionCaptionLabel.Margin = new Padding(0);
        versionCaptionLabel.Name = "versionCaptionLabel";
        versionCaptionLabel.Padding = new Padding(26, 0, 0, 0);
        versionCaptionLabel.Size = new Size(467, 41);
        versionCaptionLabel.TabIndex = 8;
        versionCaptionLabel.Text = "Core version";
        versionCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // versionValueLabel
        // 
        versionValueLabel.Dock = DockStyle.Fill;
        versionValueLabel.Font = new Font("Segoe UI", 9.5F);
        versionValueLabel.ForeColor = Color.FromArgb(42, 48, 41);
        versionValueLabel.Location = new Point(467, 164);
        versionValueLabel.Margin = new Padding(0);
        versionValueLabel.Name = "versionValueLabel";
        versionValueLabel.Padding = new Padding(26, 0, 0, 0);
        versionValueLabel.Size = new Size(869, 41);
        versionValueLabel.TabIndex = 9;
        versionValueLabel.Text = "Unknown";
        versionValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // poolCaptionLabel
        // 
        poolCaptionLabel.Dock = DockStyle.Fill;
        poolCaptionLabel.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        poolCaptionLabel.ForeColor = Color.FromArgb(77, 86, 75);
        poolCaptionLabel.Location = new Point(0, 205);
        poolCaptionLabel.Margin = new Padding(0);
        poolCaptionLabel.Name = "poolCaptionLabel";
        poolCaptionLabel.Padding = new Padding(26, 0, 0, 0);
        poolCaptionLabel.Size = new Size(467, 41);
        poolCaptionLabel.TabIndex = 10;
        poolCaptionLabel.Text = "Transactions waiting";
        poolCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // poolValueLabel
        // 
        poolValueLabel.Dock = DockStyle.Fill;
        poolValueLabel.Font = new Font("Segoe UI", 9.5F);
        poolValueLabel.ForeColor = Color.FromArgb(42, 48, 41);
        poolValueLabel.Location = new Point(467, 205);
        poolValueLabel.Margin = new Padding(0);
        poolValueLabel.Name = "poolValueLabel";
        poolValueLabel.Padding = new Padding(26, 0, 0, 0);
        poolValueLabel.Size = new Size(869, 41);
        poolValueLabel.TabIndex = 11;
        poolValueLabel.Text = "0";
        poolValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // targetCaptionLabel
        // 
        targetCaptionLabel.Dock = DockStyle.Fill;
        targetCaptionLabel.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        targetCaptionLabel.ForeColor = Color.FromArgb(77, 86, 75);
        targetCaptionLabel.Location = new Point(0, 246);
        targetCaptionLabel.Margin = new Padding(0);
        targetCaptionLabel.Name = "targetCaptionLabel";
        targetCaptionLabel.Padding = new Padding(26, 0, 0, 0);
        targetCaptionLabel.Size = new Size(467, 41);
        targetCaptionLabel.TabIndex = 12;
        targetCaptionLabel.Text = "Block target";
        targetCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // targetValueLabel
        // 
        targetValueLabel.Dock = DockStyle.Fill;
        targetValueLabel.Font = new Font("Segoe UI", 9.5F);
        targetValueLabel.ForeColor = Color.FromArgb(42, 48, 41);
        targetValueLabel.Location = new Point(467, 246);
        targetValueLabel.Margin = new Padding(0);
        targetValueLabel.Name = "targetValueLabel";
        targetValueLabel.Padding = new Padding(26, 0, 0, 0);
        targetValueLabel.Size = new Size(869, 41);
        targetValueLabel.TabIndex = 13;
        targetValueLabel.Text = "Unknown";
        targetValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lastCheckedCaptionLabel
        // 
        lastCheckedCaptionLabel.Dock = DockStyle.Fill;
        lastCheckedCaptionLabel.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        lastCheckedCaptionLabel.ForeColor = Color.FromArgb(77, 86, 75);
        lastCheckedCaptionLabel.Location = new Point(0, 287);
        lastCheckedCaptionLabel.Margin = new Padding(0);
        lastCheckedCaptionLabel.Name = "lastCheckedCaptionLabel";
        lastCheckedCaptionLabel.Padding = new Padding(26, 0, 0, 0);
        lastCheckedCaptionLabel.Size = new Size(467, 43);
        lastCheckedCaptionLabel.TabIndex = 14;
        lastCheckedCaptionLabel.Text = "Last checked";
        lastCheckedCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lastCheckedValueLabel
        // 
        lastCheckedValueLabel.Dock = DockStyle.Fill;
        lastCheckedValueLabel.Font = new Font("Segoe UI", 9.5F);
        lastCheckedValueLabel.ForeColor = Color.FromArgb(42, 48, 41);
        lastCheckedValueLabel.Location = new Point(467, 287);
        lastCheckedValueLabel.Margin = new Padding(0);
        lastCheckedValueLabel.Name = "lastCheckedValueLabel";
        lastCheckedValueLabel.Padding = new Padding(26, 0, 0, 0);
        lastCheckedValueLabel.Size = new Size(869, 43);
        lastCheckedValueLabel.TabIndex = 15;
        lastCheckedValueLabel.Text = "Never";
        lastCheckedValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // blockchainDataCaptionLabel
        // 
        blockchainDataCaptionLabel.Dock = DockStyle.Fill;
        blockchainDataCaptionLabel.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        blockchainDataCaptionLabel.ForeColor = Color.FromArgb(77, 86, 75);
        blockchainDataCaptionLabel.Location = new Point(0, 330);
        blockchainDataCaptionLabel.Margin = new Padding(0);
        blockchainDataCaptionLabel.Name = "blockchainDataCaptionLabel";
        blockchainDataCaptionLabel.Padding = new Padding(26, 0, 0, 0);
        blockchainDataCaptionLabel.Size = new Size(467, 33);
        blockchainDataCaptionLabel.TabIndex = 16;
        blockchainDataCaptionLabel.Text = "Blockchain data";
        blockchainDataCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // blockchainDataValueLabel
        // 
        blockchainDataValueLabel.Dock = DockStyle.Fill;
        blockchainDataValueLabel.Font = new Font("Segoe UI", 9.5F);
        blockchainDataValueLabel.ForeColor = Color.FromArgb(42, 48, 41);
        blockchainDataValueLabel.Location = new Point(467, 330);
        blockchainDataValueLabel.Margin = new Padding(0);
        blockchainDataValueLabel.Name = "blockchainDataValueLabel";
        blockchainDataValueLabel.Padding = new Padding(26, 0, 0, 0);
        blockchainDataValueLabel.Size = new Size(869, 33);
        blockchainDataValueLabel.TabIndex = 17;
        blockchainDataValueLabel.Text = "Checking...";
        blockchainDataValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // freeDiskCaptionLabel
        // 
        freeDiskCaptionLabel.Dock = DockStyle.Fill;
        freeDiskCaptionLabel.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        freeDiskCaptionLabel.ForeColor = Color.FromArgb(77, 86, 75);
        freeDiskCaptionLabel.Location = new Point(0, 363);
        freeDiskCaptionLabel.Margin = new Padding(0);
        freeDiskCaptionLabel.Name = "freeDiskCaptionLabel";
        freeDiskCaptionLabel.Padding = new Padding(26, 0, 0, 0);
        freeDiskCaptionLabel.Size = new Size(467, 33);
        freeDiskCaptionLabel.TabIndex = 18;
        freeDiskCaptionLabel.Text = "Free disk space";
        freeDiskCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // freeDiskValueLabel
        // 
        freeDiskValueLabel.Dock = DockStyle.Fill;
        freeDiskValueLabel.Font = new Font("Segoe UI", 9.5F);
        freeDiskValueLabel.ForeColor = Color.FromArgb(42, 48, 41);
        freeDiskValueLabel.Location = new Point(467, 363);
        freeDiskValueLabel.Margin = new Padding(0);
        freeDiskValueLabel.Name = "freeDiskValueLabel";
        freeDiskValueLabel.Padding = new Padding(26, 0, 0, 0);
        freeDiskValueLabel.Size = new Size(869, 33);
        freeDiskValueLabel.TabIndex = 19;
        freeDiskValueLabel.Text = "Checking...";
        freeDiskValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // connectionGroupBox
        // 
        connectionGroupBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        connectionGroupBox.BackColor = Color.FromArgb(255, 253, 247);
        connectionGroupBox.Controls.Add(stopNodeButton);
        connectionGroupBox.Controls.Add(startNodeButton);
        connectionGroupBox.Controls.Add(refreshButton);
        connectionGroupBox.Controls.Add(nodeAddressTextBox);
        connectionGroupBox.Controls.Add(officialNodeComboBox);
        connectionGroupBox.Controls.Add(connectionModeComboBox);
        connectionGroupBox.Controls.Add(connectionStatusLabel);
        connectionGroupBox.Controls.Add(connectionDotPanel);
        connectionGroupBox.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        connectionGroupBox.Location = new Point(32, 32);
        connectionGroupBox.Margin = new Padding(5);
        connectionGroupBox.Name = "connectionGroupBox";
        connectionGroupBox.Padding = new Padding(5);
        connectionGroupBox.Size = new Size(1336, 205);
        connectionGroupBox.TabIndex = 0;
        connectionGroupBox.TabStop = false;
        connectionGroupBox.Text = "Network connection";
        // 
        // stopNodeButton
        // 
        stopNodeButton.BackColor = Color.FromArgb(239, 226, 198);
        stopNodeButton.FlatAppearance.BorderSize = 0;
        stopNodeButton.FlatStyle = FlatStyle.Flat;
        stopNodeButton.ForeColor = Color.FromArgb(58, 68, 55);
        stopNodeButton.Location = new Point(1188, 104);
        stopNodeButton.Margin = new Padding(5);
        stopNodeButton.Name = "stopNodeButton";
        stopNodeButton.Size = new Size(122, 53);
        stopNodeButton.TabIndex = 6;
        stopNodeButton.Text = "Stop";
        stopNodeButton.UseVisualStyleBackColor = false;
        stopNodeButton.Visible = false;
        stopNodeButton.Click += stopNodeButton_Click;
        // 
        // startNodeButton
        // 
        startNodeButton.BackColor = Color.FromArgb(239, 226, 198);
        startNodeButton.FlatAppearance.BorderSize = 0;
        startNodeButton.FlatStyle = FlatStyle.Flat;
        startNodeButton.ForeColor = Color.FromArgb(58, 68, 55);
        startNodeButton.Location = new Point(1051, 104);
        startNodeButton.Margin = new Padding(5);
        startNodeButton.Name = "startNodeButton";
        startNodeButton.Size = new Size(122, 53);
        startNodeButton.TabIndex = 5;
        startNodeButton.Text = "Start";
        startNodeButton.UseVisualStyleBackColor = false;
        startNodeButton.Visible = false;
        startNodeButton.Click += startNodeButton_Click;
        // 
        // refreshButton
        // 
        refreshButton.BackColor = Color.FromArgb(71, 103, 80);
        refreshButton.FlatAppearance.BorderSize = 0;
        refreshButton.FlatStyle = FlatStyle.Flat;
        refreshButton.ForeColor = Color.White;
        refreshButton.Location = new Point(1183, 104);
        refreshButton.Margin = new Padding(5);
        refreshButton.Name = "refreshButton";
        refreshButton.Size = new Size(127, 53);
        refreshButton.TabIndex = 4;
        refreshButton.Text = "Refresh";
        refreshButton.UseVisualStyleBackColor = false;
        refreshButton.Click += refreshButton_Click;
        // 
        // nodeAddressTextBox
        // 
        nodeAddressTextBox.Font = new Font("Segoe UI", 9.5F);
        nodeAddressTextBox.Location = new Point(664, 107);
        nodeAddressTextBox.Margin = new Padding(5);
        nodeAddressTextBox.Name = "nodeAddressTextBox";
        nodeAddressTextBox.ReadOnly = true;
        nodeAddressTextBox.Size = new Size(492, 41);
        nodeAddressTextBox.TabIndex = 4;
        // 
        // officialNodeComboBox
        // 
        officialNodeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        officialNodeComboBox.Font = new Font("Segoe UI", 9.5F);
        officialNodeComboBox.FormattingEnabled = true;
        officialNodeComboBox.Items.AddRange(new object[] { "Automatic closest", "Borogove (East Coast)", "Mome (West Coast)", "Rath (Europe)" });
        officialNodeComboBox.Location = new Point(396, 107);
        officialNodeComboBox.Margin = new Padding(5);
        officialNodeComboBox.Name = "officialNodeComboBox";
        officialNodeComboBox.Size = new Size(248, 43);
        officialNodeComboBox.TabIndex = 3;
        officialNodeComboBox.SelectedIndexChanged += officialNodeComboBox_SelectedIndexChanged;
        // 
        // connectionModeComboBox
        // 
        connectionModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        connectionModeComboBox.Font = new Font("Segoe UI", 9.5F);
        connectionModeComboBox.FormattingEnabled = true;
        connectionModeComboBox.Items.AddRange(new object[] { "Official Slithy nodes", "Custom node" });
        connectionModeComboBox.Location = new Point(36, 107);
        connectionModeComboBox.Margin = new Padding(5);
        connectionModeComboBox.Name = "connectionModeComboBox";
        connectionModeComboBox.Size = new Size(339, 43);
        connectionModeComboBox.TabIndex = 2;
        connectionModeComboBox.SelectedIndexChanged += connectionModeComboBox_SelectedIndexChanged;
        // 
        // connectionStatusLabel
        // 
        connectionStatusLabel.AutoSize = true;
        connectionStatusLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        connectionStatusLabel.Location = new Point(68, 45);
        connectionStatusLabel.Margin = new Padding(5, 0, 5, 0);
        connectionStatusLabel.Name = "connectionStatusLabel";
        connectionStatusLabel.Size = new Size(274, 37);
        connectionStatusLabel.TabIndex = 1;
        connectionStatusLabel.Text = "Checking connection";
        // 
        // connectionDotPanel
        // 
        connectionDotPanel.BackColor = Color.Gray;
        connectionDotPanel.Location = new Point(36, 53);
        connectionDotPanel.Margin = new Padding(5);
        connectionDotPanel.Name = "connectionDotPanel";
        connectionDotPanel.Size = new Size(20, 19);
        connectionDotPanel.TabIndex = 0;
        // 
        // updatesTabPage
        // 
        updatesTabPage.BackColor = Color.FromArgb(246, 241, 232);
        updatesTabPage.Controls.Add(updatesCard);
        updatesTabPage.Location = new Point(8, 49);
        updatesTabPage.Margin = new Padding(5);
        updatesTabPage.Name = "updatesTabPage";
        updatesTabPage.Padding = new Padding(5);
        updatesTabPage.Size = new Size(1411, 657);
        updatesTabPage.TabIndex = 1;
        updatesTabPage.Text = "Updates";
        // 
        // updatesCard
        // 
        updatesCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        updatesCard.BackColor = Color.FromArgb(255, 253, 247);
        updatesCard.Controls.Add(supportEmailLinkLabel);
        updatesCard.Controls.Add(updateSafetyLabel);
        updatesCard.Controls.Add(autoLockMinutesNumericUpDown);
        updatesCard.Controls.Add(autoLockLabel);
        updatesCard.Controls.Add(importWalletButton);
        updatesCard.Controls.Add(backupWalletButton);
        updatesCard.Controls.Add(checkUpdatesButton);
        updatesCard.Controls.Add(minimizeToTrayOnCloseCheckBox);
        updatesCard.Controls.Add(automaticUpdatesCheckBox);
        updatesCard.Controls.Add(updateProgressTrackPanel);
        updatesCard.Controls.Add(updateStatusLabel);
        updatesCard.Controls.Add(installedVersionLabel);
        updatesCard.Controls.Add(updatesTitleLabel);
        updatesCard.Location = new Point(55, 35);
        updatesCard.Margin = new Padding(5);
        updatesCard.Name = "updatesCard";
        updatesCard.Size = new Size(1291, 585);
        updatesCard.TabIndex = 0;
        // 
        // updateSafetyLabel
        // 
        updateSafetyLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        updateSafetyLabel.Font = new Font("Segoe UI", 9F);
        updateSafetyLabel.ForeColor = Color.FromArgb(112, 105, 91);
        updateSafetyLabel.Location = new Point(55, 538);
        updateSafetyLabel.Margin = new Padding(5, 0, 5, 0);
        updateSafetyLabel.Name = "updateSafetyLabel";
        updateSafetyLabel.Size = new Size(1177, 40);
        updateSafetyLabel.TabIndex = 5;
        updateSafetyLabel.Text = "Updates must have a valid Slithy release signature and matching file hash. Wallet files are never replaced.";
        // 
        // supportEmailLinkLabel
        // 
        supportEmailLinkLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        supportEmailLinkLabel.AutoSize = true;
        supportEmailLinkLabel.Font = new Font("Segoe UI", 9.5F);
        supportEmailLinkLabel.LinkColor = Color.FromArgb(71, 101, 77);
        supportEmailLinkLabel.Location = new Point(55, 498);
        supportEmailLinkLabel.Name = "supportEmailLinkLabel";
        supportEmailLinkLabel.Size = new Size(506, 34);
        supportEmailLinkLabel.TabIndex = 12;
        supportEmailLinkLabel.TabStop = true;
        supportEmailLinkLabel.Text = "Need help? Write to support@slithy.io";
        // 
        // autoLockMinutesNumericUpDown
        // 
        autoLockMinutesNumericUpDown.Location = new Point(1090, 304);
        autoLockMinutesNumericUpDown.Margin = new Padding(5);
        autoLockMinutesNumericUpDown.Maximum = new decimal(new int[] { 120, 0, 0, 0 });
        autoLockMinutesNumericUpDown.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        autoLockMinutesNumericUpDown.Name = "autoLockMinutesNumericUpDown";
        autoLockMinutesNumericUpDown.Size = new Size(130, 41);
        autoLockMinutesNumericUpDown.TabIndex = 7;
        autoLockMinutesNumericUpDown.Value = new decimal(new int[] { 10, 0, 0, 0 });
        autoLockMinutesNumericUpDown.ValueChanged += autoLockMinutesNumericUpDown_ValueChanged;
        // 
        // autoLockLabel
        // 
        autoLockLabel.AutoSize = true;
        autoLockLabel.Font = new Font("Segoe UI", 10F);
        autoLockLabel.Location = new Point(760, 306);
        autoLockLabel.Margin = new Padding(5, 0, 5, 0);
        autoLockLabel.Name = "autoLockLabel";
        autoLockLabel.Size = new Size(317, 37);
        autoLockLabel.TabIndex = 6;
        autoLockLabel.Text = "Lock wallet after minutes:";
        // 
        // importWalletButton
        // 
        importWalletButton.BackColor = Color.FromArgb(239, 226, 198);
        importWalletButton.FlatAppearance.BorderSize = 0;
        importWalletButton.FlatStyle = FlatStyle.Flat;
        importWalletButton.ForeColor = Color.FromArgb(58, 68, 55);
        importWalletButton.Location = new Point(666, 418);
        importWalletButton.Margin = new Padding(5);
        importWalletButton.Name = "importWalletButton";
        importWalletButton.Size = new Size(284, 64);
        importWalletButton.TabIndex = 9;
        importWalletButton.Text = "Import wallet files";
        importWalletButton.UseVisualStyleBackColor = false;
        importWalletButton.Click += importWalletButton_Click;
        // 
        // backupWalletButton
        // 
        backupWalletButton.BackColor = Color.FromArgb(239, 226, 198);
        backupWalletButton.FlatAppearance.BorderSize = 0;
        backupWalletButton.FlatStyle = FlatStyle.Flat;
        backupWalletButton.ForeColor = Color.FromArgb(58, 68, 55);
        backupWalletButton.Location = new Point(358, 418);
        backupWalletButton.Margin = new Padding(5);
        backupWalletButton.Name = "backupWalletButton";
        backupWalletButton.Size = new Size(284, 64);
        backupWalletButton.TabIndex = 8;
        backupWalletButton.Text = "Back up open wallet";
        backupWalletButton.UseVisualStyleBackColor = false;
        backupWalletButton.Click += backupWalletButton_Click;
        // 
        // checkUpdatesButton
        // 
        checkUpdatesButton.BackColor = Color.FromArgb(83, 104, 79);
        checkUpdatesButton.FlatStyle = FlatStyle.Flat;
        checkUpdatesButton.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        checkUpdatesButton.ForeColor = Color.White;
        checkUpdatesButton.Location = new Point(55, 410);
        checkUpdatesButton.Margin = new Padding(5);
        checkUpdatesButton.Name = "checkUpdatesButton";
        checkUpdatesButton.Size = new Size(252, 72);
        checkUpdatesButton.TabIndex = 4;
        checkUpdatesButton.Text = "Check now";
        checkUpdatesButton.UseVisualStyleBackColor = false;
        checkUpdatesButton.Click += checkUpdatesButton_Click;
        // 
        // automaticUpdatesCheckBox
        // 
        automaticUpdatesCheckBox.AutoSize = true;
        automaticUpdatesCheckBox.Checked = true;
        automaticUpdatesCheckBox.CheckState = CheckState.Checked;
        automaticUpdatesCheckBox.Enabled = false;
        automaticUpdatesCheckBox.Font = new Font("Segoe UI", 10F);
        automaticUpdatesCheckBox.Location = new Point(55, 306);
        automaticUpdatesCheckBox.Margin = new Padding(5);
        automaticUpdatesCheckBox.Name = "automaticUpdatesCheckBox";
        automaticUpdatesCheckBox.Size = new Size(558, 41);
        automaticUpdatesCheckBox.TabIndex = 3;
        automaticUpdatesCheckBox.Text = "Updates are checked whenever Slithy starts";
        automaticUpdatesCheckBox.UseVisualStyleBackColor = true;
        automaticUpdatesCheckBox.CheckedChanged += automaticUpdatesCheckBox_CheckedChanged;
        // 
        // minimizeToTrayOnCloseCheckBox
        // 
        minimizeToTrayOnCloseCheckBox.AutoSize = true;
        minimizeToTrayOnCloseCheckBox.Checked = true;
        minimizeToTrayOnCloseCheckBox.CheckState = CheckState.Checked;
        minimizeToTrayOnCloseCheckBox.Font = new Font("Segoe UI", 10F);
        minimizeToTrayOnCloseCheckBox.Location = new Point(55, 356);
        minimizeToTrayOnCloseCheckBox.Margin = new Padding(5);
        minimizeToTrayOnCloseCheckBox.Name = "minimizeToTrayOnCloseCheckBox";
        minimizeToTrayOnCloseCheckBox.Size = new Size(676, 41);
        minimizeToTrayOnCloseCheckBox.TabIndex = 11;
        minimizeToTrayOnCloseCheckBox.Text = "Closing the window sends Slithy to the notification area";
        minimizeToTrayOnCloseCheckBox.UseVisualStyleBackColor = true;
        minimizeToTrayOnCloseCheckBox.CheckedChanged += minimizeToTrayOnCloseCheckBox_CheckedChanged;
        // 
        // updateProgressTrackPanel
        // 
        updateProgressTrackPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        updateProgressTrackPanel.BackColor = Color.FromArgb(218, 211, 193);
        updateProgressTrackPanel.Controls.Add(updateProgressFillPanel);
        updateProgressTrackPanel.Location = new Point(55, 258);
        updateProgressTrackPanel.Name = "updateProgressTrackPanel";
        updateProgressTrackPanel.Size = new Size(1177, 24);
        updateProgressTrackPanel.TabIndex = 10;
        // 
        // updateProgressFillPanel
        // 
        updateProgressFillPanel.BackColor = Color.FromArgb(48, 68, 56);
        updateProgressFillPanel.Dock = DockStyle.Left;
        updateProgressFillPanel.Location = new Point(0, 0);
        updateProgressFillPanel.Name = "updateProgressFillPanel";
        updateProgressFillPanel.Size = new Size(0, 24);
        updateProgressFillPanel.TabIndex = 0;
        // 
        // updateStatusLabel
        // 
        updateStatusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        updateStatusLabel.Font = new Font("Segoe UI", 10F);
        updateStatusLabel.ForeColor = Color.FromArgb(99, 99, 83);
        updateStatusLabel.Location = new Point(55, 190);
        updateStatusLabel.Margin = new Padding(5, 0, 5, 0);
        updateStatusLabel.Name = "updateStatusLabel";
        updateStatusLabel.Size = new Size(1177, 80);
        updateStatusLabel.TabIndex = 2;
        updateStatusLabel.Text = "Update service awaiting release configuration.";
        // 
        // installedVersionLabel
        // 
        installedVersionLabel.AutoSize = true;
        installedVersionLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        installedVersionLabel.ForeColor = Color.FromArgb(84, 84, 70);
        installedVersionLabel.Location = new Point(55, 131);
        installedVersionLabel.Margin = new Padding(5, 0, 5, 0);
        installedVersionLabel.Name = "installedVersionLabel";
        installedVersionLabel.Size = new Size(217, 37);
        installedVersionLabel.TabIndex = 1;
        installedVersionLabel.Text = "Installed version";
        // 
        // updatesTitleLabel
        // 
        updatesTitleLabel.AutoSize = true;
        updatesTitleLabel.Font = new Font("Georgia", 19F, FontStyle.Bold);
        updatesTitleLabel.ForeColor = Color.FromArgb(50, 58, 49);
        updatesTitleLabel.Location = new Point(49, 43);
        updatesTitleLabel.Margin = new Padding(5, 0, 5, 0);
        updatesTitleLabel.Name = "updatesTitleLabel";
        updatesTitleLabel.Size = new Size(399, 59);
        updatesTitleLabel.TabIndex = 0;
        updatesTitleLabel.Text = "Slithy updates";
        // 
        // activityTabPage
        // 
        activityTabPage.BackColor = Color.FromArgb(246, 241, 232);
        activityTabPage.Controls.Add(clearLogButton);
        activityTabPage.Controls.Add(reportBugButton);
        activityTabPage.Controls.Add(exportLogButton);
        activityTabPage.Controls.Add(openDataFolderButton);
        activityTabPage.Controls.Add(logTextBox);
        activityTabPage.Location = new Point(8, 49);
        activityTabPage.Margin = new Padding(5);
        activityTabPage.Name = "activityTabPage";
        activityTabPage.Padding = new Padding(5);
        activityTabPage.Size = new Size(1411, 657);
        activityTabPage.TabIndex = 2;
        activityTabPage.Text = "Technical log";
        // 
        // clearLogButton
        // 
        clearLogButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        clearLogButton.BackColor = Color.FromArgb(239, 226, 198);
        clearLogButton.FlatAppearance.BorderSize = 0;
        clearLogButton.FlatStyle = FlatStyle.Flat;
        clearLogButton.ForeColor = Color.FromArgb(58, 68, 55);
        clearLogButton.Location = new Point(1213, 536);
        clearLogButton.Margin = new Padding(5);
        clearLogButton.Name = "clearLogButton";
        clearLogButton.Size = new Size(159, 58);
        clearLogButton.TabIndex = 3;
        clearLogButton.Text = "Clear";
        clearLogButton.UseVisualStyleBackColor = false;
        clearLogButton.Click += clearLogButton_Click;
        // 
        // reportBugButton
        // 
        reportBugButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        reportBugButton.BackColor = Color.FromArgb(83, 104, 79);
        reportBugButton.FlatAppearance.BorderSize = 0;
        reportBugButton.FlatStyle = FlatStyle.Flat;
        reportBugButton.ForeColor = Color.White;
        reportBugButton.Location = new Point(534, 536);
        reportBugButton.Margin = new Padding(5);
        reportBugButton.Name = "reportBugButton";
        reportBugButton.Size = new Size(180, 58);
        reportBugButton.TabIndex = 4;
        reportBugButton.Text = "Report bug";
        reportBugButton.UseVisualStyleBackColor = false;
        reportBugButton.Click += reportBugButton_Click;
        // 
        // exportLogButton
        // 
        exportLogButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        exportLogButton.BackColor = Color.FromArgb(239, 226, 198);
        exportLogButton.FlatAppearance.BorderSize = 0;
        exportLogButton.FlatStyle = FlatStyle.Flat;
        exportLogButton.ForeColor = Color.FromArgb(58, 68, 55);
        exportLogButton.Location = new Point(285, 536);
        exportLogButton.Margin = new Padding(5);
        exportLogButton.Name = "exportLogButton";
        exportLogButton.Size = new Size(229, 58);
        exportLogButton.TabIndex = 2;
        exportLogButton.Text = "Export diagnostics";
        exportLogButton.UseVisualStyleBackColor = false;
        exportLogButton.Click += exportLogButton_Click;
        // 
        // openDataFolderButton
        // 
        openDataFolderButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        openDataFolderButton.BackColor = Color.FromArgb(239, 226, 198);
        openDataFolderButton.FlatAppearance.BorderSize = 0;
        openDataFolderButton.FlatStyle = FlatStyle.Flat;
        openDataFolderButton.ForeColor = Color.FromArgb(58, 68, 55);
        openDataFolderButton.Location = new Point(29, 536);
        openDataFolderButton.Margin = new Padding(5);
        openDataFolderButton.Name = "openDataFolderButton";
        openDataFolderButton.Size = new Size(236, 58);
        openDataFolderButton.TabIndex = 1;
        openDataFolderButton.Text = "Open data folder";
        openDataFolderButton.UseVisualStyleBackColor = false;
        openDataFolderButton.Click += openDataFolderButton_Click;
        // 
        // logTextBox
        // 
        logTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        logTextBox.BackColor = Color.FromArgb(35, 38, 34);
        logTextBox.BorderStyle = BorderStyle.FixedSingle;
        logTextBox.Font = new Font("Cascadia Mono", 9F);
        logTextBox.ForeColor = Color.FromArgb(234, 231, 216);
        logTextBox.Location = new Point(29, 29);
        logTextBox.Margin = new Padding(5);
        logTextBox.Multiline = true;
        logTextBox.Name = "logTextBox";
        logTextBox.ReadOnly = true;
        logTextBox.ScrollBars = ScrollBars.Vertical;
        logTextBox.Size = new Size(1341, 486);
        logTextBox.TabIndex = 0;
        // 
        // recentActivityCard
        // 
        recentActivityCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        recentActivityCard.BackColor = Color.FromArgb(255, 253, 247);
        recentActivityCard.Controls.Add(_latestActivityDetailLabel);
        recentActivityCard.Controls.Add(_latestActivitySummaryLabel);
        recentActivityCard.Controls.Add(_latestActivityDot);
        recentActivityCard.Controls.Add(_viewAllActivityButton);
        recentActivityCard.Controls.Add(recentActivityListView);
        recentActivityCard.Controls.Add(recentActivityTitleLabel);
        recentActivityCard.Location = new Point(26, 362);
        recentActivityCard.Name = "recentActivityCard";
        recentActivityCard.Size = new Size(856, 199);
        recentActivityCard.TabIndex = 3;
        // 
        // _latestActivityDetailLabel
        // 
        _latestActivityDetailLabel.Location = new Point(25, 68);
        _latestActivityDetailLabel.Name = "_latestActivityDetailLabel";
        _latestActivityDetailLabel.Size = new Size(650, 24);
        _latestActivityDetailLabel.TabIndex = 5;
        _latestActivityDetailLabel.Visible = false;
        // 
        // _latestActivitySummaryLabel
        // 
        _latestActivitySummaryLabel.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        _latestActivitySummaryLabel.ForeColor = Color.FromArgb(50, 58, 49);
        _latestActivitySummaryLabel.Location = new Point(25, 39);
        _latestActivitySummaryLabel.Name = "_latestActivitySummaryLabel";
        _latestActivitySummaryLabel.Size = new Size(650, 24);
        _latestActivitySummaryLabel.TabIndex = 3;
        _latestActivitySummaryLabel.Text = "Nothing has happened yet";
        // 
        // _latestActivityDot
        // 
        _latestActivityDot.BackColor = Color.FromArgb(104, 139, 108);
        _latestActivityDot.Location = new Point(25, 15);
        _latestActivityDot.Name = "_latestActivityDot";
        _latestActivityDot.Size = new Size(16, 16);
        _latestActivityDot.TabIndex = 2;
        // 
        // _viewAllActivityButton
        // 
        _viewAllActivityButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _viewAllActivityButton.AutoSize = true;
        _viewAllActivityButton.BackColor = Color.Transparent;
        _viewAllActivityButton.FlatAppearance.BorderSize = 0;
        _viewAllActivityButton.FlatStyle = FlatStyle.Flat;
        _viewAllActivityButton.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _viewAllActivityButton.ForeColor = Color.FromArgb(71, 103, 80);
        _viewAllActivityButton.Location = new Point(659, 14);
        _viewAllActivityButton.Name = "_viewAllActivityButton";
        _viewAllActivityButton.Size = new Size(171, 42);
        _viewAllActivityButton.TabIndex = 4;
        _viewAllActivityButton.Text = "Open Activity";
        _viewAllActivityButton.UseVisualStyleBackColor = false;
        // 
        // recentActivityListView
        // 
        recentActivityListView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        recentActivityListView.BackColor = Color.FromArgb(255, 253, 247);
        recentActivityListView.BorderStyle = BorderStyle.None;
        recentActivityListView.Columns.AddRange(new ColumnHeader[] { activityTypeColumnHeader, activityAmountColumnHeader, activityDateColumnHeader });
        recentActivityListView.Font = new Font("Segoe UI", 9.5F);
        recentActivityListView.FullRowSelect = true;
        recentActivityListView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        recentActivityListView.Location = new Point(24, 52);
        recentActivityListView.Name = "recentActivityListView";
        recentActivityListView.Size = new Size(806, 126);
        recentActivityListView.TabIndex = 1;
        recentActivityListView.UseCompatibleStateImageBehavior = false;
        recentActivityListView.View = View.Details;
        recentActivityListView.DoubleClick += recentActivityListView_DoubleClick;
        // 
        // activityTypeColumnHeader
        // 
        activityTypeColumnHeader.Text = "What happened";
        activityTypeColumnHeader.Width = 210;
        // 
        // activityAmountColumnHeader
        // 
        activityAmountColumnHeader.Text = "Amount";
        activityAmountColumnHeader.Width = 210;
        // 
        // activityDateColumnHeader
        // 
        activityDateColumnHeader.Text = "Date";
        activityDateColumnHeader.Width = 220;
        // 
        // recentActivityTitleLabel
        // 
        recentActivityTitleLabel.AutoSize = true;
        recentActivityTitleLabel.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        recentActivityTitleLabel.ForeColor = Color.FromArgb(99, 99, 83);
        recentActivityTitleLabel.Location = new Point(24, 18);
        recentActivityTitleLabel.Name = "recentActivityTitleLabel";
        recentActivityTitleLabel.Size = new Size(217, 41);
        recentActivityTitleLabel.TabIndex = 0;
        recentActivityTitleLabel.Text = "Recent activity";
        // 
        // _operationProgressPanel
        // 
        _operationProgressPanel.BackColor = Color.FromArgb(238, 231, 219);
        _operationProgressPanel.Controls.Add(_operationProgressLabel);
        _operationProgressPanel.Controls.Add(_operationProgressTrack);
        _operationProgressPanel.Dock = DockStyle.Bottom;
        _operationProgressPanel.Location = new Point(0, 962);
        _operationProgressPanel.Name = "_operationProgressPanel";
        _operationProgressPanel.Padding = new Padding(18, 7, 18, 7);
        _operationProgressPanel.Size = new Size(1495, 44);
        _operationProgressPanel.TabIndex = 2;
        _operationProgressPanel.Visible = false;
        // 
        // _operationProgressLabel
        // 
        _operationProgressLabel.Dock = DockStyle.Fill;
        _operationProgressLabel.Font = new Font("Arial Narrow", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        _operationProgressLabel.ForeColor = Color.FromArgb(67, 78, 62);
        _operationProgressLabel.Location = new Point(208, 7);
        _operationProgressLabel.Name = "_operationProgressLabel";
        _operationProgressLabel.Padding = new Padding(14, 0, 0, 0);
        _operationProgressLabel.Size = new Size(1269, 30);
        _operationProgressLabel.TabIndex = 1;
        _operationProgressLabel.Text = "Working...";
        _operationProgressLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _operationProgressTrack
        // 
        _operationProgressTrack.BackColor = Color.FromArgb(216, 207, 190);
        _operationProgressTrack.Controls.Add(_operationProgressFill);
        _operationProgressTrack.Dock = DockStyle.Left;
        _operationProgressTrack.Location = new Point(18, 7);
        _operationProgressTrack.Margin = new Padding(0, 5, 0, 5);
        _operationProgressTrack.Name = "_operationProgressTrack";
        _operationProgressTrack.Size = new Size(190, 30);
        _operationProgressTrack.TabIndex = 0;
        // 
        // _operationProgressFill
        // 
        _operationProgressFill.BackColor = Color.FromArgb(48, 68, 56);
        _operationProgressFill.Dock = DockStyle.Left;
        _operationProgressFill.Location = new Point(0, 0);
        _operationProgressFill.Name = "_operationProgressFill";
        _operationProgressFill.Size = new Size(0, 30);
        _operationProgressFill.TabIndex = 0;
        // 
        // _trayIcon
        // 
        _trayIcon.ContextMenuStrip = _trayMenu;
        _trayIcon.Text = "Slithy Tove";
        _trayIcon.Visible = true;
        _trayIcon.DoubleClick += trayIcon_DoubleClick;
        // 
        // _trayMenu
        // 
        _trayMenu.ImageScalingSize = new Size(32, 32);
        _trayMenu.Items.AddRange(new ToolStripItem[] { openSlithyToveToolStripMenuItem, trayToolStripSeparator, exitToolStripMenuItem });
        _trayMenu.Name = "_trayMenu";
        _trayMenu.Size = new Size(271, 86);
        // 
        // openSlithyToveToolStripMenuItem
        // 
        openSlithyToveToolStripMenuItem.Name = "openSlithyToveToolStripMenuItem";
        openSlithyToveToolStripMenuItem.Size = new Size(270, 38);
        openSlithyToveToolStripMenuItem.Text = "Open Slithy Tove";
        openSlithyToveToolStripMenuItem.Click += openSlithyToveToolStripMenuItem_Click;
        // 
        // trayToolStripSeparator
        // 
        trayToolStripSeparator.Name = "trayToolStripSeparator";
        trayToolStripSeparator.Size = new Size(267, 6);
        // 
        // exitToolStripMenuItem
        // 
        exitToolStripMenuItem.Name = "exitToolStripMenuItem";
        exitToolStripMenuItem.Size = new Size(270, 38);
        exitToolStripMenuItem.Text = "Exit";
        exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
        // 
        // statusTimer
        // 
        statusTimer.Enabled = true;
        statusTimer.Interval = 5000;
        statusTimer.Tick += statusTimer_Tick;
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF(13F, 32F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(247, 243, 235);
        ClientSize = new Size(1495, 1006);
        // Chain progress has its own row so wallet work cannot overwrite it.
        chainSyncPanel.Name = "chainSyncPanel";
        chainSyncPanel.Dock = DockStyle.Bottom;
        chainSyncPanel.Height = 96;
        chainSyncPanel.Padding = new Padding(16, 6, 16, 8);
        chainSyncPanel.BackColor = Color.FromArgb(239, 234, 222);
        chainSyncPanel.Visible = false;
        chainSyncLabel.Name = "chainSyncLabel";
        chainSyncLabel.Dock = DockStyle.Fill;
        chainSyncLabel.Font = new Font("Segoe UI", 10F);
        chainSyncLabel.ForeColor = Color.FromArgb(42, 48, 41);
        chainSyncLabel.Text = "Finding the latest blocks...";
        chainSyncProgressBar.Name = "chainSyncProgressBar";
        chainSyncProgressBar.Dock = DockStyle.Bottom;
        chainSyncProgressBar.Height = 12;
        chainSyncProgressBar.MarqueeAnimationSpeed = 30;
        chainSyncPanel.Controls.Add(chainSyncLabel);
        chainSyncPanel.Controls.Add(chainSyncProgressBar);
        Controls.Add(chainSyncPanel);
        Controls.Add(_operationProgressPanel);
        Controls.Add(mainTabControl);
        Controls.Add(headerPanel);
        Font = new Font("Segoe UI", 9F);
        Margin = new Padding(5);
        MaximumSize = new Size(1934, 1317);
        MinimumSize = new Size(1316, 1077);
        Name = "Form1";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Slithy Tove";
        Shown += Form1_Shown;
        headerPanel.ResumeLayout(false);
        headerPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)brandPictureBox).EndInit();
        mainTabControl.ResumeLayout(false);
        homeTabPage.ResumeLayout(false);
        _homeLayout.ResumeLayout(false);
        homeStatusCard.ResumeLayout(false);
        homeStatusCard.PerformLayout();
        balanceCard.ResumeLayout(false);
        balanceCard.PerformLayout();
        miningCard.ResumeLayout(false);
        miningCard.PerformLayout();
        _homeMissionCard.ResumeLayout(false);
        _homeMissionCard.PerformLayout();
        _walletActivityTab.ResumeLayout(false);
        _activityCard.ResumeLayout(false);
        _walletActivityHeaderPanel.ResumeLayout(false);
        _walletActivityHeaderPanel.PerformLayout();
        advancedTabPage.ResumeLayout(false);
        advancedTabControl.ResumeLayout(false);
        connectionTabPage.ResumeLayout(false);
        statusTableLayoutPanel.ResumeLayout(false);
        connectionGroupBox.ResumeLayout(false);
        connectionGroupBox.PerformLayout();
        updatesTabPage.ResumeLayout(false);
        updatesCard.ResumeLayout(false);
        updatesCard.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)autoLockMinutesNumericUpDown).EndInit();
        updateProgressTrackPanel.ResumeLayout(false);
        activityTabPage.ResumeLayout(false);
        activityTabPage.PerformLayout();
        recentActivityCard.ResumeLayout(false);
        recentActivityCard.PerformLayout();
        _operationProgressPanel.ResumeLayout(false);
        _operationProgressTrack.ResumeLayout(false);
        _trayMenu.ResumeLayout(false);
        ResumeLayout(false);
    }

}
