//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Receive Dialog Designer
//===============================================
#nullable disable

namespace Slithy_Tove;

// Visual Studio generated most of this file from the dialog designer.
partial class ReceiveDialog
{
    private System.ComponentModel.IContainer components = null;
    private Label headingLabel;
    private Label descriptionLabel;
    private PictureBox qrPictureBox;
    private TextBox addressTextBox;
    private Button copyButton;
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
        qrPictureBox = new PictureBox();
        addressTextBox = new TextBox();
        copyButton = new Button();
        closeButton = new Button();
        ((System.ComponentModel.ISupportInitialize)qrPictureBox).BeginInit();
        SuspendLayout();
        // 
        // headingLabel
        // 
        headingLabel.AutoSize = true;
        headingLabel.Font = new Font("Georgia", 20F, FontStyle.Bold);
        headingLabel.ForeColor = Color.FromArgb(59, 70, 57);
        headingLabel.Location = new Point(30, 24);
        headingLabel.Name = "headingLabel";
        headingLabel.Size = new Size(239, 39);
        headingLabel.TabIndex = 0;
        headingLabel.Text = "Receive Slithy";
        // 
        // descriptionLabel
        // 
        descriptionLabel.Font = new Font("Segoe UI", 10F);
        descriptionLabel.ForeColor = Color.FromArgb(102, 98, 83);
        descriptionLabel.Location = new Point(32, 73);
        descriptionLabel.Name = "descriptionLabel";
        descriptionLabel.Size = new Size(470, 48);
        descriptionLabel.TabIndex = 1;
        descriptionLabel.Text = "Let someone scan this code, or copy your address and send it to them.";
        // 
        // qrPictureBox
        // 
        qrPictureBox.BackColor = Color.White;
        qrPictureBox.BorderStyle = BorderStyle.FixedSingle;
        qrPictureBox.Location = new Point(137, 128);
        qrPictureBox.Name = "qrPictureBox";
        qrPictureBox.Size = new Size(260, 260);
        qrPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        qrPictureBox.TabIndex = 2;
        qrPictureBox.TabStop = false;
        // 
        // addressTextBox
        // 
        addressTextBox.Font = new Font("Cascadia Mono", 8.5F);
        addressTextBox.Location = new Point(32, 410);
        addressTextBox.Multiline = true;
        addressTextBox.Name = "addressTextBox";
        addressTextBox.ReadOnly = true;
        addressTextBox.Size = new Size(470, 66);
        addressTextBox.TabIndex = 3;
        // 
        // copyButton
        // 
        copyButton.BackColor = Color.FromArgb(83, 104, 79);
        copyButton.FlatStyle = FlatStyle.Flat;
        copyButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        copyButton.ForeColor = Color.White;
        copyButton.Location = new Point(32, 495);
        copyButton.Name = "copyButton";
        copyButton.Size = new Size(225, 45);
        copyButton.TabIndex = 4;
        copyButton.Text = "Copy address";
        copyButton.UseVisualStyleBackColor = false;
        copyButton.Click += copyButton_Click;
        // 
        // closeButton
        // 
        closeButton.DialogResult = DialogResult.OK;
        closeButton.Location = new Point(277, 495);
        closeButton.Name = "closeButton";
        closeButton.Size = new Size(225, 45);
        closeButton.TabIndex = 5;
        closeButton.Text = "Close";
        closeButton.UseVisualStyleBackColor = true;
        // 
        // ReceiveDialog
        // 
        AcceptButton = closeButton;
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(246, 241, 232);
        ClientSize = new Size(535, 565);
        Controls.Add(closeButton);
        Controls.Add(copyButton);
        Controls.Add(addressTextBox);
        Controls.Add(qrPictureBox);
        Controls.Add(descriptionLabel);
        Controls.Add(headingLabel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ReceiveDialog";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Receive Slithy";
        ((System.ComponentModel.ISupportInitialize)qrPictureBox).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }
}

