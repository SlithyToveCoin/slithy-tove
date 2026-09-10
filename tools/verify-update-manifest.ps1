param(
    [string]$ManifestUrl = "https://slithy.io/updates/windows/stable.json"
)

$ErrorActionPreference = "Stop"

$releaseTrust = Join-Path (Get-Location).Path "Desktop\Slithy Tove\Slithy Tove\ReleaseTrust.cs"
$source = Get-Content -LiteralPath $releaseTrust -Raw
$match = [regex]::Match($source, '"""(?<pem>.*?-----BEGIN PUBLIC KEY-----.*?-----END PUBLIC KEY-----.*?)"""', 'Singleline')
if (!$match.Success) {
    throw "Could not find UpdatePublicKeyPem in ReleaseTrust.cs"
}

$publicKeyPem = ($match.Groups["pem"].Value -split "`r?`n" |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ }) -join "`n"
$envelope = Invoke-RestMethod -Uri $ManifestUrl
$payloadBytes = [Convert]::FromBase64String($envelope.payloadBase64)
$signatureBytes = [Convert]::FromBase64String($envelope.signatureBase64)

$publicKey = [System.Security.Cryptography.ECDsa]::Create()
$publicKey.ImportFromPem($publicKeyPem)

$valid = $publicKey.VerifyData(
    $payloadBytes,
    $signatureBytes,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256,
    [System.Security.Cryptography.DSASignatureFormat]::IeeeP1363FixedFieldConcatenation)

$payload = [System.Text.Encoding]::UTF8.GetString($payloadBytes) | ConvertFrom-Json

[pscustomobject]@{
    Valid = $valid
    Version = $payload.version
    PackageUrl = $payload.packageUrl
    Sha256 = $payload.sha256
    UpdateLevel = $payload.updateLevel
    Required = $payload.required
    PublishedAt = $payload.publishedAt
}
