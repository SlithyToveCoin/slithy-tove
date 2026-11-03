using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Slithy.Linux;

internal static class DesktopInstallUiSelfTest
{
    internal static int Run(string[] args)
    {
        string output = args.SkipWhile(a => a != "--screenshots").Skip(1).FirstOrDefault()
            ?? Path.Combine(Path.GetTempPath(), "slithy-install-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        foreach (int width in new[] { 480, 620 })
        foreach (bool uninstall in new[] { false, true })
        {
            var window = new DesktopInstallWindow(uninstall, preview: true) { Width = width, Height = 540 };
            window.Show(); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var action = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "InstallAction");
            if (action.Bounds.Width < 100 || action.Bounds.Height < 30 || !ReferenceEquals(window.Icon, Branding.WindowIcon))
                throw new InvalidOperationException("Installer control layout or icon is missing.");
            using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Installer frame missing.");
            frame.Save(Path.Combine(output, $"{(uninstall ? "uninstall" : "install")}-{width}.png"));
            window.Close(); Dispatcher.UIThread.RunJobs();
        }
        Console.WriteLine("PASS: graphical installation and removal layouts at 480 and 620 pixels. No files were installed.");
        return 0;
    }
}
