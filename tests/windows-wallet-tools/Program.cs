using System.Collections;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Slithy_Tove;

internal static class Program
{
    private static readonly Assembly App = typeof(Form1).Assembly;
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Address = "tslithy1qfixtureaddress00000000000000000000";
    private const string NewAddress = "tslithy1qnewfixtureaddress0000000000000000";
    private static readonly string Hash = new('a', 64);
    private static readonly Uri Endpoint = new("http://127.0.0.1:1");
    private static int checks;
    private static string? screenshots;

    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        screenshots = args.FirstOrDefault();
        if (screenshots is not null) Directory.CreateDirectory(screenshots);
        string temporary = Path.Combine(Path.GetTempPath(), "slithy-wallet-tools-" + Guid.NewGuid().ToString("N"));
        try
        {
            TestRecoveryChallenge();
            TestCoinSelection();
            TestRestoreRange(temporary);
            object client = New("WalletRpcClient");
            using IDisposable owned = (IDisposable)client;
            Set(client, "_walletName", "disposable-fixture");
            Set(client, "_currentAddress", Address);
            var calls = new List<string>();
            ReplaceHttp(Field(client, "_rpc"), new Handler(async request =>
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                string method = body.RootElement.GetProperty("method").GetString()!;
                calls.Add(method);
                object result = method switch
                {
                    "getwalletinfo" => new { txcount = 70, lastprocessedblock = new { hash = Hash } },
                    "listtransactions" => Enumerable.Range(1, 70).Select(n => new { txid = n.ToString("x64"), category = "receive", address = Address, amount = n / 10m, confirmations = n, time = 1793650000 + n }).ToArray(),
                    "listreceivedbyaddress" => new[] { new { address = Address, label = "Original", amount = 9m, confirmations = 5, txids = new[] { Hash } }, new { address = NewAddress, label = "Books", amount = 0m, confirmations = 0, txids = Array.Empty<string>() } },
                    "listlabels" => new[] { "Original", "Books" },
                    "getaddressesbylabel" => new Dictionary<string, object> { [Address] = new { purpose = "receive" }, [NewAddress] = new { purpose = "receive" } },
                    "getaddressinfo" => new { ismine = true, ischange = false },
                    "listdescriptors" => new { descriptors = Array.Empty<object>() },
                    "getnewaddress" => NewAddress,
                    "getblockchaininfo" => new { chain = "test" },
                    "gettransaction" => new { txid = Hash, confirmations = 6, blockheight = 100, blockhash = new string('b', 64), time = 1793650000, fee = -.0001m, details = new[] { new { address = Address, category = "receive", amount = 9m } }, decoded = new { vin = new[] { new { txid = new string('c', 64), vout = 0 } }, vout = new[] { new { n = 0, value = 9m, scriptPubKey = new { address = Address } } } } },
                    _ => throw new InvalidOperationException("Unexpected RPC " + method)
                };
                return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { result, error = (string?)null, id = "1" })) };
            }));
            TestReceive(client, calls);
            TestDetails(client);
            TestSendPreview(client);
            TestRecoveryCheck(client);
            TestHistory(temporary, client);
            Check(!calls.Any(method => method is "sendrawtransaction" or "startcpumining" or "stopcpumining"), "UI checks never relay a transaction or change mining");
            Console.WriteLine($"Passed {checks} checks using disposable settings and simulated RPC. No real wallet or node was used.");
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }

    private static void TestRecoveryChallenge()
    {
        string[] words = "one two three four five six seven eight nine ten eleven twelve".Split(' ');
        using Form dialog = (Form)New("RecoveryWordsDialog", "Fixture wallet", string.Join(' ', words));
        dialog.Show(); Pump();
        var saved = (CheckBox)Field(dialog, "savedCheckBox");
        var next = (Button)Field(dialog, "continueButton");
        Check(!next.Enabled, "Backup cannot continue before saving words");
        saved.Checked = true;
        next.PerformClick(); Pump();
        Check(dialog.DialogResult != DialogResult.OK && !(bool)Field(dialog, "_confirmed"), "Checkbox alone cannot confirm a recovery backup");
        var answers = ((IEnumerable)Field(dialog, "_answers")).Cast<TextBox>().ToArray();
        int[] positions = (int[])Field(dialog, "_positions");
        Check(positions.Length == 3 && positions.Distinct().Count() == 3, "Backup asks for three distinct word positions");
        Check(!((Control)Field(dialog, "recoveryWordsTextBox")).Visible, "Written words are hidden during the check");
        foreach (TextBox answer in answers) answer.Text = "wrong";
        next.PerformClick();
        Check(!(bool)Field(dialog, "_confirmed") && ((Label)Field(dialog, "warningLabel")).Text.Contains("do not match"), "Wrong words cannot complete the check");
        Capture(dialog, "recovery-word-check");
        ((Button)Field(dialog, "copyButton")).PerformClick();
        Check(answers.All(answer => answer.Text.Length == 0), "Showing words again clears entered answers");
        next.PerformClick();
        for (int i = 0; i < positions.Length; i++) answers[i].Text = words[positions[i]].ToUpperInvariant();
        next.PerformClick(); Pump();
        Check((bool)Field(dialog, "_confirmed"), "Matching positions complete the backup check");
    }

    private static void TestCoinSelection()
    {
        Array coins = Array.CreateInstance(Type("WalletUnspentOutput"), 205);
        for (int i = 0; i < coins.Length; i++) coins.SetValue(New("WalletUnspentOutput", i.ToString("x64"), 0, Address, 9m, 100L, true, true, ""), i);
        using Form dialog = (Form)New("CoinControlDialog", coins, null);
        dialog.Show(); Pump();
        var list = (ListView)Field(dialog, "_coins");
        list.Items[204].Checked = true;
        Button(dialog, "Select first 200").PerformClick(); Pump();
        Check(list.CheckedItems.Count == 200 && !list.Items[204].Checked, "Select first 200 replaces a previous selection correctly");
        list.Items[204].Checked = true;
        Check(list.CheckedItems.Count == 200, "Coin-control UI enforces the 200-input batch limit");
        Check((decimal)dialog.GetType().GetProperty("SelectedAmount", Hidden)!.GetValue(dialog)! == 1800m, "Selected total is the sum of checked outputs");
        Capture(dialog, "coin-control");
        CheckCompactLayout(dialog, "coin-control-compact");
        Button(dialog, "Clear").PerformClick();
        Check(list.CheckedItems.Count == 0, "Clear removes every selected input");
        dialog.Hide();
    }

    private static void TestRestoreRange(string temporary)
    {
        using Form dialog = (Form)New("RestoreWordsDialog", temporary, null);
        dialog.Show(); Pump();
        var range = (NumericUpDown)Field(dialog, "_recoveryRange");
        var advanced = (CheckBox)Field(dialog, "_advancedRecovery");
        int Value() => (int)dialog.GetType().GetProperty("RecoveryRangeEnd")!.GetValue(dialog)!;
        Check(Value() == 10000 && !advanced.Checked, "Restore starts with the usual 10,000-index recovery range");
        Check(range.Minimum == 10000 && range.Maximum == 100000, "Advanced scan accepts indices from 10,000 to 100,000");
        advanced.Checked = true;
        range.Value = 15000;
        Check(Value() == 15000, "Advanced restore passes the selected end index");
        advanced.Checked = false;
        Check(Value() == 10000, "Disabling Advanced returns to the usual range");
        advanced.Checked = true;
        Check(Controls(dialog).Any(c => c.Text.Contains("scans the chain from the beginning")), "Restore explains the full-chain scan");
        Capture(dialog, "restore-advanced");
        CheckCompactLayout(dialog, "restore-compact");
        dialog.Hide();
    }

    private static void TestReceive(object client, List<string> calls)
    {
        using Form dialog = (Form)New("ReceiveDialog", Address, client, Endpoint);
        dialog.Show(); PumpUntil(() => ((ComboBox)Field(dialog, "_previous")).Items.Count == 2);
        var previous = (ComboBox)Field(dialog, "_previous");
        previous.SelectedIndex = 1;
        Check((string)Field(client, "_currentAddress") == Address, "Viewing an older address does not change the current receive address or mining");
        ((TextBox)Field(dialog, "_label")).Text = "Books";
        ((Button)Field(dialog, "_create")).PerformClick();
        PumpUntil(() => calls.Contains("getnewaddress") && ((Button)Field(dialog, "_create")).Enabled);
        Check((string)Field(client, "_currentAddress") == NewAddress, "Creating an address updates the wallet's current receiving address");
        Check(((TextBox)Field(dialog, "addressTextBox")).Text == NewAddress, "New full address is displayed without shortening");
        Capture(dialog, "receive-addresses");
        CheckCompactLayout(dialog, "receive-compact");
        dialog.Hide();
    }

    private static void TestDetails(object client)
    {
        object transfer = New("WalletTransfer", Hash, "Received", 9m, 0m, 100L, (DateTimeOffset?)DateTimeOffset.Now, Address, 5L, "receive", false, 0);
        using Form dialog = (Form)New("TransactionDetailsDialog", client, Endpoint, transfer);
        dialog.Show(); PumpUntil(() => ((ListView)Field(dialog, "_outputs")).Items.Count == 1);
        Check(((Label)Field(dialog, "_confirmations")).Text == "6 confirmations", "Transaction details refresh the actual confirmation count");
        Check(((ListView)Field(dialog, "_outputs")).Items[0].SubItems[2].Text == Address, "Transaction details show the entire output address");
        Check(Controls(dialog).Any(c => c.Text.Contains("Older transactions may not appear")), "Explorer link describes its recent-block coverage");
        Capture(dialog, "transaction-details");
        CheckCompactLayout(dialog, "transaction-details-compact");
        dialog.Hide();
    }

    private static void TestSendPreview(object client)
    {
        using Form dialog = (Form)New("SendDialog", 27m, client, Endpoint);
        dialog.Show(); Pump();
        ((CheckBox)Field(dialog, "_consolidate")).Checked = true;
        Check(!((TextBox)Field(dialog, "addressTextBox")).Enabled, "Consolidation cannot accept an external recipient address");
        Check(((Button)Field(dialog, "continueButton")).Text == "Review consolidation", "Consolidation requires review before any send");
        object prepared = New("PreparedTransfer", Hash, 10_000UL, "signed-fixture", NewAddress, 2_699_990_000UL, 3, true, false);
        string text = (string)typeof(Form1).GetMethod("PreparedTransferConfirmation", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prepared])!;
        Check(text.Contains(NewAddress) && text.Contains("26.99990000") && text.Contains("0.00010000") && text.Contains("3 selected inputs"), "Consolidation preview uses the signed recipient, amount, input count and fee");
        object reused = New("PreparedTransfer", Hash, 10_000UL, "signed-fixture", NewAddress, 100_000_000UL, 2, false, true);
        string reusedText = (string)typeof(Form1).GetMethod("PreparedTransferConfirmation", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [reused])!;
        Check(reusedText.Contains("Reusing an address reduces privacy"), "Deep-restore change-address reuse is disclosed before confirmation");
        Capture(dialog, "send-advanced");
        CheckCompactLayout(dialog, "send-compact");
        dialog.Hide();
    }

    private static void TestRecoveryCheck(object client)
    {
        using Form dialog = (Form)New("RecoveryCheckDialog", client, Endpoint);
        dialog.Show(); Pump();
        var words = (TextBox)Field(dialog, "_words");
        words.Text = "invalid fixture words";
        Button(dialog, "Check backup").PerformClick();
        PumpUntil(() => words.Enabled);
        Check(words.Text.Length == 0, "Recovery check clears entered words after failure");
        Check(!Controls(dialog).Any(c => c is Button && c.Text.Contains("Copy")), "Recovery check has no clipboard action");
        words.Text = "fixture words";
        dialog.Close();
        Check(words.Text.Length == 0, "Recovery check clears words when closed");
    }

    private static void TestHistory(string temporary, object fixtureClient)
    {
        object store = New("AppSettingsStore", temporary);
        using Form1 form = (Form1)Activator.CreateInstance(typeof(Form1), Hidden, null, [store], null)!;
        form.Shown -= (EventHandler)typeof(Form1).GetMethod("Form1_Shown", Hidden)!.CreateDelegate(typeof(EventHandler), form);
        ((System.Windows.Forms.Timer)Field(form, "statusTimer")).Stop();
        ((NotifyIcon)Field(form, "_trayIcon")).Visible = false;
        Set(form, "_walletRpcClient", fixtureClient);
        Set(form, "_walletSnapshot", New("WalletSnapshot", Address, 100_000_000UL, 100_000_000UL, 0L, 100L, 0UL, 0UL));
        Set(form, "_openWalletName", "disposable-fixture");
        form.Show();
        ((TabControl)Field(form, "mainTabControl")).SelectedTab = (TabPage)Field(form, "_walletActivityTab");
        PumpUntil(() => !(bool)Field(form, "_historyLoading"));
        var list = (ListView)Field(form, "_walletActivityListView");
        Check(list.Items.Count == 50 && ((Button)Field(form, "_historyNext")).Enabled, "History displays 50 entries and offers the next page");
        ((Button)Field(form, "_historyNext")).PerformClick();
        PumpUntil(() => !(bool)Field(form, "_historyLoading"));
        Check(list.Items.Count == 20 && ((Button)Field(form, "_historyPrevious")).Enabled, "History reaches entries beyond the old 20-entry limit");
        ((TextBox)Field(form, "_historySearch")).Text = 1.ToString("x64");
        Run((Task)Call(form, "LoadWalletHistoryAsync", 0)!);
        Check(list.Items.Count == 1 && ((object)list.Items[0].Tag!).GetType().GetProperty("TransactionHash")!.GetValue(list.Items[0].Tag)!.Equals(1.ToString("x64")), "Search finds an entry beyond the first history page");
        ((TextBox)Field(form, "_historySearch")).Clear();
        Run((Task)Call(form, "LoadWalletHistoryAsync", 0)!);
        Task csvTask = (Task)fixtureClient.GetType().GetMethod("ExportHistoryCsvAsync")!.Invoke(fixtureClient, [Endpoint, "", "all", "all", CancellationToken.None])!;
        Run(csvTask);
        string csv = (string)csvTask.GetType().GetProperty("Result")!.GetValue(csvTask)!;
        Check(csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length == 71, "CSV includes all 70 entries, not just the visible page");
        Capture(form, "history-full");
        form.MinimumSize = Size.Empty;
        form.ClientSize = new Size(1000, 780); Pump();
        var help = (Label)Field(form, "_walletActivityHelpLabel");
        Check(help.Width >= TextRenderer.MeasureText(help.Text, help.Font).Width, "Activity help text is not clipped in a compact window");
        Check(((Control)Field(form, "_historyNext")).Bottom <= ((Control)Field(form, "_historyNext")).Parent!.ClientSize.Height, "History navigation remains reachable in a compact window");
        Capture(form, "history-compact");
        Call(form, "ClearWallet", false);
        Check(list.Items.Count == 0 && !((Button)Field(form, "_historyNext")).Enabled, "Lock clears transaction history and paging state");
        form.Hide();
    }

    private static Type Type(string name) => App.GetType("Slithy_Tove." + name)!;
    private static object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, args, null)!;
    private static object Field(object target, string name) => target.GetType().GetField(name, Hidden)!.GetValue(target)!;
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Hidden)!.SetValue(target, value);
    private static object? Call(object target, string name, params object?[] args) => target.GetType().GetMethod(name, Hidden)!.Invoke(target, args);
    private static IEnumerable<Control> Controls(Control root) { foreach (Control child in root.Controls) { yield return child; foreach (Control nested in Controls(child)) yield return nested; } }
    private static Button Button(Control root, string text) => Controls(root).OfType<Button>().First(button => button.Text == text);
    private static void ReplaceHttp(object service, HttpMessageHandler handler) { var field = service.GetType().GetField("_httpClient", Hidden)!; ((HttpClient)field.GetValue(service)!).Dispose(); field.SetValue(service, new HttpClient(handler)); }
    private static void Pump() { for (int i = 0; i < 8; i++) { Application.DoEvents(); Thread.Sleep(10); } }
    private static void PumpUntil(Func<bool> done) { DateTime deadline = DateTime.UtcNow.AddSeconds(20); while (!done() && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(5); } if (!done()) throw new TimeoutException("UI test did not finish"); Pump(); }
    private static void Run(Task task) { PumpUntil(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static void Capture(Form form, string name) { if (screenshots is null) return; Pump(); using Bitmap bitmap = new(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(screenshots, name + ".png")); }
    private static void CheckCompactLayout(Form dialog, string name)
    {
        dialog.ClientSize = new Size(900, 700); Pump();
        foreach (TableLayoutPanel table in Controls(dialog).OfType<TableLayoutPanel>().Where(table => table.ColumnCount == 1))
        {
            var children = table.Controls.Cast<Control>().Where(control => control.Visible).ToArray();
            Check(children.All(control => control.Left >= 0 && control.Right <= table.ClientSize.Width), name + " rows fit within the card");
            Check(!children.SelectMany((first, index) => children.Skip(index + 1).Select(second => first.Bounds.IntersectsWith(second.Bounds))).Any(overlap => overlap), name + " rows do not overlap");
        }
        Capture(dialog, name);
    }
    private static void Check(bool passed, string text) { if (!passed) throw new InvalidOperationException(text); checks++; Console.WriteLine("PASS " + text); }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> reply) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => reply(request); }
}
