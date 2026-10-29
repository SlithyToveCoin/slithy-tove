param(
    [Parameter(Mandatory=$true)][string]$NativeBinary,
    [string]$OutputDirectory = ""
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$project=Join-Path $root 'Desktop/Slithy.Linux/Slithy.Linux.csproj'
if(-not (Test-Path -LiteralPath $NativeBinary -PathType Leaf)){ throw 'Supply the reviewed Linux slithyd binary.' }
if(-not $OutputDirectory){ $OutputDirectory=Join-Path $root 'build/linux-gui' }
if(Test-Path -LiteralPath $OutputDirectory){ throw 'Choose a new output folder so old build files cannot enter the package.' }
& dotnet publish $project -c Release -r linux-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o $OutputDirectory
if($LASTEXITCODE -ne 0){ throw 'Linux GUI publish failed.' }
$bin=Join-Path $OutputDirectory 'Binaries'
$assets=Join-Path $OutputDirectory 'Assets'
New-Item -ItemType Directory -Force -Path $bin,$assets | Out-Null
Copy-Item -LiteralPath $NativeBinary -Destination (Join-Path $bin 'slithyd') -Force
Copy-Item -LiteralPath (Join-Path $root 'Desktop/Slithy Tove/Slithy Tove/Assets/slithy-mark.png') -Destination $assets -Force
Copy-Item -LiteralPath (Join-Path $root 'Desktop/Slithy Tove/Slithy Tove/Assets/slithy-icon-256.png') -Destination $assets -Force
$desktopScript = [IO.File]::ReadAllText((Join-Path $root 'Desktop/Slithy.Linux/install-desktop.sh')).Replace("`r`n", "`n")
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'install-desktop.sh'), $desktopScript, [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $root 'docs/LINUX-GUI-PREVIEW.md') -Destination (Join-Path $OutputDirectory 'README.md') -Force
Write-Output "Linux GUI package folder ready for testing: $OutputDirectory"
Write-Output 'No website, update feed or GitHub files were changed.'
