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
    private readonly WalletRpcClient? _client;
    private readonly Uri? _endpoint;
    private IReadOnlyList<WalletOutpoint>? _selectedInputs;
    private readonly CheckBox _consolidate = new() { Text = "Combine selected coins into a new address in this wallet", AutoSize = true };
    private readonly Label _selectionSummary = WalletDialogLayout.Note("Automatic coin selection");

    public SendDialog(decimal available, WalletRpcClient? client = null, Uri? endpoint = null)
    {
        InitializeComponent();
        _available = available;
        _client = client;
        _endpoint = endpoint;
        availableLabel.Text = $"Available: {available:N8} SLTHY";
        Controls.Clear();
        headingLabel.Dispose();
        var page = WalletDialogLayout.Page(this, "Send Slithy", 760, 720);
        addressTextBox.Height = 65;
        WalletDialogLayout.Add(page, addressLabel);
        WalletDialogLayout.Add(page, addressTextBox);
        WalletDialogLayout.Add(page, amountLabel);
        WalletDialogLayout.Add(page, amountNumericUpDown);
        WalletDialogLayout.Add(page, availableLabel);
        WalletDialogLayout.Add(page, WalletDialogLayout.Note("Advanced coin selection"));
        var choose = WalletDialogLayout.Button("Choose inputs...", ChooseInputs_Click);
        choose.Enabled = client is not null;
        WalletDialogLayout.Add(page, WalletDialogLayout.Actions(choose,
            WalletDialogLayout.Button("Use automatic selection", (_, _) => { _selectedInputs = null; _consolidate.Checked = false; _selectionSummary.Text = "Automatic coin selection"; })));
        WalletDialogLayout.Add(page, _selectionSummary);
        WalletDialogLayout.Add(page, _consolidate);
        _consolidate.Enabled = client is not null;
        _consolidate.CheckedChanged += (_, _) =>
        {
            addressTextBox.Enabled = amountNumericUpDown.Enabled = !_consolidate.Checked;
            continueButton.Text = _consolidate.Checked ? "Review consolidation" : "Review transfer";
        };
        WalletDialogLayout.Add(page, WalletDialogLayout.Note("Consolidation combines outputs you own. It costs a network fee and links those outputs publicly. The next screen shows the exact amount and fee before you decide to send."));
        safetyLabel.AutoSize = true;
        WalletDialogLayout.Add(page, safetyLabel);
        continueButton.Text = "Review transfer";
        continueButton.AutoSize = cancelButton.AutoSize = true;
        WalletDialogLayout.Add(page, WalletDialogLayout.Actions(continueButton, cancelButton));
    }

    public string DestinationAddress => addressTextBox.Text.Trim();
    public decimal Amount => amountNumericUpDown.Value;
    public IReadOnlyList<WalletOutpoint>? SelectedInputs => _selectedInputs;
    public bool IsConsolidation => _consolidate.Checked;

    private async void ChooseInputs_Click(object? sender, EventArgs e)
    {
        if (_client is null || _endpoint is null) return;
        var button = (Button)sender!;
        button.Enabled = continueButton.Enabled = false;
        try
        {
            var coins = await _client.GetSpendableOutputsAsync(_endpoint);
            if (IsDisposed) return;
            using var selection = new CoinControlDialog(coins, _selectedInputs);
            if (selection.ShowDialog(this) == DialogResult.OK)
            {
                _selectedInputs = selection.SelectedInputs;
                _selectionSummary.Text = $"{_selectedInputs.Count} inputs selected, {selection.SelectedAmount:N8} SLTHY before fees.";
            }
        }
        catch (Exception ex) { if (!IsDisposed) MessageBox.Show(this, ex.Message, "Could not load coins", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { if (!IsDisposed) button.Enabled = continueButton.Enabled = true; }
    }

    private void continueButton_Click(object? sender, EventArgs e)
    {
        if (IsConsolidation)
        {
            if (_selectedInputs?.Count is not (>= 2 and <= 200))
            {
                MessageBox.Show(this, "Choose between 2 and 200 inputs to combine.", "Choose coins", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
            return;
        }
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

