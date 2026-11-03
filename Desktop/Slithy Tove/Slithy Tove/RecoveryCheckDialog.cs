namespace Slithy_Tove;

internal sealed class RecoveryCheckDialog : Form
{
    private readonly TextBox _words = new() { Multiline = true, Height = 100, AccessibleName = "Recovery words", UseSystemPasswordChar = false };
    private readonly Label _status = WalletDialogLayout.Note("");

    internal RecoveryCheckDialog(WalletRpcClient client, Uri endpoint)
    {
        var page = WalletDialogLayout.Page(this, "Check your recovery words", 720, 480);
        WalletDialogLayout.Add(page, WalletDialogLayout.Note("Enter the words from your written backup in their original order. This checks them against the open wallet on this computer. They are not saved in settings or sent to the website."));
        WalletDialogLayout.Add(page, _words);
        var check = WalletDialogLayout.Button("Check backup");
        check.Click += async (_, _) =>
        {
            check.Enabled = _words.Enabled = false;
            _status.Text = "Checking this wallet...";
            try
            {
                bool match = await client.CheckRecoveryWordsAsync(endpoint, _words.Text);
                if (!IsDisposed) _status.Text = match
                    ? "These words match this wallet's recovery addresses. Back up the wallet file too if you have imported other keys."
                    : "These words do not match this wallet's recovery addresses. Check the backup you picked.";
            }
            catch { if (!IsDisposed) _status.Text = "The words could not be checked. Check the spelling and word order, and make sure the local node is running."; }
            finally { if (!IsDisposed) { _words.Clear(); check.Enabled = _words.Enabled = true; } }
        };
        var close = WalletDialogLayout.Button("Close"); close.DialogResult = DialogResult.Cancel;
        WalletDialogLayout.Add(page, _status);
        WalletDialogLayout.Add(page, WalletDialogLayout.Actions(check, close));
        FormClosing += (_, _) => _words.Clear();
        Disposed += (_, _) => _words.Clear();
        CancelButton = close;
    }
}
