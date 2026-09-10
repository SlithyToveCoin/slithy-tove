param(
    [string]$Version = "",
    [switch]$Sign,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "Desktop\Slithy Tove\Slithy Tove\Slithy Tove.csproj"
$staging = Join-Path $root "review\release\installer-staging\win-x64"
$iss = Join-Path $PSScriptRoot "SlithyTove.iss"
$output = Join-Path $PSScriptRoot "output"
$metadata = Join-Path $PSScriptRoot "azure-signing-metadata.json"

function Find-InnoCompiler {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $command = Get-Command iscc -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw "Inno Setup compiler was not found. Install it with: winget install -e --id JRSoftware.InnoSetup"
}

function Find-SignTool {
    $command = Get-Command signtool -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $localTool = Get-ChildItem (Join-Path $PSScriptRoot "tools") -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\x64\\" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($localTool) {
        return $localTool.FullName
    }

    $candidates = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe",
        "$env:ProgramFiles\Windows Kits\10\bin\*\x64\signtool.exe",
        "${env:ProgramFiles(x86)}\Microsoft SDKs\ClickOnce\SignTool\signtool.exe"
    )

    foreach ($pattern in $candidates) {
        $found = Get-ChildItem $pattern -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
        if ($found) {
            return $found.FullName
        }
    }

    throw "SignTool was not found. Install Microsoft.Azure.ArtifactSigningClientTools or Windows SDK Build Tools."
}

function Find-AzureDlib {
    $localDlib = Get-ChildItem (Join-Path $PSScriptRoot "tools") -Recurse -Filter Azure.CodeSigning.Dlib.dll -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\x64\\" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($localDlib) {
        return $localDlib.FullName
    }

    $candidates = @(
        "$env:ProgramFiles\Microsoft\Artifact Signing Client Tools\bin\x64\Azure.CodeSigning.Dlib.dll",
        "${env:ProgramFiles(x86)}\Microsoft\Artifact Signing Client Tools\bin\x64\Azure.CodeSigning.Dlib.dll",
        "$env:ProgramFiles\Microsoft\Azure Code Signing Tools\bin\x64\Azure.CodeSigning.Dlib.dll",
        "${env:ProgramFiles(x86)}\Microsoft\Azure Code Signing Tools\bin\x64\Azure.CodeSigning.Dlib.dll"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $found = Get-ChildItem "$env:ProgramFiles","${env:ProgramFiles(x86)}" -Recurse -Filter Azure.CodeSigning.Dlib.dll -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\x64\\" } |
        Select-Object -First 1
    if ($found) {
        return $found.FullName
    }

    throw "Azure.CodeSigning.Dlib.dll was not found. Install with: winget install -e --id Microsoft.Azure.ArtifactSigningClientTools"
}

if (-not (Test-Path $project)) {
    throw "Windows project not found: $project"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$projectXml = Get-Content $project
    $Version = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not read version from the project file."
}

$staging = Join-Path $staging ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null
New-Item -ItemType Directory -Force -Path $output | Out-Null

Write-Host "Publishing Slithy Tove $Version to $staging"
dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=false `
    "-p:Version=$Version" `
    "-p:FileVersion=$Version.0" `
    "-p:AssemblyVersion=$Version.0" `
    -o $staging
if ($LASTEXITCODE -ne 0) { throw "Windows publish failed with exit code $LASTEXITCODE." }
$actualVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $staging 'Slithy Tove.exe')).ProductVersion.Split('+')[0]
if ([version]$actualVersion -ne [version]$Version) { throw "Published version $actualVersion does not match $Version." }

if ($Sign) {
    # Use the project-local CLI without changing the computer's saved PATH.
    $cliDirectory = Join-Path $PSScriptRoot 'tools/azure-cli-2.90.0'
    if (Test-Path -LiteralPath $cliDirectory) {
        $cli = Get-ChildItem $cliDirectory -Recurse -Filter az.cmd | Select-Object -First 1
        if ($cli) { $env:PATH = $cli.DirectoryName + [IO.Path]::PathSeparator + $env:PATH }
    }
    if (-not (Test-Path $metadata)) {
        throw "Missing $metadata. Copy azure-signing-metadata.sample.json and fill in the certificate profile."
    }

    $signTool = Find-SignTool
    $dlib = Find-AzureDlib

    $ownedFileNames = @(
        "Slithy Tove.exe",
        "Slithy Tove.dll",
        "slithyd.exe",
        "slithy-cli.exe",
        "slithy-wallet.exe",
        "slithy-tx.exe"
    )

    $filesToSign = Get-ChildItem $staging -Recurse -File |
        Where-Object { $ownedFileNames -contains $_.Name }

    foreach ($file in $filesToSign) {
        Write-Host "Signing $($file.FullName)"
        & $signTool sign /v /fd SHA256 /tr "http://timestamp.acs.microsoft.com" /td SHA256 /dlib $dlib /dmdf $metadata $file.FullName
        if ($LASTEXITCODE -ne 0) { throw "Signing failed for $($file.Name)." }
        if ((Get-AuthenticodeSignature -LiteralPath $file.FullName).Status -ne 'Valid') { throw "Signature validation failed for $($file.Name)." }
    }

    $env:SLITHY_SIGNING_MODE = "azure"
    # Inno expands $q after parsing its arguments, so paths with spaces stay intact.
    $signCommand = '$q{0}$q sign /v /fd SHA256 /tr http://timestamp.acs.microsoft.com /td SHA256 /dlib $q{1}$q /dmdf $q{2}$q $f' -f $signTool, $dlib, $metadata
} else {
    $env:SLITHY_SIGNING_MODE = ""
    $signCommand = ""
}

$env:SLITHY_INSTALLER_VERSION = $Version
$env:SLITHY_PUBLISH_DIR = $staging

$iscc = Find-InnoCompiler
Write-Host "Building installer with $iscc"

if ($Sign) {
    & $iscc "/Sazure=$signCommand" $iss
} else {
    & $iscc $iss
}
if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed with exit code $LASTEXITCODE." }

$installerPath = Join-Path $output "Slithy-Tove-Setup-$Version.exe"
if (-not (Test-Path $installerPath)) {
    throw "Installer was not created: $installerPath"
}

Get-FileHash $installerPath -Algorithm SHA256 | Format-List
if ($Sign -and (Get-AuthenticodeSignature -LiteralPath $installerPath).Status -ne 'Valid') { throw 'The installer signature is not valid.' }
Write-Host "Installer ready: $installerPath"
