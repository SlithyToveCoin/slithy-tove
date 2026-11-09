# .NET components

Direct application dependencies:

- NBitcoin 7.0.49, MIT: https://github.com/MetacoSA/NBitcoin
  Package source commit: b35b32a898c473f75b555c77cd2b6d5ffebc7c23
- QRCoder 1.6.0, MIT: https://github.com/codebude/QRCoder
  Package source commit: bd980577640c47f8bb881cf24c8443415a579d36

Their NuGet package metadata declares MIT and identifies the source revisions
above. NBitcoin-LICENSE.txt and QRCoder-LICENSE.txt contain the license texts
retrieved from those exact source commits. Retain them when redistributing.
This list does not replace upstream license files.

The package also uses Newtonsoft.Json 13.0.1. Its license is included in
Newtonsoft.Json-LICENSE.txt. DOTNET-LICENSE.txt and
DOTNET-THIRD-PARTY-NOTICES.txt come from Microsoft's .NET 10.0.14 runtime pack
(`Microsoft.NETCore.App.Runtime.win-x64`). The Linux x64 pack has the same text.
WINDOWSDESKTOP-LICENSE.txt contains the MIT license from
`Microsoft.WindowsDesktop.App.Runtime.win-x64` 10.0.14. Line endings are
normalized to LF, with one final newline per file. The upstream
copyright and license text are unchanged.

Both runtime packages identify source revision
`0427b9abbead3fa64c6655eb4290c537397131fa` in
https://github.com/dotnet/dotnet. Refresh these files with the runtime when
building a new release.

NBitcoin brings in Microsoft.Extensions.Logging.Abstractions 1.0.0 and older
System packages. Their NuGet metadata links Microsoft's .NET Library license:
https://www.microsoft.com/web/webpi/eula/net_library_eula_enu.htm
Those package terms are separate from Slithy's MIT license.

Run `dotnet list package --include-transitive` for each project after restore
to inspect the full dependency graph. A self-contained application also includes
the .NET runtime. Preserve its LICENSE.txt and ThirdPartyNotices.txt from the
published runtime distribution. Do not sign Microsoft runtime files as Slithy.
