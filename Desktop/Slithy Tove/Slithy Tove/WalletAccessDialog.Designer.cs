//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Wallet Access Dialog Designer
//===============================================
#nullable disable

namespace Slithy_Tove;

// Visual Studio generated most of this file from the dialog designer.
partial class WalletAccessDialog
{
    private System.ComponentModel.IContainer components = null;
    private Label headingLabel;
    private Label descriptionLabel;
    private TabControl modeTabControl;
    private TabPage createTabPage;
    private TabPage openTabPage;
    private Label createNameLabel;
    private TextBox createNameTextBox;
    private Label createPasswordLabel;
    private TextBox createPasswordTextBox;
    private Label confirmPasswordLabel;
    private TextBox confirmPasswordTextBox;
    private Button createButton;
    private Label passwordHelpLabel;
    private Label existingWalletLabel;
    private ComboBox existingWalletComboBox;
    private Label openPasswordLabel;
    private TextBox openPasswordTextBox;
    private Button openButton;
    private Button restoreButton;
    private Button restoreWordsButton;
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
        modeTabControl = new TabControl();
        createTabPage = new TabPage();
        passwordHelpLabel = new Label();
        createButton = new Button();
        confirmPasswordTextBox = new TextBox();
        confirmPasswordLabel = new Label();
        createPasswordTextBox = new TextBox();
        createPasswordLabel = new Label();
        createNameTextBox = new TextBox();
        createNameLabel = new Label();
        openTabPage = new TabPage();
        openButton = new Button();
        restoreButton = new Button();
        restoreWordsButton = new Button();
        openPasswordTextBox = new TextBox();
        openPasswordLabel = new Label();
        existingWalletComboBox = new ComboBox();
        existingWalletLabel = new Label();
        cancelButton = new Button();
        modeTabControl.SuspendLayout();
        createTabPage.SuspendLayout();
        openTabPage.SuspendLayout();
        SuspendLayout();
        // 
        // headingLabel
        // 
        headingLabel.AutoSize = true;
        headingLabel.Font = new Font("Georgia", 20F, FontStyle.Bold);
        headingLabel.ForeColor = Color.FromArgb(59, 70, 57);
        headingLabel.Location = new Point(28, 24);
        headingLabel.Name = "headingLabel";
        headingLabel.Size = new Size(226, 39);
        headingLabel.TabIndex = 0;
        headingLabel.Text = "Your wallet";
        // 
        // descriptionLabel
        // 
        descriptionLabel.Font = new Font("Segoe UI", 10F);
        descriptionLabel.ForeColor = Color.FromArgb(102, 98, 83);
        descriptionLabel.Location = new Point(31, 70);
        descriptionLabel.Name = "descriptionLabel";
        descriptionLabel.Size = new Size(510, 48);
        descriptionLabel.TabIndex = 1;
        descriptionLabel.Text = "Create a wallet for this beta. Old test balances do not carry over. Previous wallet files stay on this computer.";
        // 
        // modeTabControl
        // 
        modeTabControl.Controls.Add(createTabPage);
        modeTabControl.Controls.Add(openTabPage);
        modeTabControl.Font = new Font("Segoe UI", 10F);
        modeTabControl.Location = new Point(31, 125);
        modeTabControl.Name = "modeTabControl";
        modeTabControl.SelectedIndex = 0;
        modeTabControl.Size = new Size(510, 365);
        modeTabControl.TabIndex = 2;
        modeTabControl.SelectedIndexChanged += modeTabControl_SelectedIndexChanged;
        // 
        // createTabPage
        // 
        createTabPage.BackColor = Color.FromArgb(255, 253, 247);
        createTabPage.Controls.Add(passwordHelpLabel);
        createTabPage.Controls.Add(createButton);
        createTabPage.Controls.Add(confirmPasswordTextBox);
        createTabPage.Controls.Add(confirmPasswordLabel);
        createTabPage.Controls.Add(createPasswordTextBox);
        createTabPage.Controls.Add(createPasswordLabel);
        createTabPage.Controls.Add(createNameTextBox);
        createTabPage.Controls.Add(createNameLabel);
        createTabPage.Location = new Point(4, 32);
        createTabPage.Name = "createTabPage";
        createTabPage.Padding = new Padding(20);
        createTabPage.Size = new Size(502, 329);
        createTabPage.TabIndex = 0;
        createTabPage.Text = "New wallet";
        // 
        // createNameLabel
        // 
        createNameLabel.AutoSize = true;
        createNameLabel.Location = new Point(24, 24);
        createNameLabel.Name = "createNameLabel";
        createNameLabel.Size = new Size(105, 23);
        createNameLabel.TabIndex = 0;
        createNameLabel.Text = "Wallet name";
        // 
        // createNameTextBox
        // 
        createNameTextBox.Location = new Point(24, 51);
        createNameTextBox.Name = "createNameTextBox";
        createNameTextBox.Size = new Size(450, 30);
        createNameTextBox.TabIndex = 1;
        createNameTextBox.Text = "My Slithy Wallet";
        // 
        // createPasswordLabel
        // 
        createPasswordLabel.AutoSize = true;
        createPasswordLabel.Location = new Point(24, 98);
        createPasswordLabel.Name = "createPasswordLabel";
        createPasswordLabel.Size = new Size(80, 23);
        createPasswordLabel.TabIndex = 2;
        createPasswordLabel.Text = "Password";
        // 
        // createPasswordTextBox
        // 
        createPasswordTextBox.Location = new Point(24, 125);
        createPasswordTextBox.Name = "createPasswordTextBox";
        createPasswordTextBox.Size = new Size(450, 30);
        createPasswordTextBox.TabIndex = 3;
        createPasswordTextBox.UseSystemPasswordChar = true;
        // 
        // confirmPasswordLabel
        // 
        confirmPasswordLabel.AutoSize = true;
        confirmPasswordLabel.Location = new Point(24, 170);
        confirmPasswordLabel.Name = "confirmPasswordLabel";
        confirmPasswordLabel.Size = new Size(147, 23);
        confirmPasswordLabel.TabIndex = 4;
        confirmPasswordLabel.Text = "Confirm password";
        // 
        // confirmPasswordTextBox
        // 
        confirmPasswordTextBox.Location = new Point(24, 197);
        confirmPasswordTextBox.Name = "confirmPasswordTextBox";
        confirmPasswordTextBox.Size = new Size(450, 30);
        confirmPasswordTextBox.TabIndex = 5;
        confirmPasswordTextBox.UseSystemPasswordChar = true;
        // 
        // createButton
        // 
        createButton.BackColor = Color.FromArgb(83, 104, 79);
        createButton.FlatStyle = FlatStyle.Flat;
        createButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        createButton.ForeColor = Color.White;
        createButton.Location = new Point(324, 260);
        createButton.Name = "createButton";
        createButton.Size = new Size(150, 44);
        createButton.TabIndex = 7;
        createButton.Text = "Create wallet";
        createButton.UseVisualStyleBackColor = false;
        createButton.Click += createButton_Click;
        // 
        // passwordHelpLabel
        // 
        passwordHelpLabel.AutoSize = true;
        passwordHelpLabel.Font = new Font("Segoe UI", 8.5F);
        passwordHelpLabel.ForeColor = Color.FromArgb(111, 103, 88);
        passwordHelpLabel.Location = new Point(24, 236);
        passwordHelpLabel.Name = "passwordHelpLabel";
        passwordHelpLabel.Size = new Size(250, 20);
        passwordHelpLabel.TabIndex = 6;
        passwordHelpLabel.Text = "Use at least 8 characters. Do not lose it.";
        // 
        // openTabPage
        // 
        openTabPage.BackColor = Color.FromArgb(255, 253, 247);
        openTabPage.Controls.Add(openButton);
        openTabPage.Controls.Add(restoreButton);
        openTabPage.Controls.Add(restoreWordsButton);
        openTabPage.Controls.Add(openPasswordTextBox);
        openTabPage.Controls.Add(openPasswordLabel);
        openTabPage.Controls.Add(existingWalletComboBox);
        openTabPage.Controls.Add(existingWalletLabel);
        openTabPage.Location = new Point(4, 32);
        openTabPage.Name = "openTabPage";
        openTabPage.Padding = new Padding(20);
        openTabPage.Size = new Size(502, 329);
        openTabPage.TabIndex = 1;
        openTabPage.Text = "Open wallet";
        // 
        // existingWalletLabel
        // 
        existingWalletLabel.AutoSize = true;
        existingWalletLabel.Location = new Point(24, 30);
        existingWalletLabel.Name = "existingWalletLabel";
        existingWalletLabel.Size = new Size(112, 23);
        existingWalletLabel.TabIndex = 0;
        existingWalletLabel.Text = "Choose wallet";
        // 
        // existingWalletComboBox
        // 
        existingWalletComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        existingWalletComboBox.FormattingEnabled = true;
        existingWalletComboBox.Location = new Point(24, 58);
        existingWalletComboBox.Name = "existingWalletComboBox";
        existingWalletComboBox.Size = new Size(450, 31);
        existingWalletComboBox.TabIndex = 1;
        // 
        // openPasswordLabel
        // 
        openPasswordLabel.AutoSize = true;
        openPasswordLabel.Location = new Point(24, 116);
        openPasswordLabel.Name = "openPasswordLabel";
        openPasswordLabel.Size = new Size(80, 23);
        openPasswordLabel.TabIndex = 2;
        openPasswordLabel.Text = "Password";
        // 
        // openPasswordTextBox
        // 
        openPasswordTextBox.Location = new Point(24, 144);
        openPasswordTextBox.Name = "openPasswordTextBox";
        openPasswordTextBox.Size = new Size(450, 30);
        openPasswordTextBox.TabIndex = 3;
        openPasswordTextBox.UseSystemPasswordChar = true;
        // 
        // openButton
        // 
        openButton.BackColor = Color.FromArgb(83, 104, 79);
        openButton.FlatStyle = FlatStyle.Flat;
        openButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        openButton.ForeColor = Color.White;
        openButton.Location = new Point(324, 259);
        openButton.Name = "openButton";
        openButton.Size = new Size(150, 44);
        openButton.TabIndex = 4;
        openButton.Text = "Open wallet";
        openButton.UseVisualStyleBackColor = false;
        openButton.Click += openButton_Click;
        // 
        // restoreButton
        // 
        restoreButton.Location = new Point(24, 214);
        restoreButton.Name = "restoreButton";
        restoreButton.Size = new Size(200, 44);
        restoreButton.TabIndex = 5;
        restoreButton.Text = "Restore backup";
        restoreButton.UseVisualStyleBackColor = true;
        restoreButton.Click += restoreButton_Click;
        // 
        // restoreWordsButton
        // 
        restoreWordsButton.Location = new Point(24, 264);
        restoreWordsButton.Name = "restoreWordsButton";
        restoreWordsButton.Size = new Size(200, 44);
        restoreWordsButton.TabIndex = 6;
        restoreWordsButton.Text = "Restore words";
        restoreWordsButton.UseVisualStyleBackColor = true;
        restoreWordsButton.Click += restoreWordsButton_Click;
        // 
        // cancelButton
        // 
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Location = new Point(391, 501);
        cancelButton.Name = "cancelButton";
        cancelButton.Size = new Size(150, 44);
        cancelButton.TabIndex = 3;
        cancelButton.Text = "Cancel";
        cancelButton.UseVisualStyleBackColor = true;
        // 
        // WalletAccessDialog
        // 
        AcceptButton = createButton;
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(246, 241, 232);
        CancelButton = cancelButton;
        ClientSize = new Size(574, 565);
        Controls.Add(cancelButton);
        Controls.Add(modeTabControl);
        Controls.Add(descriptionLabel);
        Controls.Add(headingLabel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "WalletAccessDialog";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Open or create a Slithy wallet";
        modeTabControl.ResumeLayout(false);
        createTabPage.ResumeLayout(false);
        createTabPage.PerformLayout();
        openTabPage.ResumeLayout(false);
        openTabPage.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}

