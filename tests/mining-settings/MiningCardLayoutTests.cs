using Slithy_Tove;

internal static partial class Program
{
    private static void TestMiningCardLayout(Form1 form, string? screenshots)
    {
        TabControl tabs = (TabControl)Field(form, "mainTabControl");
        Panel card = (Panel)Field(form, "miningCard");
        Label title = (Label)Field(form, "miningTitleLabel");
        Label description = (Label)Field(form, "miningDescriptionLabel");
        Label status = (Label)Field(form, "miningStatusLineLabel");
        CheckBox custom = (CheckBox)Field(form, "mineToAddressCheckBox");
        Button toggle = (Button)Field(form, "miningToggleButton");
        Size originalSize = form.ClientSize;
        Size originalMinimum = form.MinimumSize;
        object? originalReward = typeof(Form1).GetField("_lastMiningRewardUtc", Hidden)!.GetValue(form);
        bool originalCustom = custom.Checked;
        bool originalMining = (bool)Field(form, "_miningStartedByApp");
        Control[] textControls = [title, description, status,
            (Control)Field(form, "miningSettingsLink"), custom,
            (Control)Field(form, "miningAddressLabel"), toggle];
        var originalFonts = textControls.ToDictionary(control => control, control => control.Font);
        var testFonts = new List<Font>();

        void SetMining(bool active)
        {
            Set(form, "_miningStartedByApp", active);
            Call(form, "UpdateMiningThreadLabels");
        }

        void Verify(string scenario)
        {
            card.AutoScrollPosition = Point.Empty;
            Application.DoEvents();
            Label[] wrapped = [title, description, status, (Label)Field(form, "miningSettingsLink")];
            Check(wrapped.All(label => label.Height >= label.GetPreferredSize(new Size(label.Width, 0)).Height),
                "Mining text has its full wrapped height: " + scenario);
            int longestHeadingWord = title.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => TextRenderer.MeasureText(word, title.Font, Size.Empty,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width).DefaultIfEmpty().Max();
            Check(title.Width >= Math.Min(longestHeadingWord, description.Width),
                "Mining heading has room for whole words: " + scenario);
            Control[] visible = card.Controls.Cast<Control>().Where(control => control.Visible).ToArray();
            Check(visible.All(control => control.Left >= 0 && control.Right <= card.ClientSize.Width),
                "Mining controls fit the card width: " + scenario);
            bool overlap = visible.SelectMany((left, index) => visible.Skip(index + 1)
                .Select(right => left.Bounds.IntersectsWith(right.Bounds))).Any(value => value);
            Check(!overlap, "Mining controls do not overlap: " + scenario);
            Check(card.AutoScroll && card.DisplayRectangle.Bottom >= status.Bottom,
                "Mining scroll area includes the complete status: " + scenario);
            card.ScrollControlIntoView(status);
            Application.DoEvents();
            if (status.Height <= card.ClientSize.Height)
            {
                Check(status.Top >= 0 && status.Bottom <= card.ClientSize.Height,
                    "Mining status can be brought fully into view: " + scenario);
            }
            else
            {
                Check(status.Top >= 0 && status.Top < card.ClientSize.Height,
                    "The beginning of a long mining status is reachable: " + scenario);
                int logicalBottom = status.Bottom - card.AutoScrollPosition.Y;
                card.AutoScrollPosition = new Point(0, logicalBottom - card.ClientSize.Height);
                Application.DoEvents();
                Check(status.Bottom > 0 && status.Bottom <= card.ClientSize.Height,
                    "The end of a long mining status is reachable: " + scenario);
            }
            card.AutoScrollPosition = Point.Empty;
            Application.DoEvents();
        }

        try
        {
            form.MinimumSize = Size.Empty;
            tabs.SelectedIndex = 0;
            foreach (Size size in new[] { new Size(1495, 1006), new Size(1000, 780) })
            {
                form.ClientSize = size;
                Application.DoEvents();
                foreach (bool useCustom in new[] { false, true })
                {
                    custom.Checked = useCustom;
                    string suffix = $"{size.Width}px, {(useCustom ? "external address" : "wallet address")}";
                    SetMining(false);
                    Call(form, "ShowMiningStopped");
                    Verify("stopped, " + suffix);
                    Check(((Control)Field(form, "miningAddressLabel")).Visible == useCustom &&
                        ((Control)Field(form, "miningAddressTextBox")).Visible == useCustom,
                        "Custom address rows follow the mining destination: " + suffix);
                    Call(form, "ShowMiningStarting");
                    Verify("starting, " + suffix);
                    description.Text = "Starting local mining node, synced to block 12,345,678,901.";
                    Verify("long sync status, " + suffix);
                    SetMining(true);
                    Set(form, "_lastMiningRewardUtc", new DateTimeOffset(2026, 11, 2, 23, 59, 0, TimeSpan.Zero));
                    Call(form, "ShowMiningStarted", 256, 0UL);
                    Verify("waiting for a block, " + suffix);
                    Call(form, "ShowMiningStarted", 256, 12_345_678_901UL);
                    Verify("hash rate and last reward, " + suffix);
                    if (size.Width == 1495)
                    {
                        Console.WriteLine($"LAYOUT {suffix}: card={card.ClientSize.Height}, statusBottom={status.Bottom}, descriptionHeight={description.Height}, statusHeight={status.Height}");
                        if (screenshots != null)
                            Capture(form, Path.Combine(screenshots, useCustom ? "mining-active-custom-1495.png" : "mining-active-wallet-1495.png"));
                    }
                    Check(status.Text.Contains("Last reward:") && status.Text.Contains("100 blocks"),
                        "Reward time and maturity text remain present: " + suffix);
                    title.Text = "Stop not confirmed";
                    description.Text = "The local node did not answer. Slithy will check again.";
                    toggle.Text = "Stop mining";
                    Verify("stop not confirmed, " + suffix);
                    title.Text = "Checking miner";
                    description.Text = "The local node did not answer. Mining state is unknown.";
                    status.Text = "Slithy will check again. You can still request a stop.";
                    Verify("unknown mining state, " + suffix);
                    if (screenshots != null && useCustom)
                        Capture(form, Path.Combine(screenshots, $"mining-status-{size.Width}.png"));
                }
            }

            foreach (Control control in textControls)
            {
                Font larger = new(control.Font.FontFamily, control.Font.Size * 1.35F, control.Font.Style);
                testFonts.Add(larger);
                control.Font = larger;
            }
            SetMining(true);
            Call(form, "ShowMiningStarted", 256, 12_345_678_901UL);
            Verify("larger fonts in a compact window");
            if (screenshots != null)
            {
                card.ScrollControlIntoView(status);
                Application.DoEvents();
                Capture(form, Path.Combine(screenshots, "mining-status-large-fonts.png"));
                card.AutoScrollPosition = Point.Empty;
                Application.DoEvents();
            }

            int movement = 0;
            EventHandler changed = (_, _) => movement++;
            foreach (Control control in card.Controls)
            {
                control.LocationChanged += changed;
                control.SizeChanged += changed;
            }
            try
            {
                for (int repeat = 0; repeat < 25; repeat++)
                {
                    Call(form, "ShowMiningStarted", 256, 12_345_678_901UL);
                    Call(form, "LayoutMiningCard");
                }
                Application.DoEvents();
                Check(movement == 0, "Unchanged mining reports do not move or resize the card controls");
            }
            finally
            {
                foreach (Control control in card.Controls)
                {
                    control.LocationChanged -= changed;
                    control.SizeChanged -= changed;
                }
            }
        }
        finally
        {
            foreach ((Control control, Font font) in originalFonts) control.Font = font;
            foreach (Font font in testFonts) font.Dispose();
            typeof(Form1).GetField("_lastMiningRewardUtc", Hidden)!.SetValue(form, originalReward);
            SetMining(originalMining);
            custom.Checked = originalCustom;
            Call(form, "ShowMiningStopped");
            card.AutoScrollPosition = Point.Empty;
            form.ClientSize = originalSize;
            form.MinimumSize = originalMinimum;
            Application.DoEvents();
        }
    }
}
