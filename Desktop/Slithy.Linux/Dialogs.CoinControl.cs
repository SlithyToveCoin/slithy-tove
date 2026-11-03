using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Slithy_Tove;

namespace Slithy.Linux;

internal static partial class Dialogs
{
    internal const int CoinSelectionLimit = 200;

    internal static async Task<IReadOnlyList<WalletOutpoint>?> ChooseCoins(Window owner,
        IReadOnlyList<WalletUnspentOutput> outputs, bool consolidation = false,
        IReadOnlyList<WalletOutpoint>? previous = null) =>
        await CreateCoinSelectionDialog(outputs, consolidation, previous).ShowDialog<IReadOnlyList<WalletOutpoint>?>(owner);

    internal static Window CreateCoinSelectionDialog(IReadOnlyList<WalletUnspentOutput> outputs, bool consolidation = false,
        IReadOnlyList<WalletOutpoint>? previous = null)
    {
        var (dialog, body) = Shell(consolidation ? "Combine mining rewards" : "Choose transaction inputs", 700);
        body.Children.Add(Text(consolidation
            ? "Combine smaller spendable outputs into a fresh address in this wallet. You will review the fee before anything is sent."
            : "Automatic selection works for normal payments. Choose specific outputs here if you need more control."));
        body.Children.Add(Text("Choose up to 200 outputs per transaction. This is the app's batch size, not an address limit.", 12));
        var selected = new HashSet<WalletOutpoint>(previous ?? Array.Empty<WalletOutpoint>());
        var eligible = outputs.Where(CanSelectCoin).ToDictionary(item => item.Outpoint);
        selected.RemoveWhere(item => !eligible.ContainsKey(item));
        if (selected.Count > CoinSelectionLimit) selected.Clear();
        var summary = Text(""); summary.Name = "CoinSelectionSummary";
        var error = Text(""); error.Name = "CoinSelectionError"; error.Foreground = Brushes.DarkRed;
        var filter = new TextBox { Name = "CoinSearch", Watermark = "Filter by address, label or transaction ID" };
        var list = new ListBox { Name = "CoinSelectionList", MinHeight = 150, MaxHeight = 300 };
        var accept = Button(consolidation ? "Review consolidation" : "Use selected inputs"); accept.Name = "CoinSelectionAccept";
        void UpdateSummary()
        {
            decimal total = selected.Sum(item => eligible[item].Amount);
            summary.Text = $"{selected.Count} selected | {total:N8} SLTHY before fees";
            accept.IsEnabled = selected.Count >= (consolidation ? 2 : 1) && selected.Count <= CoinSelectionLimit;
        }
        list.ItemTemplate = new FuncDataTemplate<WalletUnspentOutput>((item, _) =>
        {
            if (item == null) return Text("");
            var content = new StackPanel { Spacing = 3 };
            content.Children.Add(Text($"{item.Amount:N8} SLTHY | {item.Confirmations} confirmations", 14));
            content.Children.Add(Text(item.Label.Length > 0 ? item.Label : "Unlabelled output", 12));
            content.Children.Add(new TextBlock { Text = item.Address, FontSize = 12, Foreground = Ink, TextTrimming = TextTrimming.CharacterEllipsis });
            content.Children.Add(new TextBlock { Text = item.TransactionHash + ":" + item.OutputIndex, FontSize = 12, Foreground = Ink, TextTrimming = TextTrimming.CharacterEllipsis });
            var choice = new CheckBox { Content = content, IsChecked = selected.Contains(item.Outpoint), IsEnabled = CanSelectCoin(item), HorizontalAlignment = HorizontalAlignment.Stretch };
            ToolTip.SetTip(choice, CanSelectCoin(item) ? item.Address : "This output is not confirmed and safe to spend.");
            choice.IsCheckedChanged += (_, _) =>
            {
                if (choice.IsChecked == true)
                {
                    if (!TrySelectCoin(selected, item)) { choice.IsChecked = false; error.Text = "Select at most 200 spendable outputs. You can combine another batch afterwards."; }
                    else error.Text = "";
                }
                else selected.Remove(item.Outpoint);
                UpdateSummary();
            };
            return choice;
        });
        void Filter()
        {
            string text = filter.Text?.Trim() ?? "";
            list.ItemsSource = outputs.Where(item => text.Length == 0 || item.Address.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                item.Label.Contains(text, StringComparison.OrdinalIgnoreCase) || item.TransactionHash.Contains(text, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        filter.TextChanged += (_, _) => Filter(); Filter(); UpdateSummary();
        body.Children.Add(filter); body.Children.Add(summary); body.Children.Add(list); body.Children.Add(error);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        accept.Click += (_, _) => dialog.Close((IReadOnlyList<WalletOutpoint>)selected.OrderBy(item => item.TransactionHash, StringComparer.Ordinal).ThenBy(item => item.OutputIndex).ToArray());
        actions.Children.Add(accept);
        if (!consolidation)
        {
            var automatic = Button("Use automatic selection"); automatic.Margin = new Thickness(8, 0, 0, 0);
            automatic.Click += (_, _) => dialog.Close((IReadOnlyList<WalletOutpoint>)Array.Empty<WalletOutpoint>()); actions.Children.Add(automatic);
        }
        else
        {
            var batch = Button("Select smallest 200"); batch.Name = "CoinSelectBatch"; batch.Margin = new Thickness(8, 0, 0, 0);
            batch.Click += (_, _) =>
            {
                selected.Clear();
                foreach (var item in eligible.Values.OrderBy(item => item.Amount).ThenBy(item => item.TransactionHash, StringComparer.Ordinal).Take(CoinSelectionLimit))
                    selected.Add(item.Outpoint);
                list.ItemsSource = null; Filter(); UpdateSummary();
            };
            actions.Children.Add(batch);
        }
        var clear = Button("Clear selection"); clear.Margin = new Thickness(8, 0, 0, 0);
        clear.Name = "CoinSelectionClear";
        clear.Click += (_, _) => { selected.Clear(); list.ItemsSource = null; Filter(); UpdateSummary(); }; actions.Children.Add(clear);
        var cancel = Button("Cancel"); cancel.Margin = new Thickness(8, 0, 0, 0); cancel.IsCancel = true;
        cancel.Click += (_, _) => dialog.Close(); actions.Children.Add(cancel); body.Children.Add(actions);
        return dialog;
    }

    internal static bool CanSelectCoin(WalletUnspentOutput output) => output.Spendable && output.Safe && output.Confirmations > 0;

    internal static bool TrySelectCoin(HashSet<WalletOutpoint> selected, WalletUnspentOutput output)
    {
        if (!CanSelectCoin(output) || !selected.Contains(output.Outpoint) && selected.Count >= CoinSelectionLimit) return false;
        selected.Add(output.Outpoint); return true;
    }
}
