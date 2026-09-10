namespace Slithy_Tove;

partial class TermsDialog
{
    private System.ComponentModel.IContainer components;
    private TableLayoutPanel layout;
    private Label heading;
    private Label explanation;
    private TextBox termsText;
    private CheckBox agreement;
    private FlowLayoutPanel buttons;
    private Button acceptButton;
    private Button declineButton;
    private Button filesButton;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    // All of the dialog controls are here so they can be moved in the designer.
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        layout = new TableLayoutPanel();
        heading = new Label();
        explanation = new Label();
        termsText = new TextBox();
        agreement = new CheckBox();
        buttons = new FlowLayoutPanel();
        acceptButton = new Button();
        declineButton = new Button();
        filesButton = new Button();
        SuspendLayout();
        layout.Dock = DockStyle.Fill;
        layout.Padding = new Padding(24);
        layout.ColumnCount = 1;
        layout.RowCount = 5;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        heading.Text = "Before you start Slithy";
        heading.Font = new Font("Georgia", 22, FontStyle.Bold);
        heading.AutoSize = true;
        heading.Margin = new Padding(0, 0, 0, 12);
        explanation.Text = "Read the terms below. Mining uses electricity and computer resources. Test coins will reset.\nDeclining leaves your wallets untouched. You can open your files below. Stop any running Slithy node before copying wallet files.";
        explanation.AutoSize = true;
        explanation.Dock = DockStyle.Fill;
        explanation.Margin = new Padding(0, 0, 0, 16);
        termsText.Dock = DockStyle.Fill;
        termsText.Multiline = true;
        termsText.ReadOnly = true;
        termsText.AccessibleName = "Terms of Use and Beta Testing";
        termsText.ScrollBars = ScrollBars.Vertical;
        termsText.BackColor = Color.FromArgb(255, 253, 246);
        termsText.Margin = new Padding(0, 0, 0, 14);
        termsText.TabIndex = 0;
        agreement.Text = "I agree to the Terms of Use and Beta Testing shown above.";
        agreement.AutoSize = true;
        agreement.Dock = DockStyle.Fill;
        agreement.Checked = false;
        agreement.Margin = new Padding(0, 0, 0, 14);
        agreement.TabIndex = 1;
        agreement.CheckedChanged += agreement_CheckedChanged;
        buttons.AutoSize = true;
        buttons.Dock = DockStyle.Fill;
        buttons.WrapContents = true;
        acceptButton.Text = "Agree and continue";
        acceptButton.AutoSize = true;
        acceptButton.Padding = new Padding(12, 8, 12, 8);
        acceptButton.BackColor = Color.FromArgb(49, 81, 65);
        acceptButton.ForeColor = Color.White;
        acceptButton.Enabled = false;
        acceptButton.Click += acceptButton_Click;
        declineButton.Text = "Decline and exit";
        declineButton.AutoSize = true;
        declineButton.Padding = new Padding(12, 8, 12, 8);
        declineButton.DialogResult = DialogResult.Cancel;
        filesButton.Text = "Open wallet files";
        filesButton.AutoSize = true;
        filesButton.Padding = new Padding(12, 8, 12, 8);
        filesButton.Click += filesButton_Click;
        buttons.Controls.Add(acceptButton);
        buttons.Controls.Add(declineButton);
        buttons.Controls.Add(filesButton);
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(explanation, 0, 1);
        layout.Controls.Add(termsText, 0, 2);
        layout.Controls.Add(agreement, 0, 3);
        layout.Controls.Add(buttons, 0, 4);
        Controls.Add(layout);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = new Font("Segoe UI", 11);
        BackColor = Color.FromArgb(244, 240, 230);
        ForeColor = Color.FromArgb(35, 56, 46);
        ClientSize = new Size(840, 720);
        MinimumSize = new Size(700, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Slithy Tove | Terms of Use";
        CancelButton = declineButton;
        ResumeLayout(false);
    }
}
