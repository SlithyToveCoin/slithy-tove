using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Slithy.Linux;

internal static class WindowChrome
{
    internal static Control Wrap(Window window, Control content, bool mainWindow = false)
    {
        if (!OperatingSystem.IsLinux()) return content;

        // WSLg can ignore the X11 window icon. Draw the title row with the same artwork on every Linux desktop.
        window.SystemDecorations = SystemDecorations.None;
        var title = Dialogs.Text(window.Title ?? "Slithy Tove", 13);
        title.TextWrapping = TextWrapping.NoWrap;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.VerticalAlignment = VerticalAlignment.Center;
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8, Margin = new Thickness(10, 0) };
        heading.Children.Add(new Image
        {
            Name = "SlithyTitlebarLogo", Source = Branding.ApplicationImage,
            Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(title, 1); heading.Children.Add(title);
        var drag = new Border { Background = Brushes.Transparent, Child = heading };
        drag.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(drag).Properties.IsLeftButtonPressed) return;
            if (mainWindow && window.CanResize && e.ClickCount == 2) ToggleMaximized(window);
            else window.BeginMoveDrag(e);
            e.Handled = true;
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        Button? maximize = null;
        if (mainWindow)
        {
            var minimize = CaptionButton("Minimize window", "M2,10 L12,10");
            minimize.Name = "MinimizeWindow";
            minimize.Click += (_, _) => window.WindowState = WindowState.Minimized;
            buttons.Children.Add(minimize);
            maximize = CaptionButton("Maximize window", "M2,2 L12,2 L12,12 L2,12 Z");
            maximize.Name = "MaximizeWindow";
            maximize.Click += (_, _) => ToggleMaximized(window);
            buttons.Children.Add(maximize);
        }
        var close = CaptionButton("Close window", "M3,3 L11,11 M11,3 L3,11");
        close.Name = "CloseWindow";
        close.Click += (_, _) => window.Close();
        buttons.Children.Add(close);
        var bar = new Grid { Name = "SlithyTitlebar", ColumnDefinitions = new ColumnDefinitions("*,Auto"), Height = 32, Background = Dialogs.Paper };
        bar.Children.Add(drag); Grid.SetColumn(buttons, 1); bar.Children.Add(buttons);
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        body.Children.Add(bar); Grid.SetRow(content, 1); body.Children.Add(content);
        var frame = new Border { BorderBrush = Brush.Parse("#bbc5b8"), BorderThickness = new Thickness(1), Child = body };
        var outer = new Grid(); outer.Children.Add(frame);
        var grips = new List<Control>();
        AddGrip(WindowEdge.North, HorizontalAlignment.Stretch, VerticalAlignment.Top, double.NaN, 4, StandardCursorType.SizeNorthSouth);
        AddGrip(WindowEdge.South, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, double.NaN, 4, StandardCursorType.SizeNorthSouth);
        AddGrip(WindowEdge.West, HorizontalAlignment.Left, VerticalAlignment.Stretch, 4, double.NaN, StandardCursorType.SizeWestEast);
        AddGrip(WindowEdge.East, HorizontalAlignment.Right, VerticalAlignment.Stretch, 4, double.NaN, StandardCursorType.SizeWestEast);
        AddGrip(WindowEdge.NorthWest, HorizontalAlignment.Left, VerticalAlignment.Top, 8, 8, StandardCursorType.TopLeftCorner);
        AddGrip(WindowEdge.NorthEast, HorizontalAlignment.Right, VerticalAlignment.Top, 8, 8, StandardCursorType.TopRightCorner);
        AddGrip(WindowEdge.SouthWest, HorizontalAlignment.Left, VerticalAlignment.Bottom, 8, 8, StandardCursorType.BottomLeftCorner);
        AddGrip(WindowEdge.SouthEast, HorizontalAlignment.Right, VerticalAlignment.Bottom, 8, 8, StandardCursorType.BottomRightCorner);
        window.PropertyChanged += (_, args) =>
        {
            if (args.Property == Window.TitleProperty) title.Text = window.Title;
            if (args.Property == Window.WindowStateProperty || args.Property == Window.CanResizeProperty) Refresh();
        };
        Refresh();
        return outer;

        void AddGrip(WindowEdge edge, HorizontalAlignment horizontal, VerticalAlignment vertical, double width, double height, StandardCursorType cursor)
        {
            var grip = new Border
            {
                Name = "WindowResize" + edge, Background = Brushes.Transparent,
                HorizontalAlignment = horizontal, VerticalAlignment = vertical,
                Width = width, Height = height, Cursor = new Cursor(cursor)
            };
            grip.PointerPressed += (_, e) =>
            {
                if (!window.CanResize || window.WindowState != WindowState.Normal || !e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;
                window.BeginResizeDrag(edge, e); e.Handled = true;
            };
            outer.Children.Add(grip); grips.Add(grip);
        }

        void Refresh()
        {
            bool normal = window.WindowState == WindowState.Normal;
            frame.BorderThickness = normal ? new Thickness(1) : new Thickness(0);
            foreach (Control grip in grips) grip.IsVisible = window.CanResize && normal;
            if (maximize != null)
            {
                maximize.IsEnabled = window.CanResize;
                string name = normal ? "Maximize window" : "Restore window";
                AutomationProperties.SetName(maximize, name); ToolTip.SetTip(maximize, name);
                maximize.Content = Symbol(normal ? "M2,2 L12,2 L12,12 L2,12 Z" : "M2,5 L9,5 L9,12 L2,12 Z M5,5 L5,2 L12,2 L12,9 L9,9");
            }
        }
    }

    private static void ToggleMaximized(Window window) =>
        window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private static Button CaptionButton(string name, string geometry)
    {
        var button = new Button
        {
            Content = Symbol(geometry), Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0), Width = 40, Height = 32, Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            IsTabStop = false
        };
        AutomationProperties.SetName(button, name); ToolTip.SetTip(button, name);
        return button;
    }

    private static Control Symbol(string geometry) => new Avalonia.Controls.Shapes.Path
    {
        Data = Geometry.Parse(geometry), Width = 14, Height = 14, Stroke = Dialogs.Ink, StrokeThickness = 1.4
    };
}
