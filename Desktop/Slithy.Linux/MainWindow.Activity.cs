using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Slithy_Tove;

namespace Slithy.Linux;

internal sealed partial class MainWindow
{
    private const int HistoryPageSize = 25;
    private readonly TextBox _historySearch = new() { Name = "HistorySearch", Watermark = "Search address, transaction ID or label", MinWidth = 180 };
    private readonly ComboBox _historyDirection = new() { Name = "HistoryDirection", ItemsSource = new[] { "All types", "Received", "Sent", "Mining", "To yourself" }, SelectedIndex = 0, MinWidth = 130 };
    private readonly ComboBox _historyStatus = new() { Name = "HistoryStatus", ItemsSource = new[] { "Any status", "Confirmed", "Pending", "Conflicted" }, SelectedIndex = 0, MinWidth = 130 };
    private readonly StackPanel _historyRows = new() { Spacing = 8 };
    private readonly Button _historyFind = Dialogs.Button("Search");
    private readonly Button _historyPrevious = Dialogs.Button("Previous");
    private readonly Button _historyNext = Dialogs.Button("Next");
    private readonly Button _historyExport = Dialogs.Button("Export history");
    private readonly Button _historyCancel = Dialogs.Button("Cancel");
    private readonly TextBlock _historyPageLabel = Dialogs.Text("");
    private bool _historyBusy, _historyHasMore;
    private int _historyPage;
    private string _historyWallet = "", _historyQuery = "", _historyType = "all", _historyState = "all";
    private DateTimeOffset _historyUpdated;
    private CancellationTokenSource? _historyCancellation;

