using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Slithy.Linux;

internal static class Dialogs
{
    internal static readonly IBrush Ink = Brush.Parse("#30483b");
    internal static readonly IBrush Paper = Brush.Parse("#fffdf6");
    internal static readonly IBrush Gold = Brush.Parse("#ede0bd");

    internal static TextBlock Text(string value, double size = 14) => new()
    {
        Text = value, FontSize = size, Foreground = Ink, TextWrapping = TextWrapping.Wrap
    };

    internal static Button Button(string label) => new()
    {
        Content = label, Padding = new Thickness(14, 7), MinHeight = 34,
        Background = Gold, Foreground = Ink, HorizontalAlignment = HorizontalAlignment.Left,
        HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        CornerRadius = new CornerRadius(7), FontSize = 14
    };

    internal static Window CreateWindow(string title, double width = 480) => new()
    {
        Title = title, Icon = Branding.WindowIcon, Width = width, MaxHeight = 780, MinWidth = 360,
        SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Background = Paper, Foreground = Ink, FontSize = 14, CanResize = true
    };

    private static (Window Window, StackPanel Body) Shell(string title, double width = 480)
    {
        var body = new StackPanel { Spacing = 10, Margin = new Thickness(20) };
        var dialog = CreateWindow(title, width);
        dialog.Content = WindowChrome.Wrap(dialog, new ScrollViewer { Content = body });
        body.Children.Add(Text(title, 23));
        return (dialog, body);
    }

    internal static async Task Message(Window owner, string title, string message)
    {
        var (dialog, body) = Shell(title);
        body.Children.Add(Text(message));
        var close = Button("Close"); close.Click += (_, _) => dialog.Close(); body.Children.Add(close);
        await dialog.ShowDialog(owner);
    }

    internal static async Task<bool> Confirm(Window owner, string title, string message, string accept = "Continue")
    {
        var (dialog, body) = Shell(title);
        body.Children.Add(Text(message));
        var row = new WrapPanel { Orientation = Orientation.Horizontal };
        var yes = Button(accept); var no = Button("Cancel"); no.Margin = new Thickness(10, 0, 0, 0);
        yes.Click += (_, _) => dialog.Close(true); no.Click += (_, _) => dialog.Close(false);
        row.Children.Add(yes); row.Children.Add(no); body.Children.Add(row);
        return await dialog.ShowDialog<bool>(owner);
    }

    internal sealed record WalletInput(string Name, string Password, string Words, string? RequestedAction = null);

    internal static async Task<WalletInput?> Wallet(Window owner, string mode, IReadOnlyList<string>? names = null, string? selectedName = null)
        => await CreateWalletDialog(mode, names, selectedName).ShowDialog<WalletInput?>(owner);

