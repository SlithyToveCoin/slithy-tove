$ErrorActionPreference = "Stop"

$tools = Join-Path $PSScriptRoot "tools"
$nuget = Join-Path $tools "nuget.exe"

New-Item -ItemType Directory -Force -Path $tools | Out-Null

if (-not (Test-Path $nuget)) {
    Write-Host "Downloading nuget.exe"
    Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $nuget
}

Write-Host "Installing Windows SDK Build Tools package"
& $nuget install Microsoft.Windows.SDK.BuildTools -x -OutputDirectory $tools

Write-Host "Installing Azure Artifact Signing client package"
& $nuget install Microsoft.ArtifactSigning.Client -x -OutputDirectory $tools

$signTool = Get-ChildItem $tools -Recurse -Filter signtool.exe |
    Where-Object { $_.FullName -match "\\x64\\" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1

$dlib = Get-ChildItem $tools -Recurse -Filter Azure.CodeSigning.Dlib.dll |
    Where-Object { $_.FullName -match "\\x64\\" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if (-not $signTool) {
    throw "signtool.exe was not found in installer tools."
}

if (-not $dlib) {
    throw "Azure.CodeSigning.Dlib.dll was not found in installer tools."
}

Write-Host "SignTool: $($signTool.FullName)"
Write-Host "Azure dlib: $($dlib.FullName)"
