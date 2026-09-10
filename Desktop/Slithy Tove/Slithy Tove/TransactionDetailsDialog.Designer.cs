//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Transaction Details Dialog Designer
//===============================================
#nullable disable

namespace Slithy_Tove;

// Visual Studio generated most of this file from the dialog designer.
partial class TransactionDetailsDialog
{
    private System.ComponentModel.IContainer components = null;
    private Label headingLabel;
    private TableLayoutPanel detailsTable;
    private Label directionCaptionLabel;
    private Label directionValueLabel;
    private Label amountCaptionLabel;
    private Label amountValueLabel;
    private Label feeCaptionLabel;
    private Label feeValueLabel;
    private Label heightCaptionLabel;
    private Label heightValueLabel;
    private Label dateCaptionLabel;
    private Label dateValueLabel;
    private Label hashLabel;
    private TextBox transactionHashTextBox;
    private Button copyHashButton;
    private GroupBox proofGroupBox;
    private Label recipientAddressLabel;
    private TextBox recipientAddressTextBox;
    private Label proofMessageLabel;
    private TextBox proofMessageTextBox;
    private Button createProofButton;
    private TextBox proofTextBox;
    private Button copyProofButton;
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
        detailsTable = new TableLayoutPanel();
        directionCaptionLabel = new Label();
        directionValueLabel = new Label();
        amountCaptionLabel = new Label();
        amountValueLabel = new Label();
        feeCaptionLabel = new Label();
        feeValueLabel = new Label();
        heightCaptionLabel = new Label();
        heightValueLabel = new Label();
        dateCaptionLabel = new Label();
        dateValueLabel = new Label();
        hashLabel = new Label();
        transactionHashTextBox = new TextBox();
        copyHashButton = new Button();
        proofGroupBox = new GroupBox();
        copyProofButton = new Button();
        proofTextBox = new TextBox();
        createProofButton = new Button();
        proofMessageTextBox = new TextBox();
        proofMessageLabel = new Label();
        recipientAddressTextBox = new TextBox();
        recipientAddressLabel = new Label();
        closeButton = new Button();
        detailsTable.SuspendLayout();
        proofGroupBox.SuspendLayout();
        SuspendLayout();
        headingLabel.AutoSize = true;
        headingLabel.Font = new Font("Georgia", 19F, FontStyle.Bold);
        headingLabel.ForeColor = Color.FromArgb(59, 70, 57);
        headingLabel.Location = new Point(27, 21);
        headingLabel.Name = "headingLabel";
        headingLabel.Size = new Size(319, 37);
        headingLabel.TabIndex = 0;
        headingLabel.Text = "Transaction details";
        detailsTable.ColumnCount = 2;
        detailsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
        detailsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
        detailsTable.Controls.Add(directionCaptionLabel, 0, 0);
        detailsTable.Controls.Add(directionValueLabel, 1, 0);
        detailsTable.Controls.Add(amountCaptionLabel, 0, 1);
        detailsTable.Controls.Add(amountValueLabel, 1, 1);
        detailsTable.Controls.Add(feeCaptionLabel, 0, 2);
        detailsTable.Controls.Add(feeValueLabel, 1, 2);
        detailsTable.Controls.Add(heightCaptionLabel, 0, 3);
        detailsTable.Controls.Add(heightValueLabel, 1, 3);
        detailsTable.Controls.Add(dateCaptionLabel, 0, 4);
        detailsTable.Controls.Add(dateValueLabel, 1, 4);
        detailsTable.Location = new Point(30, 74);
        detailsTable.Name = "detailsTable";
        detailsTable.RowCount = 5;
        detailsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        detailsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        detailsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        detailsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        detailsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        detailsTable.Size = new Size(570, 150);
        detailsTable.TabIndex = 1;
        directionCaptionLabel.Dock = DockStyle.Fill;
        directionCaptionLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        directionCaptionLabel.Name = "directionCaptionLabel";
        directionCaptionLabel.Text = "Direction";
        directionCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        directionValueLabel.Dock = DockStyle.Fill;
        directionValueLabel.Name = "directionValueLabel";
        directionValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        amountCaptionLabel.Dock = DockStyle.Fill;
        amountCaptionLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        amountCaptionLabel.Name = "amountCaptionLabel";
        amountCaptionLabel.Text = "Amount";
        amountCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        amountValueLabel.Dock = DockStyle.Fill;
        amountValueLabel.Name = "amountValueLabel";
        amountValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        feeCaptionLabel.Dock = DockStyle.Fill;
        feeCaptionLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        feeCaptionLabel.Name = "feeCaptionLabel";
        feeCaptionLabel.Text = "Fee";
        feeCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        feeValueLabel.Dock = DockStyle.Fill;
        feeValueLabel.Name = "feeValueLabel";
        feeValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        heightCaptionLabel.Dock = DockStyle.Fill;
        heightCaptionLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        heightCaptionLabel.Name = "heightCaptionLabel";
        heightCaptionLabel.Text = "Block height";
        heightCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        heightValueLabel.Dock = DockStyle.Fill;
        heightValueLabel.Name = "heightValueLabel";
        heightValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        dateCaptionLabel.Dock = DockStyle.Fill;
        dateCaptionLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        dateCaptionLabel.Name = "dateCaptionLabel";
        dateCaptionLabel.Text = "Date";
        dateCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        dateValueLabel.Dock = DockStyle.Fill;
        dateValueLabel.Name = "dateValueLabel";
        dateValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        hashLabel.AutoSize = true;
        hashLabel.Location = new Point(30, 241);
        hashLabel.Name = "hashLabel";
        hashLabel.Size = new Size(124, 20);
        hashLabel.TabIndex = 2;
        hashLabel.Text = "Transaction hash";
        transactionHashTextBox.Font = new Font("Cascadia Mono", 8.5F);
        transactionHashTextBox.Location = new Point(30, 266);
        transactionHashTextBox.Name = "transactionHashTextBox";
        transactionHashTextBox.ReadOnly = true;
        transactionHashTextBox.Size = new Size(460, 24);
        transactionHashTextBox.TabIndex = 3;
        copyHashButton.Location = new Point(500, 263);
        copyHashButton.Name = "copyHashButton";
        copyHashButton.Size = new Size(100, 31);
        copyHashButton.TabIndex = 4;
        copyHashButton.Text = "Copy";
        copyHashButton.UseVisualStyleBackColor = true;
        copyHashButton.Click += copyHashButton_Click;
        proofGroupBox.Controls.Add(copyProofButton);
        proofGroupBox.Controls.Add(proofTextBox);
        proofGroupBox.Controls.Add(createProofButton);
        proofGroupBox.Controls.Add(proofMessageTextBox);
        proofGroupBox.Controls.Add(proofMessageLabel);
        proofGroupBox.Controls.Add(recipientAddressTextBox);
        proofGroupBox.Controls.Add(recipientAddressLabel);
        proofGroupBox.Location = new Point(30, 315);
        proofGroupBox.Name = "proofGroupBox";
        proofGroupBox.Size = new Size(570, 260);
        proofGroupBox.TabIndex = 5;
        proofGroupBox.TabStop = false;
        proofGroupBox.Text = "Proof of payment";
        recipientAddressLabel.AutoSize = true;
        recipientAddressLabel.Location = new Point(18, 30);
        recipientAddressLabel.Name = "recipientAddressLabel";
        recipientAddressLabel.Size = new Size(126, 20);
        recipientAddressLabel.TabIndex = 0;
        recipientAddressLabel.Text = "Recipient address";
        recipientAddressTextBox.Font = new Font("Cascadia Mono", 8F);
        recipientAddressTextBox.Location = new Point(18, 54);
        recipientAddressTextBox.Name = "recipientAddressTextBox";
        recipientAddressTextBox.Size = new Size(534, 23);
        recipientAddressTextBox.TabIndex = 1;
        proofMessageLabel.AutoSize = true;
        proofMessageLabel.Location = new Point(18, 91);
        proofMessageLabel.Name = "proofMessageLabel";
        proofMessageLabel.Size = new Size(135, 20);
        proofMessageLabel.TabIndex = 2;
        proofMessageLabel.Text = "Optional message";
        proofMessageTextBox.Location = new Point(18, 115);
        proofMessageTextBox.Name = "proofMessageTextBox";
        proofMessageTextBox.Size = new Size(380, 27);
        proofMessageTextBox.TabIndex = 3;
        createProofButton.Location = new Point(408, 111);
        createProofButton.Name = "createProofButton";
        createProofButton.Size = new Size(144, 35);
        createProofButton.TabIndex = 4;
        createProofButton.Text = "Create proof";
        createProofButton.UseVisualStyleBackColor = true;
        createProofButton.Click += createProofButton_Click;
        proofTextBox.Font = new Font("Cascadia Mono", 8F);
        proofTextBox.Location = new Point(18, 158);
        proofTextBox.Multiline = true;
        proofTextBox.Name = "proofTextBox";
        proofTextBox.ReadOnly = true;
        proofTextBox.Size = new Size(430, 78);
        proofTextBox.TabIndex = 5;
        copyProofButton.Location = new Point(458, 178);
        copyProofButton.Name = "copyProofButton";
        copyProofButton.Size = new Size(94, 35);
        copyProofButton.TabIndex = 6;
        copyProofButton.Text = "Copy";
        copyProofButton.UseVisualStyleBackColor = true;
        copyProofButton.Click += copyProofButton_Click;
        closeButton.DialogResult = DialogResult.OK;
        closeButton.Location = new Point(470, 586);
        closeButton.Name = "closeButton";
        closeButton.Size = new Size(130, 44);
        closeButton.TabIndex = 6;
        closeButton.Text = "Close";
        closeButton.UseVisualStyleBackColor = true;
        AcceptButton = closeButton;
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(246, 241, 232);
        ClientSize = new Size(630, 650);
        Controls.Add(closeButton);
        Controls.Add(proofGroupBox);
        Controls.Add(copyHashButton);
        Controls.Add(transactionHashTextBox);
        Controls.Add(hashLabel);
        Controls.Add(detailsTable);
        Controls.Add(headingLabel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "TransactionDetailsDialog";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Transaction details";
        detailsTable.ResumeLayout(false);
        proofGroupBox.ResumeLayout(false);
        proofGroupBox.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

}

