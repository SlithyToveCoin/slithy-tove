namespace Slithy_Tove;

public partial class Form1
{
    private TabPage miningSettingsTabPage = null!;
    private TrackBar miningThreadsTrackBar = null!;
    private Label miningThreadsValueLabel = null!;
    private Label miningThreadsHintLabel = null!;

    private void InitializeMiningSettings()
    {
        int Scale(int value) => (int)Math.Round(value * DeviceDpi / 96d);
        Color paper = Color.FromArgb(255, 253, 247);
        Color ink = Color.FromArgb(50, 58, 49);
        miningSettingsTabPage = new TabPage("Mining")
        {
            Name = "miningSettingsTabPage", BackColor = BackColor,
            Padding = new Padding(Scale(16)), AutoScroll = true
        };
        TableLayoutPanel card = new BufferedTableLayoutPanel
        {
            Name = "miningSettingsCard", Dock = DockStyle.Top, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = paper,
            Padding = new Padding(Scale(20)), ColumnCount = 1, RowCount = 0
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Label TextRow(string text, float size, FontStyle style = FontStyle.Regular) => new()
        {
            Text = text, AutoSize = true, Dock = DockStyle.Top,
            Font = new Font("Segoe UI", size, style), ForeColor = ink,
            Margin = new Padding(0, 0, 0, Scale(8))
        };
        void AddRow(Control control)
        {
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.Controls.Add(control, 0, card.RowCount++);
        }
        AddRow(TextRow("CPU mining", 16, FontStyle.Bold));
        AddRow(TextRow("Choose how many CPU threads Slithy can use. Fewer threads leave more room for your other apps.", 10));
        miningThreadsValueLabel = TextRow("", 11, FontStyle.Bold);
        miningThreadsValueLabel.Name = "miningThreadsValueLabel";
        AddRow(miningThreadsValueLabel);
        miningThreadsTrackBar = new TrackBar
        {
            Name = "miningThreadsTrackBar", Minimum = 1, Maximum = Math.Max(1, Environment.ProcessorCount),
            Value = 1, SmallChange = 1, LargeChange = 1, TickStyle = TickStyle.None,
            Dock = DockStyle.Top, Height = Scale(32), AutoSize = false, BackColor = paper,
            Margin = new Padding(0, 0, 0, Scale(8)), TabIndex = 0,
            AccessibleName = "CPU mining threads",
            AccessibleDescription = "Choose an exact thread count. Use the arrow keys to change by one. Changes apply the next time you start mining."
        };
        miningThreadsTrackBar.ValueChanged += MiningThreadsChanged;
        AddRow(miningThreadsTrackBar);
        AddRow(TextRow($"1 thread to {Math.Max(1, Environment.ProcessorCount)} supported by this computer. Use the arrow keys to change by one.", 9));
        miningThreadsHintLabel = TextRow("Changes apply the next time you start mining.", 10);
        miningThreadsHintLabel.Name = "miningThreadsHintLabel";
        AddRow(miningThreadsHintLabel);
        AddRow(TextRow("More threads can increase heat, fan noise and electricity use. You can stop mining from Home at any time.", 10));
        card.SizeChanged += (_, _) =>
        {
            int textWidth = Math.Max(1, card.ClientSize.Width - card.Padding.Horizontal);
            foreach (Label label in card.Controls.OfType<Label>()) label.MaximumSize = new Size(textWidth, 0);
            ApplyRoundedRegion(card, Scale(14));
        };
        miningSettingsTabPage.Controls.Add(card);
        advancedTabControl.TabPages.Insert(1, miningSettingsTabPage);
    }

    private void MiningThreadsChanged(object? sender, EventArgs e)
    {
        if (_loadingSettingsIntoControls) return;
        _settings.MiningThreads = miningThreadsTrackBar.Value;
        _settingsStore.Save(_settings);
        UpdateMiningThreadLabels();
    }

    private void UpdateMiningThreadLabels()
    {
        int count = MiningThreadCount();
        miningThreadsValueLabel.Text = $"{count} of {Math.Max(1, Environment.ProcessorCount)} CPU threads";
        miningThreadsHintLabel.Text = _miningStartedByApp
            ? "Mining is running. Your saved choice applies after you stop and start mining again."
            : "Changes apply the next time you start mining.";
        miningSettingsLink.Text = _miningStartedByApp
            ? $"Next start: {count} thread{(count == 1 ? "" : "s")}. Settings"
            : $"{count} CPU thread{(count == 1 ? "" : "s")}. Change in Settings";
    }

    private void ShowMiningSettings()
    {
        mainTabControl.SelectedTab = advancedTabPage;
        advancedTabControl.SelectedTab = miningSettingsTabPage;
        miningThreadsTrackBar.Focus();
    }
}
