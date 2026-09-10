//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//          Restore Words Dialog Designer
//===============================================
#nullable disable

namespace Slithy_Tove;

// Designer-owned controls for restoring a wallet from recovery words.
partial class RestoreWordsDialog
{
    private System.ComponentModel.IContainer components = null;
    private Label headingLabel;
    private Label descriptionLabel;
    private Label walletNameLabel;
    private TextBox walletNameTextBox;
    private Label passwordLabel;
    private TextBox passwordTextBox;
    private Label confirmPasswordLabel;
    private TextBox confirmPasswordTextBox;
    private Label wordsLabel;
    private TextBox wordsTextBox;
    private Button restoreButton;
    private Button cancelButton;

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
        walletNameTextBox = new TextBox();
        passwordLabel = new Label();
        passwordTextBox = new TextBox();
        confirmPasswordLabel = new Label();
        confirmPasswordTextBox = new TextBox();
        wordsLabel = new Label();
        wordsTextBox = new TextBox();
        restoreButton = new Button();
        cancelButton = new Button();
        SuspendLayout();
        // 
        // headingLabel
        // 
        headingLabel.AutoSize = true;
        headingLabel.Font = new Font("Georgia", 18F, FontStyle.Bold);
        headingLabel.ForeColor = Color.FromArgb(59, 70, 57);
        headingLabel.Location = new Point(28, 24);
        headingLabel.Name = "headingLabel";
        headingLabel.Size = new Size(390, 35);
        headingLabel.TabIndex = 0;
        headingLabel.Text = "Restore recovery words";
        // 
        // descriptionLabel
        // 
        descriptionLabel.Font = new Font("Segoe UI", 10F);
        descriptionLabel.ForeColor = Color.FromArgb(102, 98, 83);
        descriptionLabel.Location = new Point(31, 72);
        descriptionLabel.Name = "descriptionLabel";
        descriptionLabel.Size = new Size(560, 54);
        descriptionLabel.TabIndex = 1;
        descriptionLabel.Text = "Enter the words in order. Slithy will rebuild the wallet and scan the chain for its balance.";
        // 
        // walletNameLabel
        // 
        walletNameLabel.AutoSize = true;
        walletNameLabel.Location = new Point(31, 139);
        walletNameLabel.Name = "walletNameLabel";
        walletNameLabel.Size = new Size(92, 20);
        walletNameLabel.TabIndex = 2;
        walletNameLabel.Text = "Wallet name";
        // 
        // walletNameTextBox
        // 
        walletNameTextBox.Location = new Point(31, 164);
        walletNameTextBox.Name = "walletNameTextBox";
        walletNameTextBox.Size = new Size(560, 27);
        walletNameTextBox.TabIndex = 3;
        walletNameTextBox.Text = "Restored Slithy Wallet";
        // 
        // passwordLabel
        // 
        passwordLabel.AutoSize = true;
        passwordLabel.Location = new Point(31, 209);
        passwordLabel.Name = "passwordLabel";
        passwordLabel.Size = new Size(70, 20);
        passwordLabel.TabIndex = 4;
        passwordLabel.Text = "Password";
        // 
        // passwordTextBox
        // 
        passwordTextBox.Location = new Point(31, 234);
        passwordTextBox.Name = "passwordTextBox";
        passwordTextBox.Size = new Size(560, 27);
        passwordTextBox.TabIndex = 5;
        passwordTextBox.UseSystemPasswordChar = true;
        // 
        // confirmPasswordLabel
        // 
        confirmPasswordLabel.AutoSize = true;
        confirmPasswordLabel.Location = new Point(31, 279);
        confirmPasswordLabel.Name = "confirmPasswordLabel";
        confirmPasswordLabel.Size = new Size(127, 20);
        confirmPasswordLabel.TabIndex = 6;
        confirmPasswordLabel.Text = "Confirm password";
        // 
        // confirmPasswordTextBox
        // 
        confirmPasswordTextBox.Location = new Point(31, 304);
        confirmPasswordTextBox.Name = "confirmPasswordTextBox";
        confirmPasswordTextBox.Size = new Size(560, 27);
        confirmPasswordTextBox.TabIndex = 7;
        confirmPasswordTextBox.UseSystemPasswordChar = true;
        // 
        // wordsLabel
        // 
        wordsLabel.AutoSize = true;
        wordsLabel.Location = new Point(31, 350);
        wordsLabel.Name = "wordsLabel";
        wordsLabel.Size = new Size(110, 20);
        wordsLabel.TabIndex = 8;
        wordsLabel.Text = "Recovery words";
        // 
        // wordsTextBox
        // 
        wordsTextBox.Location = new Point(31, 375);
        wordsTextBox.Multiline = true;
        wordsTextBox.Name = "wordsTextBox";
        wordsTextBox.Size = new Size(560, 112);
        wordsTextBox.TabIndex = 9;
        // 
        // restoreButton
        // 
        restoreButton.BackColor = Color.FromArgb(83, 104, 79);
        restoreButton.FlatStyle = FlatStyle.Flat;
        restoreButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        restoreButton.ForeColor = Color.White;
        restoreButton.Location = new Point(421, 513);
        restoreButton.Name = "restoreButton";
        restoreButton.Size = new Size(170, 44);
        restoreButton.TabIndex = 10;
        restoreButton.Text = "Restore wallet";
        restoreButton.UseVisualStyleBackColor = false;
        restoreButton.Click += restoreButton_Click;
        // 
        // cancelButton
        // 
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Location = new Point(245, 513);
        cancelButton.Name = "cancelButton";
        cancelButton.Size = new Size(150, 44);
        cancelButton.TabIndex = 11;
        cancelButton.Text = "Cancel";
        cancelButton.UseVisualStyleBackColor = true;
        // 
        // RestoreWordsDialog
        // 
        AcceptButton = restoreButton;
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(246, 241, 232);
        CancelButton = cancelButton;
        ClientSize = new Size(624, 586);
        Controls.Add(cancelButton);
        Controls.Add(restoreButton);
        Controls.Add(wordsTextBox);
        Controls.Add(wordsLabel);
        Controls.Add(confirmPasswordTextBox);
        Controls.Add(confirmPasswordLabel);
        Controls.Add(passwordTextBox);
        Controls.Add(passwordLabel);
        Controls.Add(walletNameTextBox);
        Controls.Add(walletNameLabel);
        Controls.Add(descriptionLabel);
        Controls.Add(headingLabel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "RestoreWordsDialog";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Restore Slithy wallet";
        ResumeLayout(false);
        PerformLayout();
    }
}
