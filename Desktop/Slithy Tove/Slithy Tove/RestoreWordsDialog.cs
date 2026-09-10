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

    public RestoreWordsDialog(string walletDirectory, IReadOnlyList<string>? remoteWalletNames)
    {
        InitializeComponent();
        _walletDirectory = walletDirectory;
        _remoteWalletNames = remoteWalletNames;
    }

    public string WalletName => walletNameTextBox.Text.Trim();
    public string WalletPassword => passwordTextBox.Text;
    public string RecoveryWords => wordsTextBox.Text;

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
