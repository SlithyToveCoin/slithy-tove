//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Wallet Manage Dialog
//===============================================
namespace Slithy_Tove;

// Small wallet menu shown from the Home tab Manage button.
internal partial class WalletManageDialog : Form
{
    public WalletManageDialog(string walletName)
    {
        InitializeComponent();
        walletNameLabel.Text = string.IsNullOrWhiteSpace(walletName)
            ? "No wallet open"
            : walletName;
        Controls.Clear();
        var page = WalletDialogLayout.Page(this, "Manage wallet", 650, 600);
        WalletDialogLayout.Add(page, walletNameLabel);
        WalletDialogLayout.Add(page, WalletDialogLayout.Note("Save a file backup or check the recovery words you wrote down."));
        WalletDialogLayout.Add(page, backupButton);
        WalletDialogLayout.Add(page, WalletDialogLayout.Button("Check recovery words", (_, _) =>
        {
            SelectedAction = WalletManageAction.CheckRecoveryWords;
            DialogResult = DialogResult.OK;
        }));
        WalletDialogLayout.Add(page, restoreButton);
        WalletDialogLayout.Add(page, WalletDialogLayout.Actions(lockButton, openOtherButton, closeButton));
        headingLabel.Dispose(); descriptionLabel.Dispose();
    }

    public WalletManageAction SelectedAction { get; private set; } = WalletManageAction.None;

    private void backupButton_Click(object? sender, EventArgs e)
    {
        SelectedAction = WalletManageAction.Backup;
        DialogResult = DialogResult.OK;
    }

    private void restoreButton_Click(object? sender, EventArgs e)
    {
        SelectedAction = WalletManageAction.Restore;
        DialogResult = DialogResult.OK;
    }

    private void lockButton_Click(object? sender, EventArgs e)
    {
        SelectedAction = WalletManageAction.Lock;
        DialogResult = DialogResult.OK;
    }

    private void openOtherButton_Click(object? sender, EventArgs e)
    {
        SelectedAction = WalletManageAction.OpenOther;
        DialogResult = DialogResult.OK;
    }
}

internal enum WalletManageAction
{
    None,
    Backup,
    CheckRecoveryWords,
    Restore,
    Lock,
    OpenOther
}
