//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Send Dialog
//===============================================
namespace Slithy_Tove;

// Dialog for entering where to send Slithy and how much to send.
internal partial class SendDialog : Form
{
    private readonly decimal _available;

    public SendDialog(decimal available)
    {
        InitializeComponent();
        _available = available;
        availableLabel.Text = $"Available: {available:N8} SLTHY";
    }

    public string DestinationAddress => addressTextBox.Text.Trim();
    public decimal Amount => amountNumericUpDown.Value;

    private void continueButton_Click(object? sender, EventArgs e)
    {
        if (DestinationAddress.Length < 30)
        {
            MessageBox.Show(this, "Enter a complete Slithy address.",
                "Address needed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (Amount <= 0 || Amount > _available)
        {
            MessageBox.Show(this, "Enter an amount that is available to spend.",
                "Check the amount", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        DialogResult = DialogResult.OK;
    }
}

