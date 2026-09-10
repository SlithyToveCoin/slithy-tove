//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Update Service
//===============================================
using System.Security.Cryptography;
using System.Net;
using System.Text.Json;

namespace Slithy_Tove;

// The update manifest is the small signed file that tells the app what package is current.
internal sealed record UpdateManifest(
    string Version,
    string PackageUrl,
    string Sha256,
    string ReleaseNotes,
    DateTimeOffset PublishedAt,
    string UpdateLevel = "normal",
    bool Required = false,
    bool RestartRequired = true);

// Result of checking the update feed. If Manifest is null, there is nothing to install.
internal sealed record UpdateCheckResult(bool UpdateAvailable, UpdateManifest? Manifest);

// Downloads and verifies updates. It does not install them. Installation is handled
// by UpdateInstaller after the package signature and hash are checked.
internal sealed class UpdateService : IDisposable
{
    private const int MaxManifestBytes = 128 * 1024;
    private const long MaxPackageBytes = 512L * 1024 * 1024;
    private readonly string _updateDirectory;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public UpdateService(string updateDirectory)
    {
        _updateDirectory = updateDirectory;
    }

    public async Task<UpdateCheckResult> CheckAsync(
        Uri manifestUri,
        string publicKeyPem,
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        // First download the signed envelope, then verify the signature before trusting the payload.
        RequireAllowedUpdateUri(manifestUri);
        byte[] envelopeBytes = await DownloadLimitedAsync(manifestUri, MaxManifestBytes, cancellationToken);
        SignedUpdateEnvelope envelope = JsonSerializer.Deserialize<SignedUpdateEnvelope>(
            envelopeBytes, _jsonOptions) ?? throw new InvalidDataException("The update manifest is empty.");

        byte[] payload = Convert.FromBase64String(envelope.PayloadBase64);
        byte[] signature = Convert.FromBase64String(envelope.SignatureBase64);
        if (payload.Length > MaxManifestBytes)
        {
            throw new InvalidDataException("The signed update payload is too large.");
        }

        UpdateManifest manifest = VerifySignedPayload(payload, signature, publicKeyPem);
        Version availableVersion = Version.Parse(manifest.Version);
        return new UpdateCheckResult(availableVersion > currentVersion, manifest);
    }

    internal static UpdateManifest VerifySignedPayload(
        byte[] payload,
        byte[] signature,
        string publicKeyPem)
    {
        // The manifest is trusted when it was signed by the private key that matches ReleaseTrust.
        using ECDsa verifier = ECDsa.Create();
        verifier.ImportFromPem(publicKeyPem);
        if (!verifier.VerifyData(
            payload,
            signature,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
        {
            throw new CryptographicException("The update signature is not valid.");
        }

        UpdateManifest manifest = JsonSerializer.Deserialize<UpdateManifest>(
            payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("The signed update payload is invalid.");
        ValidateManifest(manifest);
        return manifest;
    }

    internal static string GetEffectiveUpdateLevel(UpdateManifest manifest)
    {
        string level = string.IsNullOrWhiteSpace(manifest.UpdateLevel)
            ? "normal"
            : manifest.UpdateLevel.Trim().ToLowerInvariant();
        if (manifest.Required &&
            (level == "normal" || level == "recommended"))
        {
            level = "required";
        }
        return level;
    }

    public async Task<string> DownloadVerifiedPackageAsync(
        UpdateManifest manifest,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Download the ZIP or installer, show progress, then compare its SHA256 hash
        // to the hash inside the signed manifest.
        Uri packageUri = new(manifest.PackageUrl);
        RequireAllowedUpdateUri(packageUri);
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(15));
        cancellationToken = deadline.Token;
        Directory.CreateDirectory(_updateDirectory);
        string safeVersion = Version.Parse(manifest.Version).ToString();
        string temporaryPath = Path.Combine(_updateDirectory, $"slithy-{safeVersion}.download");
        string extension = Path.GetExtension(packageUri.AbsolutePath);
        if (!extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Automatic updates require a signed Slithy ZIP or installer package.");
        }
        string completedPath = extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(_updateDirectory, $"Slithy-Tove-{safeVersion}.zip")
            : Path.Combine(_updateDirectory, $"Slithy-Tove-Setup-{safeVersion}.exe");

        using HttpResponseMessage response = await _httpClient.GetAsync(
            packageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        long? contentLength = response.Content.Headers.ContentLength;
        if (contentLength > MaxPackageBytes) throw new InvalidDataException("The update package exceeds the download limit.");
        await using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (FileStream destination = new(
            temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            byte[] buffer = new byte[81920];
            long totalRead = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (totalRead + read > MaxPackageBytes)
                    throw new InvalidDataException("The update package exceeds the download limit.");
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                totalRead += read;
                if (contentLength is > 0)
                {
                    progress?.Report(Math.Clamp((int)(totalRead * 100 / contentLength.Value), 0, 100));
                }
            }
        }
        progress?.Report(100);

        string actualHash;
        await using (FileStream package = File.OpenRead(temporaryPath))
        {
            actualHash = Convert.ToHexString(await SHA256.HashDataAsync(package, cancellationToken));
        }

        if (!actualHash.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(temporaryPath);
            throw new CryptographicException("The update package hash does not match the signed manifest.");
        }

        File.Move(temporaryPath, completedPath, overwrite: true);
        return completedPath;
    }

    private async Task<byte[]> DownloadLimitedAsync(
        Uri uri,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        // This prevents a bad server from making the app download a huge manifest into memory.
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        cancellationToken = deadline.Token;
        using HttpResponseMessage response = await _httpClient.GetAsync(
            uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > maximumBytes)
        {
            throw new InvalidDataException("The update manifest is too large.");
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using MemoryStream buffer = new();
        byte[] chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maximumBytes)
            {
                throw new InvalidDataException("The update manifest is too large.");
            }
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private static void ValidateManifest(UpdateManifest manifest)
    {
        // Basic shape checks before the UI is allowed to offer the update.
        _ = Version.Parse(manifest.Version);
        Uri packageUri = new(manifest.PackageUrl);
        RequireAllowedUpdateUri(packageUri);
        if (manifest.Sha256.Length != 64 || !manifest.Sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("The signed package hash is invalid.");
        }
        string level = GetEffectiveUpdateLevel(manifest);
        if (level is not ("normal" or "recommended" or "required" or "blocked"))
        {
            throw new InvalidDataException("The update level is invalid.");
        }
    }

    internal static bool IsAllowedUpdateUri(Uri uri)
    {
        // Public updates must use HTTPS. Local HTTP is allowed for private LAN testing.
        if (!uri.IsAbsoluteUri)
        {
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return true;
        }

        if (uri.Scheme != Uri.UriSchemeHttp ||
            !IPAddress.TryParse(uri.Host, out IPAddress? address))
        {
            return false;
        }

        byte[] bytes = address.GetAddressBytes();
        return IPAddress.IsLoopback(address) ||
               address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
               (bytes[0] == 10 ||
                bytes[0] == 192 && bytes[1] == 168 ||
                bytes[0] == 172 && bytes[1] is >= 16 and <= 31);
    }

    private static void RequireAllowedUpdateUri(Uri uri)
    {
        if (!IsAllowedUpdateUri(uri))
        {
            throw new InvalidDataException(
                "Updates require HTTPS, except for a signed private-LAN release server.");
        }
    }

    public void Dispose() => _httpClient.Dispose();

    private sealed record SignedUpdateEnvelope(string PayloadBase64, string SignatureBase64);
}

