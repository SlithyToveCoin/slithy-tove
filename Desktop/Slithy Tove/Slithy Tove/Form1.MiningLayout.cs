namespace Slithy_Tove;

public partial class Form1
{
    private bool _layingOutMiningCard;

    private void InitializeMiningCardLayout()
    {
        Control[] textRows = [miningTitleLabel, miningDescriptionLabel, miningSettingsLink,
            mineToAddressCheckBox, miningAddressLabel, miningToggleButton, miningStatusLineLabel];
        foreach (Control control in textRows)
        {
            control.AutoSize = false;
            control.TextChanged += (_, _) => LayoutMiningCard();
            control.FontChanged += (_, _) => LayoutMiningCard();
        }
        miningAddressTextBox.FontChanged += (_, _) => LayoutMiningCard();
        miningCard.ClientSizeChanged += (_, _) => LayoutMiningCard();
        miningCard.DpiChangedAfterParent += (_, _) => LayoutMiningCard();
        LayoutMiningCard();
    }

    private void LayoutMiningCard()
    {
        if (_layingOutMiningCard) return;
        _layingOutMiningCard = true;
        try
        {
            // Wrap the text first, then place the next row below it.
            // A scrollbar can change the available width, so measure again if it appears.
            for (int pass = 0; pass < 3; pass++)
            {
                int Scale(int value) => Math.Max(1, (int)Math.Round(value * DeviceDpi / 192d));
                int clientWidth = miningCard.ClientSize.Width;
                int padding = Scale(44);
                int width = Math.Max(1, clientWidth - padding * 2);
                int gap = Scale(8);
                int scrollY = miningCard.AutoScrollPosition.Y;
                int top = Scale(28);
                int Height(Control row, int rowWidth) => Math.Max(row.Font.Height,
                    row.GetPreferredSize(new Size(rowWidth, 0)).Height);
                void Place(Control row, int rowTop, int rowHeight) =>
                    row.SetBounds(padding, rowTop + scrollY, width, rowHeight);

                miningCard.SuspendLayout();
                try
                {
                    int bookWidth = Scale(64);
                    int bookHeight = Scale(52);
                    int headingWidth = miningTitleLabel.GetPreferredSize(Size.Empty).Width;
                    bool showBook = headingWidth + bookWidth + gap <= width;
                    _bookMiningAnimation.Visible = showBook;
                    int titleWidth = showBook ? width - bookWidth - gap : width;
                    int titleHeight = Height(miningTitleLabel, titleWidth);
                    miningTitleLabel.SetBounds(padding, top + scrollY, titleWidth, titleHeight);
                    _bookMiningAnimation.SetBounds(clientWidth - padding - bookWidth,
                        top + scrollY, bookWidth, bookHeight);
                    top += Math.Max(titleHeight, showBook ? bookHeight : 0) + gap;

                    // Reserve two lines so ordinary start/stop messages do not shift the button.
                    int descriptionHeight = Math.Max(miningDescriptionLabel.Font.Height * 2,
                        Height(miningDescriptionLabel, width));
                    Place(miningDescriptionLabel, top, descriptionHeight);
                    top += descriptionHeight + gap;
                    int linkHeight = Height(miningSettingsLink, width);
                    Place(miningSettingsLink, top, linkHeight);
                    top += linkHeight + gap;
                    int checkboxHeight = Height(mineToAddressCheckBox, width);
                    Place(mineToAddressCheckBox, top, checkboxHeight);
                    top += checkboxHeight + gap;

                    if (mineToAddressCheckBox.Checked)
                    {
                        int addressLabelHeight = Height(miningAddressLabel, width);
                        Place(miningAddressLabel, top, addressLabelHeight);
                        top += addressLabelHeight + Scale(4);
                        int addressHeight = miningAddressTextBox.PreferredHeight;
                        Place(miningAddressTextBox, top, addressHeight);
                        top += addressHeight + gap;
                    }

                    int buttonHeight = Math.Max(Scale(64), Height(miningToggleButton, width));
                    Place(miningToggleButton, top, buttonHeight);
                    top += buttonHeight + gap;
                    int statusHeight = Height(miningStatusLineLabel, width);
                    Place(miningStatusLineLabel, top, statusHeight);
                    Size scrollSize = new(0, top + statusHeight + Scale(24));
                    if (miningCard.AutoScrollMinSize != scrollSize) miningCard.AutoScrollMinSize = scrollSize;
                }
                finally { miningCard.ResumeLayout(true); }
                if (miningCard.ClientSize.Width == clientWidth) break;
            }
        }
        finally { _layingOutMiningCard = false; }
    }
}
