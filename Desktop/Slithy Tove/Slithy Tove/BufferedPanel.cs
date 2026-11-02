namespace Slithy_Tove;

internal sealed class BufferedPanel : Panel
{
    public BufferedPanel() => DoubleBuffered = true;
}

internal sealed class BufferedTableLayoutPanel : TableLayoutPanel
{
    public BufferedTableLayoutPanel() => DoubleBuffered = true;
}