    internal static Window CreateWalletDialog(string mode, IReadOnlyList<string>? names = null, string? selectedName = null)
    {
        bool create = mode is "Create wallet" or "Restore recovery words";
        bool opening = mode == "Open wallet";
        var (dialog, body) = Shell(mode);
        string[] available = (names ?? Array.Empty<string>()).Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        var name = new TextBox { Watermark = "Wallet name", Name = "WalletName" };
        var picker = new ComboBox
        {
            Name = "WalletPicker", ItemsSource = available, HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedItem = available.FirstOrDefault(name => name == selectedName) ?? available.FirstOrDefault(),
            IsEnabled = available.Length > 0, PlaceholderText = "No wallets on this computer"
        };
        var password = new TextBox { Watermark = "Password", PasswordChar = '●', Name = "WalletPassword" };
        var confirm = new TextBox { Watermark = "Confirm password", PasswordChar = '●' };
        var words = new TextBox { Watermark = "Your 12 recovery words", AcceptsReturn = true, MinHeight = 80, TextWrapping = TextWrapping.Wrap };
        body.Children.Add(Text(opening ? "Your wallet" : "Wallet name")); body.Children.Add(opening ? picker : name);
        if (opening && available.Length == 0)
            body.Children.Add(Text("Create a wallet or restore one from a backup or recovery words."));
        if (mode == "Restore wallet file") body.Children.Add(Text("Choose a new name for this copy. Enter the password used when the backup was made."));
        body.Children.Add(Text(create ? "Choose a password (at least 8 characters)" : "Wallet password")); body.Children.Add(password);
        if (create) body.Children.Add(confirm);
        if (mode == "Restore recovery words") { body.Children.Add(Text("Recovery words stay on this computer.")); body.Children.Add(words); }
        var error = Text(""); error.Foreground = Brushes.DarkRed; body.Children.Add(error);
        var submit = Button(mode); submit.Name = "WalletSubmit";
        submit.IsDefault = true;
        submit.IsEnabled = !opening || available.Length > 0;
        password.IsEnabled = submit.IsEnabled;
        var cancel = Button("Cancel"); cancel.Margin = new Thickness(8, 0, 0, 0);
        cancel.IsCancel = true;
        cancel.Click += (_, _) => dialog.Close();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        actions.Children.Add(submit); actions.Children.Add(cancel); body.Children.Add(actions);
        submit.Click += (_, _) =>
        {
            string walletName = opening ? picker.SelectedItem as string ?? "" : name.Text?.Trim() ?? "", secret = password.Text ?? "";
            if (opening && !available.Contains(walletName, StringComparer.Ordinal))
            { error.Text = "Choose a wallet from the list."; return; }
            if (!opening && (walletName.Length is < 1 or > 64 || walletName.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')))
            { error.Text = "Use 1 to 64 letters, numbers, hyphens or underscores for the wallet name."; return; }
            if (create && (secret.Length < 8 || secret != confirm.Text))
            { error.Text = "Enter matching passwords of at least 8 characters."; return; }
            var result = new WalletInput(walletName, secret, words.Text ?? "");
            password.Text = ""; confirm.Text = ""; words.Text = "";
            dialog.Close(result);
        };
        if (opening)
        {
            body.Children.Add(new Border { Height = 1, Background = Brush.Parse("#ded9cb"), Margin = new Thickness(0, 8) });
            var other = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (string action in new[] { "Create wallet", "Restore recovery words", "Restore wallet file" })
            {
                var button = Button(action); button.Margin = new Thickness(0, 0, 8, 6);
                button.Click += (_, _) => dialog.Close(new WalletInput("", "", "", action));
                other.Children.Add(button);
            }
            body.Children.Add(other);
        }
        dialog.Closed += (_, _) => { password.Text = ""; confirm.Text = ""; words.Text = ""; };
        dialog.Opened += (_, _) => { if (opening && available.Length > 0) password.Focus(); else if (!opening) name.Focus(); };
        return dialog;
    }

    internal static async Task<string?> Password(Window owner)
    {
        var (dialog, body) = Shell("Unlock wallet");
        var password = new TextBox { PasswordChar = '●', Watermark = "Wallet password" };
        body.Children.Add(password);
        var submit = Button("Unlock"); submit.Click += (_, _) => { string result = password.Text ?? ""; password.Text = ""; dialog.Close(result); };
        body.Children.Add(submit); dialog.Closed += (_, _) => password.Text = "";
        return await dialog.ShowDialog<string?>(owner);
    }

    internal static async Task RecoveryWords(Window owner, string words)
    {
        var (dialog, body) = Shell("Write down your recovery words");
        body.Children.Add(Text("These words restore your wallet. Write them down somewhere private. Anyone with them can spend your coins. Do not send them to support."));
        body.Children.Add(new SelectableTextBlock { Text = words, FontSize = 22, TextWrapping = TextWrapping.Wrap, Foreground = Ink });
        body.Children.Add(Text("Type word 3 and word 9 to confirm your copy."));
        var third = new TextBox { Watermark = "Word 3" }; var ninth = new TextBox { Watermark = "Word 9" };
        body.Children.Add(third); body.Children.Add(ninth);
        var error = Text(""); body.Children.Add(error);
        var done = Button("I recorded my recovery words"); bool recorded = false;
        done.Click += (_, _) =>
        {
            string[] parts = words.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (third.Text?.Trim().ToLowerInvariant() != parts[2] || ninth.Text?.Trim().ToLowerInvariant() != parts[8])
            { error.Text = "Those words do not match. Check your written copy."; return; }
            recorded = true; dialog.Close();
        };
        dialog.Closing += (_, e) => { if (!recorded) e.Cancel = true; };
        body.Children.Add(done); await dialog.ShowDialog(owner);
    }

    internal sealed record SendInput(string Address, decimal Amount);
    internal static async Task<SendInput?> Send(Window owner)
    {
        var (dialog, body) = Shell("Send test SLTHY");
        var address = new TextBox { Watermark = "Recipient address", Name = "RecipientAddress" };
        var amount = new TextBox { Watermark = "Amount, for example 1.25", Name = "SendAmount" };
        body.Children.Add(Text("Recipient address")); body.Children.Add(address); body.Children.Add(Text("Amount")); body.Children.Add(amount);
        body.Children.Add(Text("The next screen shows the address, amount and fee before anything is sent."));
        var error = Text(""); body.Children.Add(error);
        var review = Button("Review payment");
        review.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(address.Text) || !decimal.TryParse(amount.Text, System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture, out decimal value) || value <= 0 || decimal.Round(value, 8) != value || value > 21_000_000_000m)
            { error.Text = "Enter an address and a positive amount with up to 8 decimal places. Use a dot for decimals."; return; }
            dialog.Close(new SendInput(address.Text.Trim(), value));
        };
        body.Children.Add(review); return await dialog.ShowDialog<SendInput?>(owner);
    }
}
