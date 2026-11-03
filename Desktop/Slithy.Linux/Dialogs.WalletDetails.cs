using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Slithy_Tove;

namespace Slithy.Linux;

internal static partial class Dialogs
{
    internal static Task Transaction(Window owner, WalletTransactionDetails details) =>
        CreateTransactionDialog(details).ShowDialog(owner);

    internal static Window CreateTransactionDialog(WalletTransactionDetails details)
    {
        var (dialog, body) = Shell("Transaction details", 680);
        body.Children.Add(Text(details.Abandoned ? "Abandoned" : details.Confirmations < 0 ? "Conflicted" :
            details.Confirmations == 0 ? "Waiting for confirmation" : $"{details.Confirmations} confirmations", 17));
        body.Children.Add(Text("Time: " + (details.Timestamp?.ToLocalTime().ToString("g") ?? "Unavailable") +
            "\nWallet fee: " + Math.Abs(details.Fee).ToString("N8") + " SLTHY"));
        AddCopyValue(dialog, body, "Transaction ID", details.TransactionHash);
        if (details.BlockHash.Length > 0) AddCopyValue(dialog, body, "Block hash", details.BlockHash);
        body.Children.Add(Text("Outputs", 18));
        Control OutputCard(WalletTransactionOutput output)
        {
            var card = new StackPanel { Spacing = 6 };
            card.Children.Add(Text($"Output {output.OutputIndex}: {output.Amount:N8} SLTHY" + (output.IsMine ? " | Your wallet" : ""), 16));
            if (output.Address.Length > 0) AddCopyValue(dialog, card, "Address", output.Address);
            else card.Children.Add(Text("This output has no standard receiving address."));
            return new Border { Background = Brush.Parse("#f3efe4"), CornerRadius = new CornerRadius(9), Padding = new Thickness(12), Child = card };
        }
        if (details.Outputs.Count <= 8)
            foreach (var output in details.Outputs) body.Children.Add(OutputCard(output));
        else
            body.Children.Add(new ListBox { Name = "TransactionOutputs", ItemsSource = details.Outputs, Height = 280,
                ItemTemplate = new FuncDataTemplate<WalletTransactionOutput>((item, _) => item == null ? Text("") : OutputCard(item)) });
        body.Children.Add(Text("Inputs", 18));
        body.Children.Add(Text("Inputs refer to earlier transaction outputs. They do not identify a sender."));
        Control InputRow(WalletTransactionInput input) => new SelectableTextBlock { Text = input.Coinbase ? "Newly mined coins (coinbase input)" :
            input.TransactionHash + ":" + input.OutputIndex.ToString(CultureInfo.InvariantCulture), TextWrapping = TextWrapping.Wrap, Foreground = Ink };
        if (details.Inputs.Count <= 10)
            foreach (var input in details.Inputs) body.Children.Add(InputRow(input));
        else
            body.Children.Add(new ListBox { Name = "TransactionInputs", ItemsSource = details.Inputs, Height = 180,
                ItemTemplate = new FuncDataTemplate<WalletTransactionInput>((item, _) => item == null ? Text("") : InputRow(item)) });
        var explorer = Button("Open recent-block explorer"); explorer.Name = "TransactionExplorer";
        explorer.IsEnabled = details.TransactionHash.Length == 64 && details.TransactionHash.All(Uri.IsHexDigit);
        explorer.Click += async (_, _) =>
        {
            try
            {
                var start = new ProcessStartInfo("xdg-open") { UseShellExecute = false, CreateNoWindow = true };
                start.ArgumentList.Add("https://slithy.io/explorer.html?tx=" + details.TransactionHash.ToLowerInvariant());
                using var process = Process.Start(start);
                if (process == null) throw new IOException();
            }
            catch { await Message(dialog, "Browser unavailable", "Open slithy.io/explorer.html in your browser and paste the transaction ID."); }
        };
        body.Children.Add(explorer);
        body.Children.Add(Text("The explorer covers recent blocks. Older transactions may not appear. The details above come from your local wallet.", 12));
        var close = Button("Close"); close.IsCancel = true; close.Click += (_, _) => dialog.Close(); body.Children.Add(close);
        return dialog;
    }

    private static void AddCopyValue(Window dialog, StackPanel body, string label, string value)
    {
        body.Children.Add(Text(label, 12));
        body.Children.Add(new SelectableTextBlock { Text = value, TextWrapping = TextWrapping.Wrap, Foreground = Ink });
        var copy = Button("Copy " + label.ToLowerInvariant());
        copy.Click += async (_, _) => { if (dialog.Clipboard != null) await dialog.Clipboard.SetTextAsync(value); copy.Content = "Copied"; };
        body.Children.Add(copy);
    }

    internal static async Task ReceivingAddresses(Window owner, string currentAddress, IReadOnlyList<WalletReceivingAddress> addresses,
        Func<string, Task<WalletReceivingAddress>> create)
    {
        var dialog = CreateReceivingDialog(currentAddress, addresses, create);
        await dialog.ShowDialog(owner);
    }

