using System.Reflection;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace Slithy.Linux;

internal static class Branding
{
    // This ID also belongs to the Linux application-menu entry.
    internal const string DesktopId = "io.slithy.Tove";
    internal const string Release = "0.1.44-beta.2";
    private static readonly Lazy<Bitmap> AppImage = new(() => LoadImage("Slithy.AppIcon"));
    private static readonly Lazy<Bitmap> MarkImage = new(() => LoadImage("Slithy.HeaderMark"));
    private static readonly Lazy<WindowIcon> IconImage = new(() =>
    {
        using Stream stream = Open("Slithy.AppIcon");
        return new WindowIcon(stream);
    });

    internal static Bitmap ApplicationImage => AppImage.Value;
    internal static Bitmap HeaderMark => MarkImage.Value;
    internal static WindowIcon WindowIcon => IconImage.Value;

    private static Stream Open(string name) => Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
        ?? throw new InvalidOperationException("The bundled Slithy artwork is missing. Extract the complete application package again.");

    private static Bitmap LoadImage(string name)
    {
        using Stream stream = Open(name);
        return new Bitmap(stream);
    }
}
