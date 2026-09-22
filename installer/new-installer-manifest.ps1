#Requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$Installer,
    [Parameter(Mandatory)][version]$Version,
    [Parameter(Mandatory)][string]$PrivateKeyPath,
    [Parameter(Mandatory)][string]$SourceCommit,
    [Parameter(Mandatory)][string]$Output
)
$ErrorActionPreference = 'Stop'
if ($Version -lt [version]'0.1.42') { throw 'The web installer requires release 0.1.42 or newer.' }
if ($SourceCommit -notmatch '^[0-9a-f]{40}$') { throw 'Use the full release source commit.' }
$signature = Get-AuthenticodeSignature -LiteralPath $Installer
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.GetNameInfo('SimpleName', $false) -cne 'CS Idea Labs LLC') {
    throw 'Sign the full installer with the expected publisher before creating its manifest.'
}
$actual = [Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path -LiteralPath $Installer))
if ($actual.FileMajorPart -ne $Version.Major -or $actual.FileMinorPart -ne $Version.Minor -or
    $actual.FileBuildPart -ne $Version.Build -or $actual.FilePrivatePart -ne [Math]::Max(0, $Version.Revision)) {
    throw 'Installer file version does not match the release version.'
}
$root = Split-Path -Parent $PSScriptRoot
$source = Get-Content (Join-Path $root 'Desktop/Slithy Tove/Slithy Tove/ReleaseTrust.cs') -Raw
$publicKey = [regex]::Match($source, '-----BEGIN PUBLIC KEY-----.*?-----END PUBLIC KEY-----', 'Singleline').Value
$payload = [ordered]@{
    version = $Version.ToString()
    packageUrl = "https://slithy.io/downloads/windows/Slithy-Tove-Setup-$Version.exe"
    sha256 = (Get-FileHash -LiteralPath $Installer -Algorithm SHA256).Hash.ToLowerInvariant()
    releaseNotes = 'Windows wallet and CPU miner for the Slithy beta network.'
    publishedAt = [DateTimeOffset]::UtcNow.ToString('o')
    platform = 'windows-x64'
    sourceCommit = $SourceCommit
}
$bytes = [Text.Encoding]::UTF8.GetBytes(($payload | ConvertTo-Json -Compress))
$signer = [Security.Cryptography.ECDsa]::Create()
$verifier = [Security.Cryptography.ECDsa]::Create()
try {
    $signer.ImportFromPem((Get-Content -LiteralPath $PrivateKeyPath -Raw))
    $verifier.ImportFromPem($publicKey)
    $format = [Security.Cryptography.DSASignatureFormat]::IeeeP1363FixedFieldConcatenation
    $signed = $signer.SignData($bytes, [Security.Cryptography.HashAlgorithmName]::SHA256, $format)
    if (-not $verifier.VerifyData($bytes, $signed, [Security.Cryptography.HashAlgorithmName]::SHA256, $format)) {
        throw 'The release key does not match the installer trust key.'
    }
    $envelope = @{
        payloadBase64 = [Convert]::ToBase64String($bytes)
        signatureBase64 = [Convert]::ToBase64String($signed)
    }
    $envelope | ConvertTo-Json | Set-Content -LiteralPath $Output -Encoding utf8
} finally {
    $signer.Dispose()
    $verifier.Dispose()
}
