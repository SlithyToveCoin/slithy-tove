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
    Restore,
    Lock,
    OpenOther
}
