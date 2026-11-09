# Installer checks

Run the web-installer checks with the .NET 10 SDK:

```powershell
dotnet run --project tests/installer/InstallerTests.csproj -c Release
```

The runtime-cleanup test needs PowerShell 7 and Inno Setup 6. Pass the compiler
path if Inno Setup is not in a standard installation folder:

```powershell
pwsh -NoProfile -File tests/installer/Test-RuntimeCleanup.ps1 -Compiler 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

You can add `-CurrentPayload` with the path to a fresh Windows publish directory.
The test checks the actual Inno preprocessor output and confirms that cleanup
does not target any file included in that payload. It also checks a simulated
future payload that reintroduces an old runtime filename.

The test creates its own setup program and disposable files under
`LocalAppData\SlithyBuildReview`. It checks matching-file removal and preserves
modified files, unrelated files and paths through a directory link. The
fixture does not register an application, create shortcuts or start Slithy.
It prints the evidence directory when it finishes.

`installer/runtime-files-0.1.48.iss` lists exact paths and SHA256 hashes from
the 0.1.48 runtime. Each entry is excluded at compile time if the new payload
contains that path. Do not replace the list with wildcard deletion.
