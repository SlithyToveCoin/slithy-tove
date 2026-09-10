//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Send Dialog Designer
//===============================================
#nullable disable

namespace Slithy_Tove;

// Visual Studio generated most of this file from the dialog designer.
partial class SendDialog
{
    private System.ComponentModel.IContainer components = null;
    private Label headingLabel;
    private Label addressLabel;
    private TextBox addressTextBox;
    private Label amountLabel;
    private NumericUpDown amountNumericUpDown;
    private Label availableLabel;
    private Label safetyLabel;
    private Button continueButton;
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
        addressLabel = new Label();
        addressTextBox = new TextBox();
        amountLabel = new Label();
        amountNumericUpDown = new NumericUpDown();
        availableLabel = new Label();
        safetyLabel = new Label();
        continueButton = new Button();
        cancelButton = new Button();
        ((System.ComponentModel.ISupportInitialize)amountNumericUpDown).BeginInit();
        SuspendLayout();
        // 
        // headingLabel
        // 
        headingLabel.AutoSize = true;
        headingLabel.Font = new Font("Georgia", 20F, FontStyle.Bold);
        headingLabel.ForeColor = Color.FromArgb(59, 70, 57);
        headingLabel.Location = new Point(30, 26);
        headingLabel.Name = "headingLabel";
        headingLabel.Size = new Size(205, 39);
        headingLabel.TabIndex = 0;
        headingLabel.Text = "Send Slithy";
        // 
        // addressLabel
        // 
        addressLabel.AutoSize = true;
        addressLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        addressLabel.Location = new Point(33, 91);
        addressLabel.Name = "addressLabel";
        addressLabel.Size = new Size(154, 23);
        addressLabel.TabIndex = 1;
        addressLabel.Text = "Receiving address";
        // 
        // addressTextBox
        // 
        addressTextBox.Font = new Font("Cascadia Mono", 9F);
        addressTextBox.Location = new Point(33, 119);
        addressTextBox.Multiline = true;
        addressTextBox.Name = "addressTextBox";
        addressTextBox.ScrollBars = ScrollBars.Vertical;
        addressTextBox.Size = new Size(520, 72);
        addressTextBox.TabIndex = 2;
        // 
        // amountLabel
        // 
        amountLabel.AutoSize = true;
        amountLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        amountLabel.Location = new Point(33, 218);
        amountLabel.Name = "amountLabel";
        amountLabel.Size = new Size(128, 23);
        amountLabel.TabIndex = 3;
        amountLabel.Text = "Amount SLTHY";
        // 
        // amountNumericUpDown
        // 
        amountNumericUpDown.DecimalPlaces = 8;
        amountNumericUpDown.Font = new Font("Segoe UI", 11F);
        amountNumericUpDown.Location = new Point(33, 247);
        amountNumericUpDown.Maximum = new decimal(new int[] { -1, -1, 0, 0 });
        amountNumericUpDown.Name = "amountNumericUpDown";
        amountNumericUpDown.Size = new Size(300, 32);
        amountNumericUpDown.TabIndex = 4;
        amountNumericUpDown.ThousandsSeparator = true;
        // 
        // availableLabel
        // 
        availableLabel.AutoSize = true;
        availableLabel.Font = new Font("Segoe UI", 9F);
        availableLabel.ForeColor = Color.FromArgb(99, 99, 83);
        availableLabel.Location = new Point(35, 287);
        availableLabel.Name = "availableLabel";
        availableLabel.Size = new Size(72, 20);
        availableLabel.TabIndex = 5;
        availableLabel.Text = "Available";
        // 
        // safetyLabel
        // 
        safetyLabel.Font = new Font("Segoe UI", 9F);
        safetyLabel.ForeColor = Color.FromArgb(126, 83, 70);
        safetyLabel.Location = new Point(33, 330);
        safetyLabel.Name = "safetyLabel";
        safetyLabel.Size = new Size(520, 46);
        safetyLabel.TabIndex = 6;
        safetyLabel.Text = "Check the address carefully. Slithy transfers cannot be reversed after they are sent.";
        // 
        // continueButton
        // 
        continueButton.BackColor = Color.FromArgb(83, 104, 79);
        continueButton.FlatStyle = FlatStyle.Flat;
        continueButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        continueButton.ForeColor = Color.White;
        continueButton.Location = new Point(393, 398);
        continueButton.Name = "continueButton";
        continueButton.Size = new Size(160, 45);
        continueButton.TabIndex = 7;
        continueButton.Text = "Review transfer";
        continueButton.UseVisualStyleBackColor = false;
        continueButton.Click += continueButton_Click;
        // 
        // cancelButton
        // 
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Location = new Point(229, 398);
        cancelButton.Name = "cancelButton";
        cancelButton.Size = new Size(145, 45);
        cancelButton.TabIndex = 8;
        cancelButton.Text = "Cancel";
        cancelButton.UseVisualStyleBackColor = true;
        // 
        // SendDialog
        // 
        AcceptButton = continueButton;
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(246, 241, 232);
        CancelButton = cancelButton;
        ClientSize = new Size(588, 470);
        Controls.Add(cancelButton);
        Controls.Add(continueButton);
        Controls.Add(safetyLabel);
        Controls.Add(availableLabel);
        Controls.Add(amountNumericUpDown);
        Controls.Add(amountLabel);
        Controls.Add(addressTextBox);
        Controls.Add(addressLabel);
        Controls.Add(headingLabel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "SendDialog";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Send Slithy";
        ((System.ComponentModel.ISupportInitialize)amountNumericUpDown).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }
}

