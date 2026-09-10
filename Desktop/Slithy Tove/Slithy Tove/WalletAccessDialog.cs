//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Wallet Access Dialog
//===============================================
namespace Slithy_Tove;

// First wallet screen. It lets the user create, open, or restore a wallet backup.
internal partial class WalletAccessDialog : Form
{
    private readonly string _walletDirectory;
    private readonly IReadOnlyList<string>? _remoteWalletNames;
    private string _restoreSourceDirectory = "";
    private string _restoreWalletName = "";
    private string _restoreWords = "";
    private string _restoreWordsPassword = "";

    public WalletAccessDialog(
        string walletDirectory,
        string preferredWalletName = "",
        IReadOnlyList<string>? remoteWalletNames = null)
    {
        InitializeComponent();
        _walletDirectory = walletDirectory;
        _remoteWalletNames = remoteWalletNames;
        if (_remoteWalletNames is null)
        {
            Directory.CreateDirectory(walletDirectory);
        }
        RefreshWalletList(preferredWalletName);
        modeTabControl_SelectedIndexChanged(this, EventArgs.Empty);
    }

    public WalletAccessMode AccessMode { get; private set; } = WalletAccessMode.Open;
    public string WalletName => AccessMode switch
    {
        WalletAccessMode.Create => createNameTextBox.Text.Trim(),
        WalletAccessMode.RestoreBackup => _restoreWalletName,
        WalletAccessMode.RestoreWords => _restoreWalletName,
        _ => existingWalletComboBox.Text.Trim()
    };
    public string WalletPassword => AccessMode switch
    {
        WalletAccessMode.Create => createPasswordTextBox.Text,
        WalletAccessMode.RestoreWords => _restoreWordsPassword,
        _ => openPasswordTextBox.Text
    };
    public string RestoreSourceDirectory => _restoreSourceDirectory;
    public string RestoreRecoveryWords => _restoreWords;
    private void createButton_Click(object? sender, EventArgs e)
    {
        // Validate the wallet name and password before returning to Form1.
        if (!ValidateWalletName(createNameTextBox.Text, out string error))
        {
            MessageBox.Show(this, error, "Choose another wallet name",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (createPasswordTextBox.Text.Length < 8)
        {
            MessageBox.Show(this, "Use a password with at least 8 characters.",
                "Password required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (createPasswordTextBox.Text != confirmPasswordTextBox.Text)
        {
            MessageBox.Show(this, "The two passwords do not match.",
                "Passwords do not match", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        bool walletExists = _remoteWalletNames is not null
            ? _remoteWalletNames.Contains(createNameTextBox.Text.Trim(), StringComparer.OrdinalIgnoreCase)
            : File.Exists(Path.Combine(_walletDirectory, createNameTextBox.Text.Trim())) ||
              Directory.Exists(Path.Combine(_walletDirectory, createNameTextBox.Text.Trim()));
        if (walletExists)
        {
            MessageBox.Show(this, "A wallet with that name already exists.",
                "Wallet already exists", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        AccessMode = WalletAccessMode.Create;
        DialogResult = DialogResult.OK;
    }

    private void openButton_Click(object? sender, EventArgs e)
    {
        // User picked an existing wallet and entered its password.
        if (string.IsNullOrWhiteSpace(existingWalletComboBox.Text))
        {
            MessageBox.Show(this, "Choose a wallet to open.", "Choose a wallet",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        AccessMode = WalletAccessMode.Open;
        DialogResult = DialogResult.OK;
    }

    private void restoreButton_Click(object? sender, EventArgs e)
    {
        // Restore from the backup folder made by Settings > Back up open wallet.
        using FolderBrowserDialog dialog = new()
        {
            Description = "Choose the Slithy wallet backup folder",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(openPasswordTextBox.Text))
        {
            MessageBox.Show(this, "Enter the wallet password for this backup.",
                "Password required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!WalletBackupTools.TryFindBackupWalletDirectory(
                dialog.SelectedPath,
                out string walletDirectory,
                out string walletName))
        {
            MessageBox.Show(this,
                "That folder does not look like a Slithy wallet backup. Choose the folder that contains wallet.dat, or the backup folder created by Slithy.",
                "Wallet backup not found",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        if (!ValidateWalletName(walletName, out string error))
        {
            MessageBox.Show(this, error, "Choose another wallet backup",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        bool walletExists = _remoteWalletNames is not null
            ? _remoteWalletNames.Contains(walletName, StringComparer.OrdinalIgnoreCase)
            : Directory.Exists(Path.Combine(_walletDirectory, walletName));
        if (walletExists)
        {
            MessageBox.Show(this,
                "A wallet with that name already exists. Rename the backup folder before restoring it.",
                "Wallet already exists",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _restoreSourceDirectory = walletDirectory;
        _restoreWalletName = walletName;
        AccessMode = WalletAccessMode.RestoreBackup;
        DialogResult = DialogResult.OK;
    }

    private void restoreWordsButton_Click(object? sender, EventArgs e)
    {
        // Restore a deterministic wallet from the recovery words shown at creation.
        using RestoreWordsDialog dialog = new(_walletDirectory, _remoteWalletNames);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _restoreWalletName = dialog.WalletName;
        _restoreWordsPassword = dialog.WalletPassword;
        _restoreWords = SlithySeedPhrase.NormalizeRecoveryWords(dialog.RecoveryWords);
        AccessMode = WalletAccessMode.RestoreWords;
        DialogResult = DialogResult.OK;
    }

    private void modeTabControl_SelectedIndexChanged(object? sender, EventArgs e)
    {
        // Update the heading text when the user switches between Open and Create.
        bool creatingWallet = modeTabControl.SelectedTab == createTabPage;
        AcceptButton = creatingWallet ? createButton : openButton;
        if (creatingWallet)
        {
            headingLabel.Text = "Create a new wallet";
            descriptionLabel.Text =
                "Choose a name and password. Slithy will show recovery words next.";
        }
        else
        {
            headingLabel.Text = "Welcome back";
            descriptionLabel.Text =
                "Enter your wallet password to continue. Your password never leaves this computer.";
        }
    }

    private void RefreshWalletList(string preferredWalletName)
    {
        // Finds wallet folders in the wallet directory and fills the dropdown.
        string[] names = (_remoteWalletNames ?? Directory.EnumerateDirectories(_walletDirectory)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToArray();
        existingWalletComboBox.Items.Clear();
        existingWalletComboBox.Items.AddRange(names);
        if (names.Length == 0)
        {
            headingLabel.Text = "Create your wallet";
            descriptionLabel.Text =
                "Choose a name and password. Slithy will show recovery words next.";
            modeTabControl.SelectedTab = createTabPage;
            Shown += (_, _) => createNameTextBox.Focus();
            return;
        }

        headingLabel.Text = "Welcome back";
        descriptionLabel.Text =
            "Enter your wallet password to continue. Your password never leaves this computer.";
        int preferredIndex = Array.FindIndex(
            names,
            name => name.Equals(preferredWalletName, StringComparison.OrdinalIgnoreCase));
        existingWalletComboBox.SelectedIndex = preferredIndex >= 0 ? preferredIndex : 0;
        modeTabControl.SelectedTab = openTabPage;
        Shown += (_, _) => openPasswordTextBox.Focus();
    }

    private static bool ValidateWalletName(string name, out string error)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 50)
        {
            error = "Wallet names must be between 1 and 50 characters.";
            return false;
        }
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.Contains('/') || name.Contains('\\'))
        {
            error = "Wallet names cannot contain path or special filename characters.";
            return false;
        }
        error = "";
        return true;
    }

}

internal enum WalletAccessMode
{
    // Form1 reads this to know which wallet action the user chose.
    Open,
    Create,
    RestoreBackup,
    RestoreWords
}

