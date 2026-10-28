namespace Slithy_Tove;

internal sealed class SpendingPasswordDialog : Form
{
    private readonly TextBox _password = new() { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
    public string Password => _password.Text;

    public SpendingPasswordDialog()
    {
        Text = "Unlock for this payment";
        BackColor = Color.FromArgb(246, 241, 232);
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(440, 195);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), RowCount = 3, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.Controls.Add(new Label { Text = "Your balance stays visible. Enter your wallet password to prepare this payment.", AutoSize = true, Dock = DockStyle.Fill }, 0, 0);
        _password.AccessibleName = "Wallet password";
        layout.Controls.Add(_password, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var unlock = new Button { Text = "Unlock", DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(unlock);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);
        AcceptButton = unlock;
        CancelButton = cancel;
        Shown += (_, _) => _password.Focus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _password.Clear();
        base.Dispose(disposing);
    }
}
