//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Recovery Words Dialog
//===============================================
namespace Slithy_Tove;

// Shows the words for a new wallet and makes the user confirm they saved them.
internal partial class RecoveryWordsDialog : Form
{
    public RecoveryWordsDialog(string walletName, string recoveryWords)
    {
        InitializeComponent();
        walletNameLabel.Text = walletName;
        recoveryWordsTextBox.Text = recoveryWords;
    }

    private void copyButton_Click(object? sender, EventArgs e)
    {
        Clipboard.SetText(recoveryWordsTextBox.Text);
        copyButton.Text = "Copied";
    }

    private void savedCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        continueButton.Enabled = savedCheckBox.Checked;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !savedCheckBox.Checked)
        {
            e.Cancel = true;
            MessageBox.Show(this, "Write down your recovery words and confirm they are saved before continuing.", "Save your recovery words");
            return;
        }
        base.OnFormClosing(e);
    }
}
