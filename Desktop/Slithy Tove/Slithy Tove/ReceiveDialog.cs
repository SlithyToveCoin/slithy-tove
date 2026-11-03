//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Receive Dialog
//===============================================
using QRCoder;

namespace Slithy_Tove;

// Small window that shows the current receive address and QR code.
internal partial class ReceiveDialog : Form
{
    private string _address;
    private readonly WalletRpcClient? _client;
    private readonly Uri? _endpoint;
    private readonly ComboBox _previous = new() { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Previous receiving addresses", DropDownWidth = 700 };
    private readonly TextBox _label = new() { PlaceholderText = "Optional label for the new address", AccessibleName = "New address label", MaxLength = 100 };
    private readonly Label _status = WalletDialogLayout.Note("");
    private readonly Button _create;

    public ReceiveDialog(string address, WalletRpcClient? client = null, Uri? endpoint = null)
    {
        InitializeComponent();
        _address = address;
        _client = client;
        _endpoint = endpoint;
        Controls.Clear();
        headingLabel.Dispose();
        var page = WalletDialogLayout.Page(this, "Receive Slithy", 780, 760);
        WalletDialogLayout.Add(page, descriptionLabel);
        qrPictureBox.Anchor = AnchorStyles.None;
        qrPictureBox.Size = new Size(210, 210);
        WalletDialogLayout.Add(page, qrPictureBox);
        qrPictureBox.Anchor = AnchorStyles.None;
        WalletDialogLayout.Add(page, addressTextBox);
        WalletDialogLayout.Add(page, WalletDialogLayout.Actions(copyButton, closeButton));
        WalletDialogLayout.Add(page, WalletDialogLayout.Note("Previous receiving addresses still work. Selecting one below changes the address shown here, not a running miner."));
        WalletDialogLayout.Add(page, _previous);
        _previous.SelectedIndexChanged += (_, _) => { if (_previous.SelectedItem is AddressChoice choice) ShowAddress(choice.Address); };
        WalletDialogLayout.Add(page, _label);
        _create = WalletDialogLayout.Button("Create new receiving address", CreateAddress_Click);
        _create.Enabled = client is not null;
        WalletDialogLayout.Add(page, _create);
        WalletDialogLayout.Add(page, _status);
        _previous.Items.Add(new AddressChoice(address, "Current"));
        _previous.SelectedIndex = 0;
        Shown += async (_, _) => await LoadAddressesAsync();
        ShowAddress(address);
        Disposed += (_, _) => qrPictureBox.Image?.Dispose();
    }

    private void ShowAddress(string address)
    {
        _address = address;
        addressTextBox.Text = address;
        copyButton.Text = "Copy address";
        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode($"slithy:{address}", QRCodeGenerator.ECCLevel.Q);
        using QRCode code = new(data);
        Image? previous = qrPictureBox.Image;
        qrPictureBox.Image = code.GetGraphic(8, Color.FromArgb(50, 58, 49), Color.White, true);
        previous?.Dispose();
    }

    private async Task LoadAddressesAsync()
    {
        if (_client is null || _endpoint is null) return;
        try
        {
            var addresses = await _client.GetReceivingAddressesAsync(_endpoint);
            if (IsDisposed) return;
            string displayed = _address;
            _previous.Items.Clear();
            foreach (var item in addresses) _previous.Items.Add(new AddressChoice(item.Address, item.Label));
            if (!addresses.Any(item => item.Address == displayed)) _previous.Items.Insert(0, new AddressChoice(displayed, "Current"));
            _previous.SelectedIndex = _previous.Items.Cast<AddressChoice>().Select((item, index) => (item, index)).First(pair => pair.item.Address == displayed).index;
            _status.Text = "Your words restore addresses created from those words. A wallet-file backup also saves labels and any imported keys.";
        }
        catch (Exception ex) { if (!IsDisposed) _status.Text = "Could not load previous addresses: " + ex.Message; }
    }

    private async void CreateAddress_Click(object? sender, EventArgs e)
    {
        if (_client is null || _endpoint is null) return;
        _create.Enabled = false;
        try
        {
            WalletReceivingAddress address = await _client.CreateReceivingAddressAsync(_endpoint, _label.Text.Trim());
            if (IsDisposed) return;
            ShowAddress(address.Address);
            _label.Clear();
            await LoadAddressesAsync();
        }
        catch (Exception ex) { if (!IsDisposed) _status.Text = "No new address was shown: " + ex.Message; }
        finally { if (!IsDisposed) _create.Enabled = true; }
    }

    private sealed record AddressChoice(string Address, string Label)
    {
        public override string ToString() => string.IsNullOrWhiteSpace(Label) ? Address : Label + " : " + Address;
    }

    private void copyButton_Click(object? sender, EventArgs e)
    {
        Clipboard.SetText(_address);
        copyButton.Text = "Copied";
    }
}

