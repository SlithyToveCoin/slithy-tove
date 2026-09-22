param(
    [switch]$Sign,
    [string]$SignToolPath,
    [string]$SigningLibraryPath,
    [string]$SigningMetadataPath
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'WebInstaller/WebInstaller.csproj'
$output = Join-Path $PSScriptRoot 'output/web-installer'
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $output
if ($LASTEXITCODE -ne 0) { throw 'Web installer build failed.' }
$file = Join-Path $output 'Slithy-Tove-Web-Setup.exe'
if ($Sign) {
    foreach ($path in @($SignToolPath, $SigningLibraryPath, $SigningMetadataPath)) {
        if (-not $path -or -not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw 'Supply the SignTool, signing library and private metadata file paths.'
        }
    }
    & $SignToolPath sign /fd SHA256 /tr http://timestamp.acs.microsoft.com /td SHA256 /dlib $SigningLibraryPath /dmdf $SigningMetadataPath $file
    if ($LASTEXITCODE -ne 0) { throw 'Azure signing failed. Do not publish this file.' }
    $signature = Get-AuthenticodeSignature -LiteralPath $file
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.GetNameInfo('SimpleName', $false) -cne 'CS Idea Labs LLC') {
        throw 'The web installer does not have the expected valid publisher signature.'
    }
} else {
    Write-Warning 'Unsigned test build. Do not publish this file.'
}
Get-FileHash -LiteralPath $file -Algorithm SHA256