    internal static Window CreateReceivingDialog(string currentAddress, IReadOnlyList<WalletReceivingAddress> addresses,
        Func<string, Task<WalletReceivingAddress>> create)
    {
        var (dialog, body) = Shell("Receive test SLTHY", 620);
        var qr = new Image { Width = 180, Height = 180, HorizontalAlignment = HorizontalAlignment.Center };
        var addressText = new SelectableTextBlock { Name = "ReceiveAddress", TextWrapping = TextWrapping.Wrap, Foreground = Ink };
        string selectedAddress = currentAddress;
        void SelectAddress(string address)
        {
            selectedAddress = address; addressText.Text = address;
            using var generator = new QRCoder.QRCodeGenerator();
            using var data = generator.CreateQrCode(address, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var code = new QRCoder.PngByteQRCode(data);
            var old = qr.Source as Bitmap;
            qr.Source = new Bitmap(new MemoryStream(code.GetGraphic(6))); old?.Dispose();
        }
        SelectAddress(currentAddress);
        body.Children.Add(qr); body.Children.Add(addressText);
        var copy = Button("Copy address"); copy.Name = "CopyReceiveAddress";
        copy.Click += async (_, _) => { if (dialog.Clipboard != null) await dialog.Clipboard.SetTextAsync(selectedAddress); copy.Content = "Copied"; };
        body.Children.Add(copy);
        body.Children.Add(Text("Receiving addresses", 18));
        body.Children.Add(Text("Past addresses still work. A new address does not change a running miner's payout address.", 12));
        body.Children.Add(Text("Recovery words restore addresses created from those words. Back up the wallet file to retain imported keys and labels.", 12));
        var history = new ListBox { Name = "ReceiveHistory", MinHeight = 90, MaxHeight = 170, ItemsSource = addresses };
        history.ItemTemplate = new FuncDataTemplate<WalletReceivingAddress>((item, _) =>
        {
            if (item == null) return Text("");
            var line = new StackPanel { Spacing = 3 };
            line.Children.Add(Text((item.Label.Length == 0 ? "Unlabelled address" : item.Label) +
                (item.Used ? $" | {item.ReceivedAmount:N8} SLTHY received" : " | No payments yet"), 12));
            line.Children.Add(new TextBlock { Text = item.Address, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Ink, FontSize = 12 });
            ToolTip.SetTip(line, item.Address); return line;
        });
        history.SelectedItem = addresses.FirstOrDefault(item => item.Address == currentAddress);
        history.SelectionChanged += (_, _) => { if (history.SelectedItem is WalletReceivingAddress item) { SelectAddress(item.Address); copy.Content = "Copy address"; } };
        body.Children.Add(history);
        var label = new TextBox { Name = "NewAddressLabel", Watermark = "Label for your new address (optional)", MaxLength = 80 };
        body.Children.Add(label);
        var createButton = Button("Create new receiving address"); createButton.Name = "CreateReceiveAddress";
        var error = Text(""); error.Foreground = Brushes.DarkRed;
        bool working = false;
        createButton.Click += async (_, _) =>
        {
            if (working) return;
            working = true; createButton.IsEnabled = false; error.Text = "";
            try
            {
                var added = await create(label.Text?.Trim() ?? "");
                addresses = new[] { added }.Concat(addresses.Where(item => item.Address != added.Address)).ToArray();
                history.ItemsSource = addresses; history.SelectedItem = added; label.Text = "";
                SelectAddress(added.Address);
            }
            catch (Exception ex) { error.Text = ex is HttpRequestException ? "The local node could not create an address." : ex.Message; }
            finally { working = false; createButton.IsEnabled = true; }
        };
        body.Children.Add(createButton); body.Children.Add(error);
        var close = Button("Close"); close.IsCancel = true; close.Click += (_, _) => dialog.Close(); body.Children.Add(close);
        dialog.Closing += (_, e) => { if (working) e.Cancel = true; };
        dialog.Closed += (_, _) => (qr.Source as Bitmap)?.Dispose();
        return dialog;
    }

    internal static Task CheckRecoveryWords(Window owner, Func<string, Task<bool>> check) => CreateRecoveryCheckDialog(check).ShowDialog(owner);

    internal static Window CreateRecoveryCheckDialog(Func<string, Task<bool>> check)
    {
        var (dialog, body) = Shell("Check recovery words", 540);
        body.Children.Add(Text("Check that your written words belong to the open wallet. They restore addresses created from those words. A wallet-file backup also saves labels and imported keys. The words stay on this computer and are not saved or sent to support."));
        var words = new TextBox { Name = "CheckRecoveryWords", AcceptsReturn = true, MinHeight = 90, TextWrapping = TextWrapping.Wrap, Watermark = "Your recovery words" };
        var result = Text(""); result.Name = "RecoveryCheckResult";
        var verify = Button("Check this wallet"); verify.Name = "RecoveryCheckSubmit"; bool working = false;
        body.Children.Add(words); body.Children.Add(verify); body.Children.Add(result);
        verify.Click += async (_, _) =>
        {
            if (working || string.IsNullOrWhiteSpace(words.Text)) return;
            working = true; verify.IsEnabled = false;
            string entered = words.Text; words.Text = "";
            try { result.Text = await check(entered) ? "These words match the recovery addresses in this wallet. A wallet-file backup also saves labels and imported keys." : "These words do not match the open wallet. Check which wallet your backup belongs to."; }
            catch { result.Text = "The words could not be checked. Confirm the spelling and order, then try again."; }
            finally { entered = ""; working = false; verify.IsEnabled = true; }
        };
        var close = Button("Close"); close.IsCancel = true; close.Click += (_, _) => dialog.Close(); body.Children.Add(close);
        dialog.Closing += (_, e) => { if (working) e.Cancel = true; };
        dialog.Closed += (_, _) => words.Text = "";
        return dialog;
    }
}
