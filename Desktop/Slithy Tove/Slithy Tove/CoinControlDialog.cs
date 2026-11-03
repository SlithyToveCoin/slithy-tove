namespace Slithy_Tove;

internal sealed class CoinControlDialog : Form
{
    private readonly ListView _coins = new() { CheckBoxes = true, View = View.Details, FullRowSelect = true, Height = 300, HideSelection = false };
    private readonly Label _summary = WalletDialogLayout.Note("");
    internal IReadOnlyList<WalletOutpoint> SelectedInputs => _coins.CheckedItems.Cast<ListViewItem>().Select(item => ((WalletUnspentOutput)item.Tag!).Outpoint).ToArray();
    internal decimal SelectedAmount => _coins.CheckedItems.Cast<ListViewItem>().Sum(item => ((WalletUnspentOutput)item.Tag!).Amount);

    internal CoinControlDialog(IReadOnlyList<WalletUnspentOutput> coins, IReadOnlyList<WalletOutpoint>? selected)
    {
        var page = WalletDialogLayout.Page(this, "Choose coins to spend", 850, 630);
        WalletDialogLayout.Add(page, WalletDialogLayout.Note("Each row is an unspent output. Choose up to 200 per transaction. Mature, confirmed coins are shown. Nothing is sent from this window."));
        _coins.Columns.Add("Amount (SLTHY)", 145);
        _coins.Columns.Add("Confirmations", 110);
        _coins.Columns.Add("Receiving address", 250);
        _coins.Columns.Add("Transaction / output", 230);
        _coins.AccessibleName = "Available coin outputs";
        _coins.Font = new Font("Segoe UI", 9F);
        _coins.ShowItemToolTips = true;
        var chosen = selected?.ToHashSet() ?? [];
        foreach (var coin in coins.Where(coin => coin.Spendable && coin.Safe && coin.Confirmations > 0))
        {
            var item = new ListViewItem(coin.Amount.ToString("N8")) { Tag = coin, Checked = chosen.Contains(coin.Outpoint), ToolTipText = coin.Address + "\n" + coin.TransactionHash + ":" + coin.OutputIndex };
            item.SubItems.Add(coin.Confirmations.ToString("N0"));
            item.SubItems.Add(coin.Address);
            item.SubItems.Add(coin.TransactionHash + ":" + coin.OutputIndex);
            _coins.Items.Add(item);
        }
        _coins.ItemCheck += (_, e) =>
        {
            if (e.NewValue == CheckState.Checked && _coins.CheckedItems.Count >= 200) e.NewValue = CheckState.Unchecked;
            if (IsHandleCreated) BeginInvoke((Action)UpdateSummary);
        };
        WalletDialogLayout.Add(page, _coins);
        _coins.SizeChanged += (_, _) =>
        {
            _coins.Columns[0].Width = TextRenderer.MeasureText("100,000.00000000", _coins.Font).Width + 30;
            _coins.Columns[1].Width = TextRenderer.MeasureText("Confirmations", _coins.Font).Width + 20;
            _coins.Columns[2].Width = Math.Max(250, (_coins.ClientSize.Width - _coins.Columns[0].Width - _coins.Columns[1].Width) / 2);
            _coins.Columns[3].Width = Math.Max(300, _coins.ClientSize.Width - _coins.Columns[0].Width - _coins.Columns[1].Width - _coins.Columns[2].Width - 30);
        };
        var selectedDetails = WalletDialogLayout.Readable("Select a row to read its full address and previous transaction ID.", 85);
        _coins.SelectedIndexChanged += (_, _) =>
        {
            if (_coins.SelectedItems.Count == 1 && _coins.SelectedItems[0].Tag is WalletUnspentOutput coin)
                selectedDetails.Text = $"Address: {coin.Address}\r\nTransaction: {coin.TransactionHash}\r\nOutput: {coin.OutputIndex}";
        };
        WalletDialogLayout.Add(page, selectedDetails);
        WalletDialogLayout.Add(page, _summary);
        Button use = WalletDialogLayout.Button("Use selected coins", (_, _) =>
        {
            if (SelectedInputs.Count == 0) { _summary.Text = "Choose at least one output, or cancel to leave your choice unchanged."; return; }
            DialogResult = DialogResult.OK;
        });
        Button cancel = WalletDialogLayout.Button("Cancel"); cancel.DialogResult = DialogResult.Cancel;
        WalletDialogLayout.Add(page, WalletDialogLayout.Actions(
            WalletDialogLayout.Button("Select first 200", (_, _) => { foreach (ListViewItem item in _coins.Items) item.Checked = false; foreach (ListViewItem item in _coins.Items.Cast<ListViewItem>().Take(200)) item.Checked = true; UpdateSummary(); }),
            WalletDialogLayout.Button("Clear", (_, _) => { foreach (ListViewItem item in _coins.Items) item.Checked = false; UpdateSummary(); }), use, cancel));
        AcceptButton = use; CancelButton = cancel;
        UpdateSummary();
    }

    private void UpdateSummary() => _summary.Text = $"{SelectedInputs.Count} selected, {SelectedAmount:N8} SLTHY before fees.";
}
