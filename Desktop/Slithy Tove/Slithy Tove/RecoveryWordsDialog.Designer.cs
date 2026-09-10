//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//          Recovery Words Dialog Designer
//===============================================
#nullable disable

namespace Slithy_Tove;

// Designer-owned controls for the recovery words screen.
partial class RecoveryWordsDialog
{
    private System.ComponentModel.IContainer components = null;
    private Label headingLabel;
    private Label introLabel;
    private Label walletNameLabel;
    private TextBox recoveryWordsTextBox;
    private Label warningLabel;
    private CheckBox savedCheckBox;
    private Button copyButton;
    private Button continueButton;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        headingLabel = new Label();
        introLabel = new Label();
        walletNameLabel = new Label();
        recoveryWordsTextBox = new TextBox();
        warningLabel = new Label();
        savedCheckBox = new CheckBox();
        copyButton = new Button();
        continueButton = new Button();
        SuspendLayout();
        // 
        // headingLabel
        // 
        headingLabel.AutoSize = true;
        headingLabel.Font = new Font("Georgia", 18F, FontStyle.Bold);
        headingLabel.ForeColor = Color.FromArgb(59, 70, 57);
        headingLabel.Location = new Point(28, 24);
        headingLabel.Name = "headingLabel";
        headingLabel.Size = new Size(337, 35);
        headingLabel.TabIndex = 0;
        headingLabel.Text = "Save these words";
        // 
        // introLabel
        // 
        introLabel.Font = new Font("Segoe UI", 10F);
        introLabel.ForeColor = Color.FromArgb(102, 98, 83);
        introLabel.Location = new Point(31, 72);
        introLabel.Name = "introLabel";
        introLabel.Size = new Size(560, 58);
        introLabel.TabIndex = 1;
        introLabel.Text = "These words can restore this wallet on another computer. Write them down in order and keep them private.";
        // 
        // walletNameLabel
        // 
        walletNameLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        walletNameLabel.ForeColor = Color.FromArgb(34, 53, 54);
        walletNameLabel.Location = new Point(31, 132);
        walletNameLabel.Name = "walletNameLabel";
        walletNameLabel.Size = new Size(560, 28);
        walletNameLabel.TabIndex = 2;
        walletNameLabel.Text = "Wallet";
        // 
        // recoveryWordsTextBox
        // 
        recoveryWordsTextBox.BackColor = Color.FromArgb(255, 253, 247);
        recoveryWordsTextBox.Font = new Font("Segoe UI", 13F);
        recoveryWordsTextBox.Location = new Point(31, 170);
        recoveryWordsTextBox.Multiline = true;
        recoveryWordsTextBox.Name = "recoveryWordsTextBox";
        recoveryWordsTextBox.ReadOnly = true;
        recoveryWordsTextBox.Size = new Size(560, 116);
        recoveryWordsTextBox.TabIndex = 3;
        // 
        // warningLabel
        // 
        warningLabel.Font = new Font("Segoe UI", 9.5F);
        warningLabel.ForeColor = Color.FromArgb(128, 82, 35);
        warningLabel.Location = new Point(31, 302);
        warningLabel.Name = "warningLabel";
        warningLabel.Size = new Size(560, 58);
        warningLabel.TabIndex = 4;
        warningLabel.Text = "Do not send these words in email or chat. Anyone who has them can restore and spend this wallet.";
        // 
        // savedCheckBox
        // 
        savedCheckBox.AutoSize = true;
        savedCheckBox.Font = new Font("Segoe UI", 10F);
        savedCheckBox.Location = new Point(31, 376);
        savedCheckBox.Name = "savedCheckBox";
        savedCheckBox.Size = new Size(326, 27);
        savedCheckBox.TabIndex = 5;
        savedCheckBox.Text = "I saved these words somewhere private";
        savedCheckBox.UseVisualStyleBackColor = true;
        savedCheckBox.CheckedChanged += savedCheckBox_CheckedChanged;
        // 
        // copyButton
        // 
        copyButton.Location = new Point(31, 426);
        copyButton.Name = "copyButton";
        copyButton.Size = new Size(170, 44);
        copyButton.TabIndex = 6;
        copyButton.Text = "Copy words";
        copyButton.UseVisualStyleBackColor = true;
        copyButton.Click += copyButton_Click;
        // 
        // continueButton
        // 
        continueButton.BackColor = Color.FromArgb(83, 104, 79);
        continueButton.DialogResult = DialogResult.OK;
        continueButton.Enabled = false;
        continueButton.FlatStyle = FlatStyle.Flat;
        continueButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        continueButton.ForeColor = Color.White;
        continueButton.Location = new Point(421, 426);
        continueButton.Name = "continueButton";
        continueButton.Size = new Size(170, 44);
        continueButton.TabIndex = 7;
        continueButton.Text = "Continue";
        continueButton.UseVisualStyleBackColor = false;
        // 
        // RecoveryWordsDialog
        // 
        AcceptButton = continueButton;
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(246, 241, 232);
        ClientSize = new Size(624, 500);
        Controls.Add(continueButton);
        Controls.Add(copyButton);
        Controls.Add(savedCheckBox);
        Controls.Add(warningLabel);
        Controls.Add(recoveryWordsTextBox);
        Controls.Add(walletNameLabel);
        Controls.Add(introLabel);
        Controls.Add(headingLabel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "RecoveryWordsDialog";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Save wallet recovery words";
        ResumeLayout(false);
        PerformLayout();
    }
}
