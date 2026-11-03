using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Slithy_Tove;

namespace Slithy.Linux;

internal static class UiWalletSelfTest
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Address = "tslithy1qdwf9t7fmczdzajy9wc5hjykt2k3d9h4d5";
    private const string Words = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    public static void Run(string output)
    {
        CheckSelectionRules();
        CheckPendingPayment();
        foreach (int width in new[] { 680, 460 })
        {
            CheckTransaction(output, width);
            CheckCoins(output, width);
            CheckAddresses(output, width);
            CheckRecovery(output, width);
            CheckRestore(output, width);
        }
    }

    public static void CheckActivity(MainWindow window, string output, int width)
    {
        var controls = window.GetVisualDescendants().OfType<Control>().ToArray();
        Require(controls.OfType<TextBox>().Single(c => c.Name == "HistorySearch").Bounds.Width >= 180, "history search has usable width");
        Require(controls.OfType<ComboBox>().Any(c => c.Name == "HistoryDirection") &&
            controls.OfType<ComboBox>().Any(c => c.Name == "HistoryStatus"), "history filters are present");
        Require(controls.OfType<Button>().Count(c => c.Name?.StartsWith("HistoryTransaction", StringComparison.Ordinal) == true) == 3,
            "history rows open transaction details");
        Require(!Find<Button>(window, "HistoryPrevious").IsEnabled && !Find<Button>(window, "HistoryNext").IsEnabled,
            "one-page history does not offer empty navigation");
        Require(!Find<Button>(window, "HistoryExport").IsEnabled, "preview cannot export fixture history as a wallet");
        Set(window, "_historyBusy", true); Invoke(window, "UpdateControls");
        Require(!Find<Button>(window, "HistoryFind").IsEnabled && ((Button)Get(window, "_miningButton")!).IsEnabled,
            "a history request does not disable stopping the miner");
        Set(window, "_historyBusy", false); Invoke(window, "UpdateControls");
        Save(window, output, $"wallet-activity-{width}");
        Console.WriteLine($"PASS: activity filters, detail rows, paging and mining controls at {width}px.");
    }

    private static void CheckSelectionRules()
    {
        var selected = new HashSet<WalletOutpoint>();
        var coins = Coins();
        foreach (var item in coins.Take(200)) Require(Dialogs.TrySelectCoin(selected, item), "eligible output selectable");
        Require(!Dialogs.TrySelectCoin(selected, coins[200]) && selected.Count == 200, "coin selection stops at the app batch limit");
        Require(Dialogs.TrySelectCoin(selected, coins[0]) && selected.Count == 200, "existing selection does not count twice");
        selected.Clear();
        foreach (var invalid in new[] { coins[0] with { Safe = false }, coins[0] with { Spendable = false }, coins[0] with { Confirmations = 0 } })
            Require(!Dialogs.TrySelectCoin(selected, invalid), "unsafe or immature output cannot be selected");
        var entry = new WalletTransfer(new string('a', 64), "Mined", 9m, 0m, 1, null, Address, 2, "immature");
        Require(MainWindow.HistoryConfirmation(entry).Contains("not spendable yet"), "immature reward status");
        Require(MainWindow.HistoryConfirmation(entry with { Confirmations = -1 }) == "Conflicted", "conflicted status");
        Require(MainWindow.HistoryConfirmation(entry with { Confirmations = 0 }) == "Waiting for confirmation", "pending status");
        Require(MainWindow.HistoryConfirmation(entry with { Abandoned = true }) == "Abandoned", "abandoned status");
        Console.WriteLine("PASS: coin eligibility, 200-input app limit and transaction status wording.");
    }

    private static void CheckPendingPayment()
    {
        var prepared = new PreparedTransfer(new string('a', 64), 1000, "00aa", Address, 100_000_000, 2);
        MainWindow.ValidatePreparedPayment(prepared);
        Require(!MainWindow.PaymentConfirmation(prepared).Contains("reduces privacy") &&
            MainWindow.PaymentConfirmation(prepared with { ReusesChangeAddress = true }).Contains("Reusing an address reduces privacy."),
            "reused change address is disclosed before confirming a deep-restore payment");
        foreach (var invalid in new[] { prepared with { InputCount = 0 }, prepared with { AmountAtomic = 0 }, prepared with { RecipientAddress = "" } })
        {
            bool refused = false; try { MainWindow.ValidatePreparedPayment(invalid); } catch (InvalidDataException) { refused = true; }
            Require(refused, "confirmation refuses missing signed payment details");
        }
        string Record(PreparedTransfer transfer, string address = Address, decimal amount = 1m) => JsonSerializer.Serialize(new { Transfer = transfer, Address = address, Amount = amount });
        Require(MainWindow.PendingRecordIsValid(Record(prepared)), "reviewed payment can be reloaded");
        Require(!MainWindow.PendingRecordIsValid(Record(prepared, amount: 2m)) &&
            !MainWindow.PendingRecordIsValid(Record(prepared, address: "changed")), "mismatched pending summary is refused");
        Require(MainWindow.PendingRecordIsValid(Record(new PreparedTransfer(new string('a', 64), 1000, "00aa"))), "older pending records remain recoverable");
        string root = Path.Combine(Path.GetTempPath(), "slithy-pending-ui-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LinuxSettingsStore(root); Directory.CreateDirectory(store.ConfigDirectory);
            var window = new MainWindow(preview: true, store: store);
            Set(window, "_walletName", "fixture");
            Invoke(window, "RememberApprovedPayment", prepared);
            string path = (string)typeof(MainWindow).GetProperty("PendingPath", Private)!.GetValue(window)!;
            Require(File.Exists(path), "approved signed payment is persisted before relay");
            Invoke(window, "DeletePending");
            Require(!File.Exists(path) && Get(window, "_pending") == null && (string)Get(window, "_pendingAddress")! == "" &&
                (decimal)Get(window, "_pendingAmount")! == 0 && !(bool)Get(window, "_paymentBlocked")!, "completed payment clears all update-blocking state");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        Console.WriteLine("PASS: exact prepared details, compatible retry records and completed-payment cleanup.");
    }

    private static void CheckTransaction(string output, int width)
    {
        var details = new WalletTransactionDetails(new string('a', 64), 12, DateTimeOffset.UtcNow, -0.00001m, new string('b', 64), false,
            new[] { new WalletTransactionInput(new string('c', 64), 7, false) },
            new[] { new WalletTransactionOutput(0, Address, 1m, false), new WalletTransactionOutput(1, Address + "change", 2m, true), new WalletTransactionOutput(2, "", 0m, false) },
            Array.Empty<WalletTransfer>());
        var dialog = Dialogs.CreateTransactionDialog(details); dialog.Width = width; dialog.Show(); Pump();
        Require(Find<Button>(dialog, "TransactionExplorer").IsEnabled, "valid transaction opens recent-block explorer");
        string text = AllText(dialog);
        Require(text.Contains("12 confirmations") && text.Contains("They do not identify a sender") &&
            text.Contains("no standard receiving address") && text.Contains("Older transactions may not appear"), "local transaction details explain inputs and explorer scope");
        Require(dialog.GetVisualDescendants().OfType<SelectableTextBlock>().Any(c => c.Text == new string('c', 64) + ":7"), "inputs display exact previous outpoints");
        Save(dialog, output, $"transaction-details-{width}"); dialog.Close(); Pump();
        dialog = Dialogs.CreateTransactionDialog(details with { TransactionHash = "invalid" }); dialog.Show(); Pump();
        Require(!Find<Button>(dialog, "TransactionExplorer").IsEnabled, "invalid transaction cannot create an explorer URL"); dialog.Close();
        dialog = Dialogs.CreateTransactionDialog(details with
        {
            Outputs = Enumerable.Range(0, 1000).Select(i => new WalletTransactionOutput(i, Address, 1m, false)).ToArray(),
            Inputs = Enumerable.Range(0, 1000).Select(i => new WalletTransactionInput(i.ToString("x64"), 0, false)).ToArray()
        });
        dialog.Width = width; dialog.Show(); Pump();
        Require(Find<ListBox>(dialog, "TransactionOutputs").ItemCount == 1000 && Find<ListBox>(dialog, "TransactionInputs").ItemCount == 1000,
            "large transaction details retain every input and output");
        Require(dialog.GetVisualDescendants().OfType<Control>().Count() < 1500, "large transaction details virtualize rows");
        Save(dialog, output, $"large-transaction-{width}"); dialog.Close(); Pump();
    }

    private static void CheckCoins(string output, int width)
    {
        var dialog = Dialogs.CreateCoinSelectionDialog(Coins(), consolidation: true); dialog.Width = width; dialog.Show(); Pump();
        Require(!Find<Button>(dialog, "CoinSelectionAccept").IsEnabled, "consolidation waits for two inputs");
        Click(Find<Button>(dialog, "CoinSelectBatch"));
        Require(Find<TextBlock>(dialog, "CoinSelectionSummary").Text!.StartsWith("200 selected") &&
            Find<Button>(dialog, "CoinSelectionAccept").IsEnabled, "smallest-input batch makes consolidation accessible");
        Save(dialog, output, $"coin-control-{width}");
        Click(Find<Button>(dialog, "CoinSelectionClear"));
        Require(Find<TextBlock>(dialog, "CoinSelectionSummary").Text!.StartsWith("0 selected") &&
            !Find<Button>(dialog, "CoinSelectionAccept").IsEnabled, "clear resets selection and review action");
        Require(!dialog.GetVisualDescendants().OfType<CheckBox>().Any(c => c.IsChecked == true), "cleared selection has no checked rows");
        dialog.Close(); Pump();
    }

    private static void CheckAddresses(string output, int width)
    {
        var old = new WalletReceivingAddress(Address, "Mining", 27m, 102, true);
        var created = new WalletReceivingAddress(Address + "new", "Gift", 0m, 0, false);
        int calls = 0;
        var dialog = Dialogs.CreateReceivingDialog(Address, new[] { old }, label =>
        { Require(label == "Gift", "new address retains its label"); calls++; return Task.FromResult(created); });
        dialog.Width = width; dialog.Show(); Pump();
        Find<TextBox>(dialog, "NewAddressLabel").Text = "Gift";
        Click(Find<Button>(dialog, "CreateReceiveAddress"));
        Require(calls == 1 && Find<SelectableTextBlock>(dialog, "ReceiveAddress").Text == created.Address &&
            Find<ListBox>(dialog, "ReceiveHistory").ItemCount == 2, "new address appears in history and QR view");
        Find<ListBox>(dialog, "ReceiveHistory").SelectedItem = old; Pump();
        Require(calls == 1 && Find<SelectableTextBlock>(dialog, "ReceiveAddress").Text == old.Address,
            "selecting an old address does not create another address");
        Save(dialog, output, $"receiving-addresses-{width}"); dialog.Close(); Pump();
    }

    private static void CheckRecovery(string output, int width)
    {
        var dialog = Dialogs.CreateRecoveryWordsDialog(Words); dialog.Width = width; dialog.Show(); Pump();
        var third = Find<TextBox>(dialog, "RecoveryWord3"); var ninth = Find<TextBox>(dialog, "RecoveryWord9");
        third.Text = "wrong"; ninth.Text = "abandon"; Click(Find<Button>(dialog, "RecoveryQuizConfirm"));
        Require(dialog.IsVisible && Find<TextBlock>(dialog, "RecoveryQuizError").Text!.Contains("do not match"), "wrong recovery answer cannot complete backup");
        dialog.Close(); Require(dialog.IsVisible, "creation recovery quiz cannot be dismissed before verification");
        third.Text = "abandon"; ninth.Text = "abandon";
        Save(dialog, output, $"recovery-quiz-{width}"); Click(Find<Button>(dialog, "RecoveryQuizConfirm"));
        Require(!dialog.IsVisible && third.Text == "" && ninth.Text == "", "correct backup check closes and clears answers");
        int calls = 0;
        dialog = Dialogs.CreateRecoveryCheckDialog(words => { calls++; return Task.FromResult(words == Words); });
        dialog.Width = width; dialog.Show(); Pump();
        var input = Find<TextBox>(dialog, "CheckRecoveryWords"); input.Text = "another phrase";
        Click(Find<Button>(dialog, "RecoveryCheckSubmit"));
        Require(input.Text == "" && Find<TextBlock>(dialog, "RecoveryCheckResult").Text!.Contains("do not match"), "backup mismatch clears entered words");
        input.Text = Words; Click(Find<Button>(dialog, "RecoveryCheckSubmit"));
        Require(calls == 2 && input.Text == "" && Find<TextBlock>(dialog, "RecoveryCheckResult").Text!.StartsWith("These words match the recovery addresses in this wallet.") &&
            Find<TextBlock>(dialog, "RecoveryCheckResult").Text!.Contains("imported keys"), "wallet recovery check confirms without retaining phrase or overstating coverage");
        Save(dialog, output, $"recovery-check-{width}"); dialog.Close(); Pump();
        Console.WriteLine($"PASS: transaction, receiving-address, coin-control and recovery dialogs at {width}px.");
    }

    private static void CheckRestore(string output, int width)
    {
        var dialog = Dialogs.CreateWalletDialog("Restore recovery words"); dialog.Width = width;
        dialog.Show(); Pump();
        var expander = dialog.GetVisualDescendants().OfType<Expander>().Single(); expander.IsExpanded = true; Pump();
        var range = Find<NumericUpDown>(dialog, "RecoveryRangeEnd");
        Require(range.Minimum == 10000 && range.Maximum == 100000 && range.Value == 10000, "advanced recovery starts at the guarded range");
        range.Value = 12001;
        Find<TextBox>(dialog, "WalletName").Text = "restore-fixture";
        Find<TextBox>(dialog, "WalletPassword").Text = "fixture-password";
        Find<TextBox>(dialog, "WalletPasswordConfirm").Text = "fixture-password";
        Find<TextBox>(dialog, "WalletRecoveryWords").Text = Words;
        Save(dialog, output, $"restore-range-{width}"); Click(Find<Button>(dialog, "WalletSubmit"));
        Require(!dialog.IsVisible && range.Value == 12001, "valid deeper recovery index can be submitted");
        Pump();
    }

    private static WalletUnspentOutput[] Coins() => Enumerable.Range(0, 205).Select(i =>
        new WalletUnspentOutput(i.ToString("x64"), 0, Address, 9m + i, 120, true, true, "Mining reward " + i)).ToArray();
    private static T Find<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); }
    private static void Pump() => Dispatcher.UIThread.RunJobs();
    private static object? Get(MainWindow window, string field) => typeof(MainWindow).GetField(field, Private)!.GetValue(window);
    private static void Set(MainWindow window, string field, object value) => typeof(MainWindow).GetField(field, Private)!.SetValue(window, value);
    private static void Invoke(MainWindow window, string method, params object[] args) => typeof(MainWindow).GetMethod(method, Private)!.Invoke(window, args);
    private static string AllText(Window window) => string.Join("\n", window.GetVisualDescendants().OfType<TextBlock>().Select(c => c.Text));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Save(Window window, string output, string name)
    {
        Pump(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var image = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rendered wallet frame: " + name + ", visible=" + window.IsVisible);
        image.Save(Path.Combine(output, name + ".png"));
    }
}
