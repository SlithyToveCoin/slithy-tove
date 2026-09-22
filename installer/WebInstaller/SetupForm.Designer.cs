#nullable enable
namespace Slithy_Tove;

partial class SetupForm
{
    private System.ComponentModel.IContainer? components;
    private PictureBox artwork = null!;
    private Label heading = null!;
    private Label description = null!;
    private Label status = null!;
    private ProgressBar progress = null!;
    private Button install = null!;
    private Button cancel = null!;
    private TableLayoutPanel layout = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
            artwork.Image?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        artwork = new PictureBox();
        heading = new Label();
        description = new Label();
        status = new Label();
        progress = new ProgressBar();
        install = new Button();
        cancel = new Button();
        layout = new TableLayoutPanel();
        SuspendLayout();
        layout.Dock = DockStyle.Fill;
        layout.Padding = new Padding(24);
        layout.ColumnCount = 2;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        layout.RowCount = 6;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        heading.Text = "Slithy Tove";
        heading.Font = new Font("Georgia", 27F, FontStyle.Bold);
        heading.Dock = DockStyle.Fill;
        description.Text = "A wallet and CPU miner that helps support children's literacy.\n\nSetup downloads the current signed beta release. Internet access is required. Test balances do not carry into the live network.";
        description.Dock = DockStyle.Fill;
        status.Text = "Ready to download. Your wallet files stay on this computer.";
        status.Dock = DockStyle.Fill;
        progress.Dock = DockStyle.Fill;
        progress.MarqueeAnimationSpeed = 30;
        install.Text = "Download and install";
        install.Dock = DockStyle.Fill;
        install.BackColor = Color.FromArgb(65, 99, 77);
        install.ForeColor = Color.White;
        install.FlatStyle = FlatStyle.Flat;
        install.Click += Install_Click;
        cancel.Text = "Cancel";
        cancel.Dock = DockStyle.Fill;
        cancel.Click += (_, _) => { if (_installing) _cancel.Cancel(); else Close(); };
        layout.Controls.Add(heading, 0, 0);
        layout.SetColumnSpan(heading, 2);
        layout.Controls.Add(description, 0, 1);
        layout.SetColumnSpan(description, 2);
        layout.Controls.Add(status, 0, 2);
        layout.SetColumnSpan(status, 2);
        layout.Controls.Add(progress, 0, 3);
        layout.SetColumnSpan(progress, 2);
        layout.Controls.Add(install, 0, 5);
        layout.Controls.Add(cancel, 1, 5);
        artwork.Dock = DockStyle.Left;
        artwork.Width = 190;
        artwork.SizeMode = PictureBoxSizeMode.Zoom;
        artwork.BackColor = Color.FromArgb(45, 67, 55);
        Controls.Add(layout);
        Controls.Add(artwork);
        Text = "Install Slithy Tove";
        Font = new Font("Segoe UI", 11F);
        ForeColor = Color.FromArgb(42, 48, 41);
        BackColor = Color.FromArgb(247, 243, 232);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(800, 450);
        MinimumSize = new Size(800, 450);
        StartPosition = FormStartPosition.CenterScreen;
        FormClosing += SetupForm_FormClosing;
        ResumeLayout(false);
    }
}
