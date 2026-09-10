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
    private readonly string _address;

    public ReceiveDialog(string address)
    {
        InitializeComponent();
        _address = address;
        addressTextBox.Text = address;
        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode($"slithy:{address}", QRCodeGenerator.ECCLevel.Q);
        using QRCode code = new(data);
        qrPictureBox.Image = code.GetGraphic(8, Color.FromArgb(50, 58, 49), Color.White, true);
    }

    private void copyButton_Click(object? sender, EventArgs e)
    {
        Clipboard.SetText(_address);
        copyButton.Text = "Copied";
    }
}

