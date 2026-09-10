//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Book Mining Animation
//===============================================
namespace Slithy_Tove;

// Custom little drawing control for the book animation on the mining card.
internal sealed class BookMiningAnimation : Control
{
    private readonly System.Windows.Forms.Timer _timer;
    private int _frame;
    private bool _mining;

    public BookMiningAnimation()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        _timer = new System.Windows.Forms.Timer { Interval = 120 };
        _timer.Tick += (_, _) =>
        {
            _frame = (_frame + 1) % 24;
            Invalidate();
        };
    }

    public bool Mining
    {
        get => _mining;
        set
        {
            if (_mining == value)
            {
                return;
            }

            _mining = value;
            if (value)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
                _frame = 0;
            }
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // Draws the book, sparkle dots, and small motion changes while mining is on.
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        float scale = Math.Min(Width / 92f, Height / 72f);
        e.Graphics.TranslateTransform((Width - (92 * scale)) / 2f, (Height - (72 * scale)) / 2f);
        e.Graphics.ScaleTransform(scale, scale);

        using Pen bookPen = new(Color.FromArgb(61, 91, 70), 3f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };
        using SolidBrush pageBrush = new(Color.FromArgb(249, 240, 215));

        PointF[] leftPage =
        [
            new(8, 20), new(25, 15), new(44, 22), new(44, 59), new(25, 52), new(8, 57)
        ];
        PointF[] rightPage =
        [
            new(44, 22), new(63, 15), new(80, 20), new(80, 57), new(63, 52), new(44, 59)
        ];
        e.Graphics.FillPolygon(pageBrush, leftPage);
        e.Graphics.FillPolygon(pageBrush, rightPage);
        e.Graphics.DrawPolygon(bookPen, leftPage);
        e.Graphics.DrawPolygon(bookPen, rightPage);
        e.Graphics.DrawLine(bookPen, 44, 22, 44, 59);

        using Pen linePen = new(Color.FromArgb(126, 139, 105), 1.6f);
        e.Graphics.DrawLine(linePen, 16, 29, 35, 27);
        e.Graphics.DrawLine(linePen, 16, 37, 35, 35);
        e.Graphics.DrawLine(linePen, 53, 27, 72, 29);
        e.Graphics.DrawLine(linePen, 53, 35, 72, 37);

        if (_mining)
        {
            float rise = (_frame % 12) * 2.2f;
            float glow = 1f - ((_frame % 12) / 12f);
            using SolidBrush sparkBrush = new(Color.FromArgb((int)(220 * glow), 229, 139, 65));
            DrawSpark(e.Graphics, 31, 17 - rise, 4.5f, sparkBrush);
            DrawSpark(e.Graphics, 58, 10 - ((rise + 10) % 26), 3.5f, sparkBrush);

            float pageLift = (float)Math.Sin(_frame * Math.PI / 12) * 5f;
            using Pen pagePen = new(Color.FromArgb(79, 113, 86), 2f);
            e.Graphics.DrawBezier(pagePen, 44, 23, 51, 17 - pageLift, 58, 17 - pageLift, 64, 20);
        }
    }

    private static void DrawSpark(Graphics graphics, float x, float y, float radius, Brush brush)
    {
        PointF[] points =
        [
            new(x, y - radius),
            new(x + radius * .35f, y - radius * .35f),
            new(x + radius, y),
            new(x + radius * .35f, y + radius * .35f),
            new(x, y + radius),
            new(x - radius * .35f, y + radius * .35f),
            new(x - radius, y),
            new(x - radius * .35f, y - radius * .35f)
        ];
        graphics.FillPolygon(brush, points);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }
        base.Dispose(disposing);
    }
}

