using System.ComponentModel;

namespace Slithy_Tove;

// Paint the moving segment inside the bar. Moving child controls on every frame
// caused unnecessary layout and repainting in the footer.
internal sealed class LoadingProgressTrack : Panel
{
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 40 };
    private bool _indeterminate;
    private int _frame;
    private bool _disposing;
    private readonly List<Control> _ancestors = [];

    public LoadingProgressTrack()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        _animation.Tick += (_, _) =>
        {
            if (!Visible) { _animation.Stop(); return; }
            _frame = (_frame + 1) % 80;
            Invalidate();
        };
        AccessibleRole = AccessibleRole.ProgressBar;
        AccessibleName = "Wallet loading progress";
        TabStop = false;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Indeterminate
    {
        get => _indeterminate;
        set
        {
            if (_indeterminate == value) return;
            _indeterminate = value;
            _frame = 0;
            UpdateAnimation();
            Invalidate();
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateAnimation();
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        WatchAncestors();
    }

    private void AncestorChanged(object? sender, EventArgs e) => WatchAncestors();

    private void WatchAncestors()
    {
        foreach (Control ancestor in _ancestors)
        {
            ancestor.VisibleChanged -= AncestorChanged;
            ancestor.ParentChanged -= AncestorChanged;
        }
        _ancestors.Clear();
        if (_disposing) return;
        for (Control? ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            _ancestors.Add(ancestor);
            ancestor.VisibleChanged += AncestorChanged;
            ancestor.ParentChanged += AncestorChanged;
        }
        UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        if (!_disposing) _animation.Enabled = _indeterminate && Visible && !IsDisposed;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!_indeterminate || ClientSize.Width == 0) return;
        int segment = Math.Max(12, ClientSize.Width / 3);
        double position = (1 - Math.Cos(_frame * Math.PI / 40)) / 2;
        int left = (int)Math.Round((ClientSize.Width - segment) * position);
        using SolidBrush brush = new(Color.FromArgb(48, 68, 56));
        e.Graphics.FillRectangle(brush, left, 0, segment, ClientSize.Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposing = true;
            WatchAncestors();
            _animation.Dispose();
        }
        base.Dispose(disposing);
    }
}