    private Control BuildActivity()
    {
        _historyFind.Name = "HistoryFind"; _historyExport.Name = "HistoryExport";
        _historyPrevious.Name = "HistoryPrevious"; _historyNext.Name = "HistoryNext";
        _historyCancel.Name = "HistoryCancel"; _historyCancel.IsVisible = false;
        _historyPageLabel.Name = "HistoryPage";
        var search = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        search.Children.Add(_historySearch); Grid.SetColumn(_historyFind, 1); search.Children.Add(_historyFind);
        var top = Card("Wallet activity", search, Row(_historyDirection, _historyStatus, _historyExport, _historyCancel), _activity);
        top.Margin = new Thickness(0, 0, 0, 12);
        var page = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(top, Dock.Top); page.Children.Add(top);
        var navigation = Row(_historyPrevious, _historyPageLabel, _historyNext);
        navigation.Margin = new Thickness(0, 10, 0, 0);
        DockPanel.SetDock(navigation, Dock.Bottom); page.Children.Add(navigation);
        page.Children.Add(new ScrollViewer { Content = _historyRows });
        _historyFind.Click += async (_, _) => await SearchHistory();
        _historySearch.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await SearchHistory(); } };
        _historyPrevious.Click += async (_, _) => await LoadHistoryPage(Math.Max(0, _historyPage - 1));
        _historyNext.Click += async (_, _) => await LoadHistoryPage(_historyPage + 1);
        _historyExport.Click += async (_, _) => await ExportHistory();
        _historyCancel.Click += (_, _) => _historyCancellation?.Cancel();
        UpdateHistoryControls();
        return page;
    }

    private async Task SearchHistory()
    {
        if (_historyBusy || _busy) return;
        _historyQuery = _historySearch.Text?.Trim() ?? "";
        _historyType = _historyDirection.SelectedIndex switch { 1 => "in", 2 => "out", 3 => "mining", 4 => "self", _ => "all" };
        _historyState = _historyStatus.SelectedIndex switch { 1 => "confirmed", 2 => "pending", 3 => "conflicted", _ => "all" };
        _historyRows.Children.Clear(); _historyPage = 0; _historyHasMore = false; _historyPageLabel.Text = "";
        await LoadHistoryPage(0);
    }

    private void ResetHistory()
    {
        _historyCancellation?.Cancel();
        _historyRows.Children.Clear(); _historyPage = 0; _historyHasMore = false;
        _historyWallet = ""; _historyUpdated = default;
        _historyQuery = ""; _historyType = "all"; _historyState = "all";
        _historySearch.Text = ""; _historyDirection.SelectedIndex = 0; _historyStatus.SelectedIndex = 0;
        _historyPageLabel.Text = ""; _activity.Text = "Open a wallet to see transactions.";
        UpdateHistoryControls();
    }

    private async Task RefreshHistoryIfNeeded()
    {
        if (_historyBusy || _walletName.Length == 0) return;
        bool changedWallet = _historyWallet != _walletName;
        // A deep filtered search should not run again on every balance poll.
        if (!changedWallet && (_historyPage != 0 || _historyQuery.Length > 0 || _historyType != "all" || _historyState != "all")) return;
        if (!changedWallet && DateTimeOffset.UtcNow - _historyUpdated < TimeSpan.FromSeconds(30)) return;
        await LoadHistoryPage(changedWallet ? 0 : _historyPage, duringRefresh: true);
    }

    private async Task LoadHistoryPage(int page, bool duringRefresh = false)
    {
        if (_historyBusy || _closing || _walletName.Length == 0 || (!duringRefresh && _busy)) return;
        _historyBusy = true;
        _historyCancellation = new CancellationTokenSource();
        string walletName = _walletName;
        _activity.Text = "Reading wallet history...";
        UpdateControls();
        try
        {
            WalletHistoryPage result = _preview ? PreviewHistory(page) : await _wallet.GetHistoryPageAsync(
                _endpoint!, page, HistoryPageSize, _historyQuery, _historyType, _historyState, _historyCancellation.Token);
            if (_closing || walletName != _walletName) return;
            _historyWallet = walletName; _historyPage = result.Page; _historyHasMore = result.HasMore;
            _historyUpdated = DateTimeOffset.UtcNow;
            RenderHistory(result.Entries);
            _historyPageLabel.Text = "Page " + (result.Page + 1).ToString(CultureInfo.CurrentCulture);
            _activity.Text = result.Entries.Count == 0 ? "No matching transactions." :
                $"{result.Entries.Count} entries shown. Select one for its full details.";
        }
        catch (OperationCanceledException) { if (!_closing) _activity.Text = "History request cancelled."; }
        catch (Exception ex) { if (!_closing) _activity.Text = FriendlyError(ex); }
        finally
        {
            _historyCancellation.Dispose(); _historyCancellation = null;
            _historyBusy = false; UpdateControls();
        }
    }

    private void RenderHistory(IReadOnlyList<WalletTransfer> entries)
    {
        _historyRows.Children.Clear();
        int index = 0;
        foreach (var item in entries)
        {
            var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
            heading.Children.Add(Dialogs.Text(item.Direction + " | " + (item.Timestamp?.ToLocalTime().ToString("g") ?? "Time unavailable")));
            var amount = Dialogs.Text(item.Amount.ToString("N8") + " SLTHY", 16);
            Grid.SetColumn(amount, 1); heading.Children.Add(amount);
            var body = new StackPanel { Spacing = 5 };
            body.Children.Add(heading);
            body.Children.Add(Dialogs.Text(HistoryConfirmation(item), 12));
            var address = Dialogs.Text(string.IsNullOrEmpty(item.Address) ? "Open for output addresses" : item.Address, 12);
            address.TextWrapping = TextWrapping.NoWrap; address.TextTrimming = TextTrimming.CharacterEllipsis;
            ToolTip.SetTip(address, item.Address); body.Children.Add(address);
            var txid = Dialogs.Text(item.TransactionHash, 12);
            txid.TextWrapping = TextWrapping.NoWrap; txid.TextTrimming = TextTrimming.CharacterEllipsis;
            body.Children.Add(txid); body.IsHitTestVisible = false;
            var button = Dialogs.Button(""); button.Name = "HistoryTransaction" + index++;
            button.Content = body; button.Background = Dialogs.Paper;
            button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Click += async (_, _) => await ShowTransaction(item.TransactionHash);
            _historyRows.Children.Add(button);
        }
    }

    internal static string HistoryConfirmation(WalletTransfer item) => item.Abandoned ? "Abandoned" :
        item.Confirmations < 0 ? "Conflicted" : item.Confirmations == 0 ? "Waiting for confirmation" :
        item.Category == "immature" ? $"Mining reward | {item.Confirmations} confirmations, not spendable yet" :
        $"{item.Confirmations} confirmation{(item.Confirmations == 1 ? "" : "s")}";

    private async Task ShowTransaction(string transactionHash)
    {
        if (_historyBusy || _busy || _closing) return;
        _historyBusy = true; _historyCancellation = new CancellationTokenSource();
        string previousStatus = _activity.Text ?? "";
        _activity.Text = "Reading transaction details..."; UpdateControls();
        try
        {
            var details = _preview ? PreviewTransaction(transactionHash) : await _wallet.GetTransactionDetailsAsync(_endpoint!, transactionHash, _historyCancellation.Token);
            _historyCancellation.Token.ThrowIfCancellationRequested();
            _historyCancellation.Dispose(); _historyCancellation = null; UpdateHistoryControls();
            if (_closing) return;
            await Dialogs.Transaction(this, details);
            _activity.Text = previousStatus;
        }
        catch (OperationCanceledException) { _activity.Text = "Transaction request cancelled."; }
        catch (Exception ex)
        {
            if (!_closing) { _activity.Text = FriendlyError(ex); await Dialogs.Message(this, "Transaction unavailable", _activity.Text); }
        }
        finally
        {
            _historyCancellation?.Dispose(); _historyCancellation = null;
            _historyBusy = false; UpdateControls();
        }
    }

    private async Task ExportHistory()
    {
        if (_preview || _historyBusy || _busy || _walletName.Length == 0) return;
        var selected = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export matching wallet history", SuggestedFileName = _walletName + "-history.csv",
            FileTypeChoices = new[] { new FilePickerFileType("CSV spreadsheet") { Patterns = new[] { "*.csv" } } }
        });
        string? path = selected?.TryGetLocalPath(); if (path == null) return;
        _historyBusy = true; _historyCancellation = new CancellationTokenSource(); UpdateControls();
        _activity.Text = "Exporting all matching entries. This can take a moment for a large wallet.";
        bool created = false, complete = false;
        try
        {
            if (File.Exists(path)) throw new IOException("Choose a new filename. Existing files are not overwritten.");
            string csv = await _wallet.ExportHistoryCsvAsync(_endpoint!, _historyQuery, _historyType, _historyState, _historyCancellation.Token);
            _historyCancellation.Token.ThrowIfCancellationRequested();
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (OperatingSystem.IsLinux()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using var file = new FileStream(path, options);
            created = true;
            await using var writer = new StreamWriter(file, new UTF8Encoding(true));
            await writer.WriteAsync(csv.AsMemory(), _historyCancellation.Token);
            await writer.FlushAsync(_historyCancellation.Token);
            complete = true;
            _activity.Text = "History exported. The file includes wallet addresses and transaction details; share it carefully.";
        }
        catch (OperationCanceledException) { _activity.Text = "History export cancelled."; }
        catch (Exception ex) { _activity.Text = FriendlyError(ex); }
        finally
        {
            if (created && !complete)
            {
                try { File.Delete(path); }
                catch { _activity.Text += " A partial CSV remains at the selected path. Delete it before sharing."; }
            }
            _historyCancellation.Dispose(); _historyCancellation = null; _historyBusy = false; UpdateControls();
        }
    }

    private void UpdateHistoryControls()
    {
        bool enabled = _walletName.Length > 0 && !_busy && !_historyBusy && !_closing;
        _historySearch.IsEnabled = enabled; _historyDirection.IsEnabled = enabled; _historyStatus.IsEnabled = enabled;
        _historyFind.IsEnabled = enabled; _historyExport.IsEnabled = enabled && !_preview;
        _historyPrevious.IsEnabled = enabled && _historyPage > 0; _historyNext.IsEnabled = enabled && _historyHasMore;
        _historyCancel.IsVisible = _historyCancellation != null; _historyRows.IsEnabled = enabled;
    }

    private static WalletHistoryPage PreviewHistory(int page)
    {
        var items = Enumerable.Range(0, 3).Select(i => new WalletTransfer(new string((char)('a' + i), 64),
            i == 0 ? "Mined" : i == 1 ? "Received" : "Sent", i == 0 ? 9m : 1.25m, i == 2 ? 0.00001m : 0m,
            2000 - i, DateTimeOffset.UtcNow.AddMinutes(-i * 7), "tslithy1qexampleaddressforthepreview", 12 + i,
            i == 0 ? "generate" : i == 1 ? "receive" : "send")).ToArray();
        return new WalletHistoryPage(items, page, HistoryPageSize, false, items.Length);
    }

    private static WalletTransactionDetails PreviewTransaction(string transactionHash) => new(transactionHash, 12,
        DateTimeOffset.UtcNow.AddMinutes(-15), 0.00001m, new string('0', 64), false,
        new[] { new WalletTransactionInput(new string('d', 64), 0, false) },
        new[] { new WalletTransactionOutput(0, "tslithy1qexampleaddressforthepreview", 1.25m, false),
            new WalletTransactionOutput(1, "tslithy1qexamplechangeaddress", 7.74999m, true) }, PreviewHistory(0).Entries);
}
