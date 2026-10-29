using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Slithy.Linux;

internal static class Program
{
    internal static bool Preview;
    internal static bool Regtest;
    internal static string[] StartupArguments = Array.Empty<string>();

    [STAThread]
    public static int Main(string[] args)
    {
        StartupArguments = args;
        Preview = args.Contains("--preview");
        Regtest = args.Contains("--regtest");
        if (args.Contains("--update-test")) return LinuxGuiUpdateSelfTest.RunAsync().GetAwaiter().GetResult();
        if (args.Contains("--self-test"))
        {
            return LinuxRuntimeSelfTest.RunAsync().GetAwaiter().GetResult();
        }
        if (args.Contains("--integration-test")) return IntegrationSelfTest.RunAsync().GetAwaiter().GetResult();
        if (args.Contains("--ui-test"))
        {
            Preview = true;
            Build().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
            return UiSelfTest.Run(args);
        }
        Build().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    internal static AppBuilder Build() => AppBuilder.Configure<App>().UsePlatformDetect()
        .With(new X11PlatformOptions { WmClass = Branding.DesktopId });
}

internal sealed class App : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        var theme = new FluentTheme();
        theme.Palettes[ThemeVariant.Light] = new ColorPaletteResources
        {
            Accent = Color.Parse("#3c654f"),
            RegionColor = Color.Parse("#f3efe4")
        };
        Styles.Add(theme);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow(Program.Preview, Program.Regtest);
        base.OnFrameworkInitializationCompleted();
    }
}
