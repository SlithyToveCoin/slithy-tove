//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Transaction Details Dialog
//===============================================
namespace Slithy_Tove;

// Addresses come from the wallet transaction. Inputs are previous outputs, not sender identities.
internal partial class TransactionDetailsDialog : Form
{
    private readonly WalletRpcClient _client;
    private readonly Uri _endpoint;
    private readonly WalletTransfer _transfer;
    private readonly Label _confirmations = WalletDialogLayout.Note("");
    private readonly Label _detailStatus = WalletDialogLayout.Note("");
    private readonly ListView _outputs = new() { View = View.Details, FullRowSelect = true, Height = 160, HideSelection = false };

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
        Controls.Clear();
        headingLabel.Dispose(); hashLabel.Dispose();
        proofGroupBox.Dispose();
        var page = WalletDialogLayout.Page(this, "Transaction details", 850, 820);
        WalletDialogLayout.Add(page, detailsTable);
        WalletDialogLayout.Add(page, _confirmations);
        _confirmations.Text = ConfirmationText(transfer.Confirmations, transfer.Abandoned);
        string addressCaption = transfer.Direction is "Sent" or "Sending" ? "Sent to" : transfer.Direction == "Mined" ? "Reward address" : "Received at";
        WalletDialogLayout.Add(page, WalletDialogLayout.Note(addressCaption));
        var address = WalletDialogLayout.Readable(string.IsNullOrWhiteSpace(transfer.Address) ? "No standard address recorded for this entry." : transfer.Address);
        WalletDialogLayout.Add(page, address);
        var copyAddress = WalletDialogLayout.Button("Copy address", (_, _) => Clipboard.SetText(transfer.Address));
        copyAddress.Enabled = !string.IsNullOrWhiteSpace(transfer.Address);
        WalletDialogLayout.Add(page, copyAddress);
        transactionHashTextBox.Multiline = true; transactionHashTextBox.Height = 64;
        WalletDialogLayout.Add(page, WalletDialogLayout.Note("Transaction ID"));
        WalletDialogLayout.Add(page, transactionHashTextBox);
        var explorer = WalletDialogLayout.Button("Open recent-block explorer", (_, _) =>
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(transfer.TransactionHash, "\\A[0-9a-fA-F]{64}\\z")) return;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://slithy.io/explorer.html?tx=" + transfer.TransactionHash) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not open browser"); }
        });
        WalletDialogLayout.Add(page, WalletDialogLayout.Actions(copyHashButton, explorer));
        WalletDialogLayout.Add(page, WalletDialogLayout.Note("The website covers recent blocks. Older transactions may not appear. Opening it shares this transaction ID with slithy.io."));
        _outputs.Columns.Add("Output", 65); _outputs.Columns.Add("Amount (SLTHY)", 145); _outputs.Columns.Add("Address", 420); _outputs.Columns.Add("Owned here", 95);
        _outputs.AccessibleName = "Transaction outputs and addresses";
        _outputs.Font = new Font("Segoe UI", 9F);
        _outputs.ShowItemToolTips = true;
        _outputs.SizeChanged += (_, _) =>
        {
            _outputs.Columns[0].Width = TextRenderer.MeasureText("Output", _outputs.Font).Width + 20;
            _outputs.Columns[1].Width = TextRenderer.MeasureText("100,000.00000000", _outputs.Font).Width + 20;
            _outputs.Columns[3].Width = TextRenderer.MeasureText("Owned here", _outputs.Font).Width + 20;
            _outputs.Columns[2].Width = Math.Max(300, _outputs.ClientSize.Width - _outputs.Columns[0].Width - _outputs.Columns[1].Width - _outputs.Columns[3].Width - 30);
        };
        WalletDialogLayout.Add(page, _outputs);
        var selectedOutput = WalletDialogLayout.Readable("Select an output to read its full address.");
        _outputs.SelectedIndexChanged += (_, _) =>
        {
            if (_outputs.SelectedItems.Count == 1)
                selectedOutput.Text = _outputs.SelectedItems[0].SubItems[2].Text;
        };
        WalletDialogLayout.Add(page, selectedOutput);
        WalletDialogLayout.Add(page, WalletDialogLayout.Button("Copy selected output address", (_, _) =>
        {
            if (_outputs.SelectedItems.Count == 1 && _outputs.SelectedItems[0].Tag is string value && value.Length > 0) Clipboard.SetText(value);
        }));
        WalletDialogLayout.Add(page, _detailStatus);
        WalletDialogLayout.Add(page, closeButton);
        Shown += async (_, _) => await LoadDetailsAsync();
    }

    internal static string ConfirmationText(long count, bool abandoned) => abandoned ? "Abandoned" : count < 0 ? "Conflicted" : count == 0 ? "Pending confirmation" : $"{count:N0} confirmations";

    private async Task LoadDetailsAsync()
    {
        _detailStatus.Text = "Reading transaction from your wallet...";
        try
        {
            WalletTransactionDetails details = await _client.GetTransactionDetailsAsync(_endpoint, _transfer.TransactionHash);
            if (IsDisposed) return;
            _confirmations.Text = ConfirmationText(details.Confirmations, details.Abandoned);
            _outputs.Items.Clear();
            foreach (var output in details.Outputs)
            {
                var item = new ListViewItem(output.OutputIndex.ToString()) { Tag = output.Address, ToolTipText = output.Address };
                item.SubItems.Add(output.Amount.ToString("N8"));
                item.SubItems.Add(string.IsNullOrWhiteSpace(output.Address) ? "Script output without a standard address" : output.Address);
                item.SubItems.Add(output.IsMine ? "Yes" : "No");
                _outputs.Items.Add(item);
            }
            _detailStatus.Text = "Outputs show where the coins went. Inputs spend earlier outputs; they do not identify a sender.";
            if (_outputs.Items.Count > 0) _outputs.Items[0].Selected = true;
        }
        catch (Exception ex) { if (!IsDisposed) _detailStatus.Text = "Additional details are unavailable: " + ex.Message; }
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

