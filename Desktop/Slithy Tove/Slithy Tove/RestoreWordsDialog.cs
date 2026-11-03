//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Restore Words Dialog
//===============================================
namespace Slithy_Tove;

// Collects a wallet name, password, and recovery words for a seed restore.
internal partial class RestoreWordsDialog : Form
{
    private readonly string _walletDirectory;
    private readonly IReadOnlyList<string>? _remoteWalletNames;
    private readonly NumericUpDown _recoveryRange = new()
    {
        Minimum = SlithySeedPhrase.DefaultRecoveryRangeEnd,
        Maximum = SlithySeedPhrase.MaximumRecoveryRangeEnd,
        Value = SlithySeedPhrase.DefaultRecoveryRangeEnd,
        Increment = 5000,
        ThousandsSeparator = true,
        AccessibleName = "Last address index to scan"
    };
    private readonly CheckBox _advancedRecovery = new() { Text = "Advanced recovery scan", AutoSize = true };

    public RestoreWordsDialog(string walletDirectory, IReadOnlyList<string>? remoteWalletNames)
    {
        InitializeComponent();
        _walletDirectory = walletDirectory;
        _remoteWalletNames = remoteWalletNames;
        Controls.Clear();
        headingLabel.Dispose();
        var page = WalletDialogLayout.Page(this, "Restore recovery words", 760, 760);
        descriptionLabel.Text = "Enter your words in order. Slithy scans the chain from the beginning to find this wallet's transactions. A large wallet can take several minutes to restore.";
        WalletDialogLayout.Add(page, descriptionLabel);
        foreach (Control control in new Control[] { walletNameLabel, walletNameTextBox, passwordLabel, passwordTextBox, confirmPasswordLabel, confirmPasswordTextBox, wordsLabel, wordsTextBox })
            WalletDialogLayout.Add(page, control);
        wordsTextBox.Height = 100;
        WalletDialogLayout.Add(page, _advancedRecovery);
        var advanced = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Visible = false };
        advanced.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        WalletDialogLayout.Add(advanced, WalletDialogLayout.Note("Last address index to scan"));
        WalletDialogLayout.Add(advanced, _recoveryRange);
        WalletDialogLayout.Add(advanced, WalletDialogLayout.Note("The usual range ends at index 10,000. Raise this if you created addresses beyond that point. Higher values take longer. This changes the address search range, not your recovery words or the first block scanned."));
        _advancedRecovery.CheckedChanged += (_, _) => advanced.Visible = _advancedRecovery.Checked;
        WalletDialogLayout.Add(page, advanced);
        WalletDialogLayout.Add(page, WalletDialogLayout.Actions(restoreButton, cancelButton));
        Disposed += (_, _) => { wordsTextBox.Clear(); passwordTextBox.Clear(); confirmPasswordTextBox.Clear(); };
    }

    public string WalletName => walletNameTextBox.Text.Trim();
    public string WalletPassword => passwordTextBox.Text;
    public string RecoveryWords => wordsTextBox.Text;
    public int RecoveryRangeEnd => _advancedRecovery.Checked ? (int)_recoveryRange.Value : SlithySeedPhrase.DefaultRecoveryRangeEnd;

    private void restoreButton_Click(object? sender, EventArgs e)
    {
        if (!ValidateWalletName(WalletName, out string error))
        {
            MessageBox.Show(this, error, "Choose another wallet name",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool walletExists = _remoteWalletNames is not null
            ? _remoteWalletNames.Contains(WalletName, StringComparer.OrdinalIgnoreCase)
            : Directory.Exists(Path.Combine(_walletDirectory, WalletName));
        if (walletExists)
        {
            MessageBox.Show(this, "A wallet with that name already exists.",
                "Wallet already exists", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (passwordTextBox.Text.Length < 8)
        {
            MessageBox.Show(this, "Use a password with at least 8 characters.",
                "Password required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (passwordTextBox.Text != confirmPasswordTextBox.Text)
        {
            MessageBox.Show(this, "The two passwords do not match.",
                "Passwords do not match", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _ = SlithySeedPhrase.NormalizeRecoveryWords(wordsTextBox.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Check recovery words",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
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
