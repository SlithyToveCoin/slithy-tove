using System.Text;

namespace Slithy_Tove;

public partial class Form1
{
    private readonly TextBox _historySearch = new() { Width = 240, PlaceholderText = "Address, transaction ID or text", AccessibleName = "Search wallet history" };
    private readonly ComboBox _historyDirection = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, AccessibleName = "Transaction direction" };
    private readonly ComboBox _historyStatus = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 145, AccessibleName = "Confirmation status" };
    private readonly Button _historyPrevious = WalletDialogLayout.Button("Previous");
    private readonly Button _historyNext = WalletDialogLayout.Button("Next");
    private readonly Label _historyPageLabel = WalletDialogLayout.Note("Open a wallet to view its history.");
    private CancellationTokenSource? _historyRequest;
    private int _historyPage;
    private bool _historyLoading;
    private bool _historyExporting;
    private DateTime _lastHistoryRefreshUtc;

    private void InitializeWalletHistory()
    {
        _walletActivityHelpLabel.Text = "Search transactions, then select View details.";
        void LayoutHeader()
        {
            _walletActivityHelpLabel.SetBounds(10, _walletActivityHeadingLabel.Bottom + 2,
                Math.Max(100, _walletActivityHeaderPanel.ClientSize.Width - 20), _walletActivityHelpLabel.Font.Height + 6);
            _walletActivityHeaderPanel.Height = _walletActivityHelpLabel.Bottom + 10;
        }
        _walletActivityHeaderPanel.SizeChanged += (_, _) => LayoutHeader();
        _walletActivityHeadingLabel.SizeChanged += (_, _) => LayoutHeader();
        LayoutHeader();
        _walletActivityListView.Font = new Font("Segoe UI", 9F);
        _historySearch.Width = TextRenderer.MeasureText("Address or transaction ID", _historySearch.Font).Width + 20;
        _historyDirection.Width = TextRenderer.MeasureText("To this wallet", _historyDirection.Font).Width + 40;
        _historyStatus.Width = TextRenderer.MeasureText("Confirmed", _historyStatus.Font).Width + 40;
        _walletActivityListView.Columns.Clear();
        foreach (string heading in new[] { "Type", "Amount (SLTHY)", "Date", "Confirmations", "Sent to / received at" })
            _walletActivityListView.Columns.Add(heading);
        _walletActivityListView.ShowItemToolTips = true;
        _historyDirection.Items.AddRange(["All activity", "Received", "Sent", "Mining", "To this wallet"]);
        _historyStatus.Items.AddRange(["Any status", "Confirmed", "Pending", "Conflicted"]);
        _historyDirection.SelectedIndex = _historyStatus.SelectedIndex = 0;
        var search = WalletDialogLayout.Button("Search", async (_, _) => await LoadWalletHistoryAsync(0));
        var refresh = WalletDialogLayout.Button("Refresh", async (_, _) => await LoadWalletHistoryAsync(_historyPage));
        var export = WalletDialogLayout.Button("Export CSV...", async (_, _) => await ExportWalletHistoryAsync());
        var details = WalletDialogLayout.Button("View details", (_, _) => OpenSelectedActivity(_walletActivityListView));
        _historyPrevious.Enabled = _historyNext.Enabled = false;
        _historyPrevious.Click += async (_, _) => await LoadWalletHistoryAsync(Math.Max(0, _historyPage - 1));
        _historyNext.Click += async (_, _) => await LoadWalletHistoryAsync(_historyPage + 1);
        _historySearch.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            await LoadWalletHistoryAsync(0);
        };
        var toolbar = WalletDialogLayout.Actions(_historySearch, _historyDirection, _historyStatus, search, refresh, export, details);
        toolbar.Dock = DockStyle.Fill;
        var navigation = WalletDialogLayout.Actions(_historyPrevious, _historyNext, _historyPageLabel);
        navigation.Dock = DockStyle.Fill;
        var rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rows.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var listArea = new Panel { Dock = DockStyle.Fill };
        listArea.Controls.Add(_walletActivityListView);
        listArea.Controls.Add(_activityEmptyLabel);
        rows.Controls.Add(toolbar, 0, 0);
        rows.Controls.Add(listArea, 0, 1);
        rows.Controls.Add(navigation, 0, 2);
        _activityCard.Controls.Add(rows);
        mainTabControl.SelectedIndexChanged += async (_, _) =>
        {
            if (mainTabControl.SelectedTab == _walletActivityTab) await LoadWalletHistoryAsync(0);
        };
        Disposed += (_, _) => { _historyRequest?.Cancel(); _historyRequest?.Dispose(); };
    }

    private string HistoryDirection => new[] { "all", "in", "out", "mining", "self" }[Math.Max(0, _historyDirection.SelectedIndex)];
    private string HistoryStatus => new[] { "all", "confirmed", "pending", "conflicted" }[Math.Max(0, _historyStatus.SelectedIndex)];

    private async Task LoadWalletHistoryAsync(int page)
    {
        if (_walletSnapshot is null || IsDisposed) return;
        _historyRequest?.Cancel();
        using var request = new CancellationTokenSource();
        _historyRequest = request;
        string wallet = _openWalletName;
        _historyLoading = true;
        _lastHistoryRefreshUtc = DateTime.UtcNow;
        _historyPrevious.Enabled = _historyNext.Enabled = false;
        _historyPageLabel.Text = "Reading wallet history...";
        try
        {
            WalletHistoryPage result = await _walletRpcClient.GetHistoryPageAsync(WalletRpcUri, page, 50,
                _historySearch.Text.Trim(), HistoryDirection, HistoryStatus, request.Token);
            if (request.IsCancellationRequested || IsDisposed || wallet != _openWalletName) return;
            _historyPage = result.Page;
            PopulateHistoryPage(result.Entries);
            _historyPrevious.Enabled = page > 0;
            _historyNext.Enabled = result.HasMore;
            _historyPageLabel.Text = $"Page {page + 1} | {result.Entries.Count} entries shown";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!IsDisposed && !request.IsCancellationRequested) _historyPageLabel.Text = "History could not be read: " + FriendlyWalletError(ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_historyRequest, request)) { _historyLoading = false; _historyRequest = null; }
        }
    }

    private void PopulateHistoryPage(IReadOnlyList<WalletTransfer> entries)
    {
        _walletActivityListView.BeginUpdate();
        _walletActivityListView.Items.Clear();
        foreach (WalletTransfer transfer in entries)
        {
            var item = new ListViewItem(transfer.Direction) { Tag = transfer, ToolTipText = transfer.Address + "\n" + transfer.TransactionHash };
            item.SubItems.Add((transfer.Direction is "Sent" or "Sending" ? "-" : "+") + transfer.Amount.ToString("N8"));
            item.SubItems.Add(transfer.Timestamp?.LocalDateTime.ToString("g") ?? "Unknown");
            item.SubItems.Add(transfer.Confirmations > 0 && !transfer.Abandoned ? transfer.Confirmations.ToString("N0") : TransactionDetailsDialog.ConfirmationText(transfer.Confirmations, transfer.Abandoned));
            item.SubItems.Add(string.IsNullOrWhiteSpace(transfer.Address) ? "See transaction details" : transfer.Address);
            _walletActivityListView.Items.Add(item);
        }
        _walletActivityListView.EndUpdate();
        _activityEmptyLabel.Text = "No entries match this search.";
        _activityEmptyLabel.Visible = entries.Count == 0;
        _walletActivityListView.Visible = entries.Count > 0;
        ResizeActivityColumns();
    }

    private async Task ExportWalletHistoryAsync()
    {
        if (_walletSnapshot is null || _historyLoading || _historyExporting) return;
        string wallet = _openWalletName;
        bool previous = _walletDialogOpen;
        _walletDialogOpen = true;
        _historyExporting = true;
        try
        {
            if (MessageBox.Show(this, "The CSV contains wallet addresses, transaction IDs and amounts. Anyone with the file can read them. Export every entry matching your current search?", "Export wallet history", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
            using var save = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = "slithy-wallet-history.csv", OverwritePrompt = true };
            if (save.ShowDialog(this) != DialogResult.OK) return;
            _historyPageLabel.Text = "Preparing all matching entries...";
            string csv = await _walletRpcClient.ExportHistoryCsvAsync(WalletRpcUri, _historySearch.Text.Trim(), HistoryDirection, HistoryStatus);
            if (IsDisposed || wallet != _openWalletName) return;
            await File.WriteAllTextAsync(save.FileName, csv, new UTF8Encoding(true));
            if (!IsDisposed) _historyPageLabel.Text = "Export saved. It includes every matching entry.";
        }
        catch (Exception ex) { if (!IsDisposed) _historyPageLabel.Text = "Export failed: " + FriendlyWalletError(ex.Message); }
        finally { _walletDialogOpen = previous; _historyExporting = false; }
    }

    private void ResetWalletHistory()
    {
        _historyRequest?.Cancel();
        _historyPage = 0;
        _historySearch.Clear();
        _historyPrevious.Enabled = _historyNext.Enabled = false;
        _historyPageLabel.Text = "Open a wallet to view its history.";
        _activityEmptyLabel.Text = "Open a wallet to view its history.";
    }

    private void RefreshVisibleHistory()
    {
        if (mainTabControl.SelectedTab == _walletActivityTab && !_historyLoading && !_walletDialogOpen &&
            _historyPage == 0 && DateTime.UtcNow - _lastHistoryRefreshUtc > TimeSpan.FromSeconds(30))
            _ = LoadWalletHistoryAsync(0);
    }

    private DialogResult ShowWalletDialog(Form dialog)
    {
        bool previous = _walletDialogOpen;
        _walletDialogOpen = true;
        try { return dialog.ShowDialog(this); }
        finally { _walletDialogOpen = previous; }
    }

    internal static string PreparedTransferConfirmation(PreparedTransfer prepared)
    {
        decimal amount = prepared.AmountAtomic / 100_000_000m;
        decimal fee = prepared.FeeAtomic / 100_000_000m;
        if (prepared.IsConsolidation)
            return $"Combine {prepared.InputCount} selected inputs into your own new address?\n\n{prepared.RecipientAddress}\n\nAmount after fee: {amount:N8} SLTHY\nNetwork fee: {fee:N8} SLTHY\n\nThis publicly links the selected outputs. Nothing has been sent yet.";
        string privacyNote = prepared.ReusesChangeAddress
            ? "\n\nChange returns to an existing wallet address because this wallet reached its recovery window. Reusing an address reduces privacy."
            : "";
        return $"Send {amount:N8} SLTHY to\n{prepared.RecipientAddress}?\n\nNetwork fee: {fee:N8} SLTHY\nTotal: {amount + fee:N8} SLTHY\nInputs: {prepared.InputCount}" + privacyNote;
    }
}
