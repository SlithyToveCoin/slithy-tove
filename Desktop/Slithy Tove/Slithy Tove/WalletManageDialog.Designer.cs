//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Wallet Manage Dialog Designer
//===============================================
#nullable disable

namespace Slithy_Tove;

// Designer-owned controls for the wallet management dialog.
partial class WalletManageDialog
{
    private System.ComponentModel.IContainer components = null;
    private Label headingLabel;
    private Label descriptionLabel;
    private Label walletNameLabel;
    private Button backupButton;
    private Button restoreButton;
    private Button lockButton;
    private Button openOtherButton;
    private Button closeButton;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        headingLabel = new Label();
        descriptionLabel = new Label();
        walletNameLabel = new Label();
        backupButton = new Button();
        restoreButton = new Button();
        lockButton = new Button();
        openOtherButton = new Button();
        closeButton = new Button();
        SuspendLayout();
        // 
        // headingLabel
        // 
        headingLabel.AutoSize = true;
        headingLabel.Font = new Font("Georgia", 18F, FontStyle.Bold);
        headingLabel.ForeColor = Color.FromArgb(59, 70, 57);
        headingLabel.Location = new Point(28, 24);
        headingLabel.Name = "headingLabel";
        headingLabel.Size = new Size(232, 35);
        headingLabel.TabIndex = 0;
        headingLabel.Text = "Manage wallet";
        // 
        // descriptionLabel
        // 
        descriptionLabel.Font = new Font("Segoe UI", 10F);
        descriptionLabel.ForeColor = Color.FromArgb(102, 98, 83);
        descriptionLabel.Location = new Point(31, 68);
        descriptionLabel.Name = "descriptionLabel";
        descriptionLabel.Size = new Size(430, 46);
        descriptionLabel.TabIndex = 1;
        descriptionLabel.Text = "Back up your wallet, restore another wallet backup, or lock this wallet.";
        // 
        // walletNameLabel
        // 
        walletNameLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        walletNameLabel.ForeColor = Color.FromArgb(34, 53, 54);
        walletNameLabel.Location = new Point(31, 118);
        walletNameLabel.Name = "walletNameLabel";
        walletNameLabel.Size = new Size(430, 30);
        walletNameLabel.TabIndex = 2;
        walletNameLabel.Text = "Wallet";
        // 
        // backupButton
        // 
        backupButton.BackColor = Color.FromArgb(83, 104, 79);
        backupButton.FlatStyle = FlatStyle.Flat;
        backupButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        backupButton.ForeColor = Color.White;
        backupButton.Location = new Point(31, 166);
        backupButton.Name = "backupButton";
        backupButton.Size = new Size(200, 52);
        backupButton.TabIndex = 3;
        backupButton.Text = "Back up wallet";
        backupButton.UseVisualStyleBackColor = false;
        backupButton.Click += backupButton_Click;
        // 
        // restoreButton
        // 
        restoreButton.BackColor = Color.FromArgb(239, 226, 198);
        restoreButton.FlatStyle = FlatStyle.Flat;
        restoreButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        restoreButton.ForeColor = Color.FromArgb(58, 68, 55);
        restoreButton.Location = new Point(261, 166);
        restoreButton.Name = "restoreButton";
        restoreButton.Size = new Size(200, 52);
        restoreButton.TabIndex = 4;
        restoreButton.Text = "Restore backup";
        restoreButton.UseVisualStyleBackColor = false;
        restoreButton.Click += restoreButton_Click;
        // 
        // lockButton
        // 
        lockButton.Location = new Point(31, 236);
        lockButton.Name = "lockButton";
        lockButton.Size = new Size(200, 48);
        lockButton.TabIndex = 5;
        lockButton.Text = "Lock wallet";
        lockButton.UseVisualStyleBackColor = true;
        lockButton.Click += lockButton_Click;
        // 
        // openOtherButton
        // 
        openOtherButton.Location = new Point(261, 236);
        openOtherButton.Name = "openOtherButton";
        openOtherButton.Size = new Size(200, 48);
        openOtherButton.TabIndex = 6;
        openOtherButton.Text = "Open another";
        openOtherButton.UseVisualStyleBackColor = true;
        openOtherButton.Click += openOtherButton_Click;
        // 
        // closeButton
        // 
        closeButton.DialogResult = DialogResult.Cancel;
        closeButton.Location = new Point(311, 312);
        closeButton.Name = "closeButton";
        closeButton.Size = new Size(150, 44);
        closeButton.TabIndex = 7;
        closeButton.Text = "Close";
        closeButton.UseVisualStyleBackColor = true;
        // 
        // WalletManageDialog
        // 
        AcceptButton = backupButton;
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(246, 241, 232);
        CancelButton = closeButton;
        ClientSize = new Size(494, 384);
        Controls.Add(closeButton);
        Controls.Add(openOtherButton);
        Controls.Add(lockButton);
        Controls.Add(restoreButton);
        Controls.Add(backupButton);
        Controls.Add(walletNameLabel);
        Controls.Add(descriptionLabel);
        Controls.Add(headingLabel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "WalletManageDialog";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Manage Slithy wallet";
        ResumeLayout(false);
        PerformLayout();
    }
}
