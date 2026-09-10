//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Transaction Details Dialog
//===============================================
namespace Slithy_Tove;

// Shows one wallet activity item and, when supported, can create a payment proof.
internal partial class TransactionDetailsDialog : Form
{
    private readonly WalletRpcClient _client;
    private readonly Uri _endpoint;
    private readonly WalletTransfer _transfer;

    public TransactionDetailsDialog(
        WalletRpcClient client,
        Uri endpoint,
        WalletTransfer transfer)
    {
        InitializeComponent();
        _client = client;
        _endpoint = endpoint;
        _transfer = transfer;
        directionValueLabel.Text = transfer.Direction;
        amountValueLabel.Text = $"{transfer.Amount:N8} SLTHY";
        feeValueLabel.Text = $"{transfer.Fee:N8} SLTHY";
        heightValueLabel.Text = transfer.Height > 0 ? transfer.Height.ToString("N0") : "Pending";
        dateValueLabel.Text = transfer.Timestamp?.LocalDateTime.ToString("F") ?? "Pending";
        transactionHashTextBox.Text = transfer.TransactionHash;
        proofGroupBox.Enabled = false;
        proofGroupBox.Text = "Payment proofs are not available in this release";
    }

    private async void createProofButton_Click(object? sender, EventArgs e)
    {
        string address = recipientAddressTextBox.Text.Trim();
        if (address.Length < 30)
        {
            MessageBox.Show(this, "Enter the recipient address used for this transfer.",
                "Recipient address needed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            createProofButton.Enabled = false;
            proofTextBox.Text = await _client.GetTransactionProofAsync(
                _endpoint, _transfer.TransactionHash, address, proofMessageTextBox.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not create proof",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            createProofButton.Enabled = true;
        }
    }

    private void copyHashButton_Click(object? sender, EventArgs e) =>
        Clipboard.SetText(_transfer.TransactionHash);

    private void copyProofButton_Click(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(proofTextBox.Text))
        {
            Clipboard.SetText(proofTextBox.Text);
        }
    }
}

