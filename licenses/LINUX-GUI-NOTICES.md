# Linux desktop dependencies

The Linux GUI includes these components in addition to the shared wallet
dependencies listed in DOTNET-NOTICES.md. Preserve the accompanying license
texts when redistributing the application.

| Component | Version | License and source |
| --- | --- | --- |
| Avalonia desktop libraries | 11.3.22 | MIT. Avalonia-LICENSE.txt, from [source revision 627ae9ef921621e27e7aa58df2796fbadb50af88](https://github.com/AvaloniaUI/Avalonia/tree/627ae9ef921621e27e7aa58df2796fbadb50af88). |
| SkiaSharp and its Linux native assets | 2.88.9 | MIT. SkiaSharp-LICENSE.txt and SkiaSharp-THIRD-PARTY-NOTICES.txt come from the 2.88.9 NuGet package. [Source](https://github.com/mono/SkiaSharp/tree/v2.88.9). |
| HarfBuzzSharp and its Linux native assets | 8.3.1.1 | MIT for the bindings. HarfBuzzSharp-LICENSE.txt comes from the 8.3.1.1 NuGet package. SkiaSharp-THIRD-PARTY-NOTICES.txt also includes the HarfBuzz notices. [Source](https://github.com/mono/SkiaSharp). |
| MicroCom.Runtime | 0.11.0 | MIT. MicroCom-LICENSE.txt comes from the [upstream repository](https://github.com/kekekeks/MicroCom). The package identifies Nikita Tsukanov as the copyright holder. |
| Tmds.DBus.Protocol | 0.21.3 | MIT. Tmds.DBus-LICENSE.txt, from [source revision 8cb04f66c330b64244e996ff68f685f27f7381a0](https://github.com/tmds/Tmds.DBus/tree/8cb04f66c330b64244e996ff68f685f27f7381a0). |

System.IO.Pipelines and the self-contained .NET runtime use the Microsoft
notices shipped with this package. The native Slithy daemon has its own
Bitcoin Core and bundled dependency notices.

The complete restored dependency graph is recorded in the project's
`obj/project.assets.json` during a build. Run `dotnet list package
--include-transitive` to inspect it. Build-time packages and native assets
for other operating systems are not all shipped in the Linux archive.
