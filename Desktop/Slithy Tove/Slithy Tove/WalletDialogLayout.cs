namespace Slithy_Tove;

internal static class WalletDialogLayout
{
    internal static TableLayoutPanel Page(Form form, string title, int width = 760, int height = 650)
    {
        form.Text = title;
        form.Font = new Font("Segoe UI", 10F);
        form.BackColor = Color.FromArgb(246, 241, 232);
        form.ForeColor = Color.FromArgb(50, 58, 49);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.StartPosition = FormStartPosition.CenterParent;
        form.MinimizeBox = false;
        float scale = Math.Min(1.4F, form.DeviceDpi / 96F);
        Rectangle screen = Screen.FromControl(form).WorkingArea;
        int maximumHeight = Math.Min(1050, screen.Height - 120);
        form.ClientSize = new Size(Math.Min(Math.Min(1100, (int)(width * scale)), screen.Width - 80), Math.Min((int)(height * scale), maximumHeight));
        form.MinimumSize = new Size(Math.Min((int)(620 * scale), screen.Width - 80), Math.Min((int)(480 * scale), screen.Height - 120));
        using Stream? icon = typeof(Form1).Assembly.GetManifestResourceStream("Slithy_Tove.Assets.slithy.ico");
        if (icon is not null) form.Icon = new Icon(icon);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24) };
        var page = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(0), BackColor = Color.FromArgb(255, 253, 247) };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scroll.Controls.Add(page);
        page.SizeChanged += (_, _) =>
        {
            foreach (Control child in page.Controls)
                if (child is Label or CheckBox) child.MaximumSize = new Size(Math.Max(120, page.ClientSize.Width - 24), 0);
        };
        form.Controls.Add(scroll);
        form.Shown += (_, _) =>
        {
            page.PerformLayout();
            form.ClientSize = new Size(form.ClientSize.Width, Math.Min(maximumHeight, page.PreferredSize.Height + 95));
        };
        Add(page, new Label { Text = title, AutoSize = true, Font = new Font("Georgia", 21F, FontStyle.Bold), Margin = new Padding(12, 10, 12, 18) });
        return page;
    }

    internal static void Add(TableLayoutPanel page, Control control)
    {
        int row = page.RowCount++;
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        if (control is Label label) label.AutoSize = true;
        control.Margin = new Padding(12, 6, 12, 8);
        page.Controls.Add(control, 0, row);
    }

    internal static Label Note(string text) => new() { Text = text, AutoSize = true, MaximumSize = new Size(660, 0) };
    internal static TextBox Readable(string text, int height = 64) => new() { Text = text, ReadOnly = true, Multiline = true, Height = height, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10F) };
    internal static Button Button(string text, EventHandler? click = null)
    {
        Button button = new() { Text = text, AutoSize = true, MinimumSize = new Size(110, 38), Padding = new Padding(10, 4, 10, 4), BackColor = Color.FromArgb(239, 226, 198), FlatStyle = FlatStyle.Flat };
        button.FlatAppearance.BorderSize = 0;
        if (click is not null) button.Click += click;
        return button;
    }
    internal static FlowLayoutPanel Actions(params Control[] controls)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        foreach (Button button in controls.OfType<Button>())
        {
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(110, 38);
            button.Padding = new Padding(10, 4, 10, 4);
        }
        panel.Controls.AddRange(controls);
        return panel;
    }
}
