using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Slithy_Tove;

namespace Slithy.Linux;

internal sealed record LinuxGuiUpdateManifest(
    string Version, string PackageUrl, string Sha256, string ReleaseNotes,
    DateTimeOffset PublishedAt, string Platform, string NetworkResetId, string Genesis,
    bool Required = false);

internal sealed class LinuxGuiUpdateService : IDisposable
{
    internal const string FeedUrl = "https://slithy.io/updates/linux-gui-x64/beta-20260909/stable.json";
    internal const string Platform = "linux-gui-x64";
    internal const string Network = "beta-20260909";
    internal const string Genesis = "000f888cdb70403cd5310d02d7983951ee146ac799485bca337a7b52c24643f2";
    internal const int MaxManifestBytes = 128 * 1024;
    internal const long MaxPackageBytes = 512L * 1024 * 1024;
    private readonly HttpClient _http;
    private readonly string _cacheDirectory;
    private readonly string _publicKey;
    private readonly Func<string, long> _availableBytes;
    internal const long CacheReserveBytes = 1024L * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public LinuxGuiUpdateService() : this(LinuxGuiUpdatePaths.CacheDirectory, null, ReleaseTrust.UpdatePublicKeyPem) { }

    // The alternate handler and key are for disconnected updater tests.
    internal LinuxGuiUpdateService(string cacheDirectory, HttpMessageHandler? handler, string publicKey, Func<string, long>? availableBytes = null)
    {
        _cacheDirectory = Path.GetFullPath(cacheDirectory);
        _publicKey = publicKey;
        _availableBytes = availableBytes ?? LinuxGuiUpdatePaths.AvailableBytes;
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public async Task<LinuxGuiUpdateManifest?> CheckAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        LinuxGuiVersion current = LinuxGuiVersion.Parse(currentVersion);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(25));
        byte[] bytes = await ReadManifestAsync(deadline.Token);
        LinuxGuiUpdateManifest manifest = VerifyManifest(bytes, _publicKey);
        return LinuxGuiVersion.Parse(manifest.Version).CompareTo(current) > 0 ? manifest : null;
    }

    private async Task<byte[]> ReadManifestAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(FeedUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("The Linux GUI update feed is not available yet. You can check the downloads page or try again later.");
        RequireSuccessfulResponse(response);
        if (response.Content.Headers.ContentLength > MaxManifestBytes)
            throw new InvalidDataException("The update notice is too large.");
        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        byte[] buffer = new byte[8192];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + read > MaxManifestBytes) throw new InvalidDataException("The update notice is too large.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return output.ToArray();
    }

    internal static LinuxGuiUpdateManifest VerifyManifest(byte[] envelopeBytes, string publicKey)
    {
        if (envelopeBytes.Length > MaxManifestBytes) throw new InvalidDataException("The update notice is too large.");
        // PowerShell release tools may save UTF-8 with a byte-order mark.
        ReadOnlySpan<byte> json = envelopeBytes;
        if (json.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) json = json[3..];
        using JsonDocument envelope = JsonDocument.Parse(json.ToArray());
        RejectDuplicateProperties(envelope.RootElement);
        byte[] payload = Convert.FromBase64String(envelope.RootElement.GetProperty("payloadBase64").GetString() ?? "");
        byte[] signature = Convert.FromBase64String(envelope.RootElement.GetProperty("signatureBase64").GetString() ?? "");
        if (payload.Length == 0 || payload.Length > MaxManifestBytes || signature.Length != 64)
            throw new InvalidDataException("The update signature is incomplete.");
        using ECDsa verifier = ECDsa.Create();
        verifier.ImportFromPem(publicKey);
        if (!verifier.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            throw new CryptographicException("The Linux GUI update signature is not valid.");
        using JsonDocument document = JsonDocument.Parse(payload);
        RejectDuplicateProperties(document.RootElement);
        LinuxGuiUpdateManifest manifest = JsonSerializer.Deserialize<LinuxGuiUpdateManifest>(payload, Json)
            ?? throw new InvalidDataException("The update notice is empty.");
        ValidateManifest(manifest);
        return manifest;
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("The update notice is malformed.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in element.EnumerateObject())
            if (!names.Add(property.Name)) throw new InvalidDataException("The update notice contains duplicate fields.");
    }

    internal static void ValidateManifest(LinuxGuiUpdateManifest manifest)
    {
        _ = LinuxGuiVersion.Parse(manifest.Version);
        if (manifest.Platform != Platform || manifest.NetworkResetId != Network || manifest.Genesis != Genesis)
            throw new InvalidDataException("This update belongs to a different app or network. Your installation was not changed.");
        string expected = $"https://slithy.io/downloads/linux/Slithy-Tove-Linux-GUI-{manifest.Version}-x64.tar.gz";
        if (manifest.PackageUrl != expected || !Uri.TryCreate(manifest.PackageUrl, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != "https" || uri.Host != "slithy.io" || uri.Port != 443 || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidDataException("The update download address is not an official Linux GUI package.");
        if (manifest.Sha256 == null || !Regex.IsMatch(manifest.Sha256, "\\A[0-9a-fA-F]{64}\\z", RegexOptions.CultureInvariant))
            throw new InvalidDataException("The update file hash is missing or malformed.");
        if (manifest.ReleaseNotes == null || manifest.ReleaseNotes.Length > 12000 || manifest.ReleaseNotes.Contains('\0'))
            throw new InvalidDataException("The update notes are malformed.");
        if (manifest.PublishedAt == default || manifest.PublishedAt > DateTimeOffset.UtcNow.AddDays(1))
            throw new InvalidDataException("The update date is invalid. Check your computer's date and time.");
    }

    public async Task<string> DownloadAsync(LinuxGuiUpdateManifest manifest, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        ValidateManifest(manifest);
        LinuxGuiUpdatePaths.EnsureDirectory(_cacheDirectory);
        string temporary = Path.Combine(_cacheDirectory, "package-" + Guid.NewGuid().ToString("N") + ".download");
        string completed = Path.ChangeExtension(temporary, ".tar.gz");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(15));
        try
        {
            using HttpResponseMessage response = await _http.GetAsync(manifest.PackageUrl, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            RequireSuccessfulResponse(response);
            long? contentLength = response.Content.Headers.ContentLength;
            if (contentLength is > MaxPackageBytes or 0) throw new InvalidDataException("The update download has an invalid size.");
            long budget = contentLength ?? MaxPackageBytes;
            RequireCacheSpace(budget);
            await using (Stream source = await response.Content.ReadAsStreamAsync(deadline.Token))
            await using (FileStream destination = LinuxGuiUpdatePaths.CreatePrivateFile(temporary))
            {
                byte[] buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, deadline.Token)) > 0)
                {
                    total += read;
                    if (total > MaxPackageBytes) throw new InvalidDataException("The update download exceeds the size limit.");
                    RequireCacheSpace(Math.Max(read, budget - total + read));
                    await destination.WriteAsync(buffer.AsMemory(0, read), deadline.Token);
                    if (contentLength is > 0) progress?.Report((int)Math.Clamp(total * 100 / contentLength.Value, 0, 99));
                }
                if (total == 0 || (contentLength.HasValue && total != contentLength))
                    throw new InvalidDataException("The update download was interrupted. Try again.");
                await destination.FlushAsync(deadline.Token);
                destination.Flush(true);
            }
            await using (FileStream stream = File.OpenRead(temporary))
            {
                string actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, deadline.Token));
                if (!actual.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new CryptographicException("The update file does not match its signed hash. It was not installed.");
            }
            File.Move(temporary, completed);
            progress?.Report(100);
            return completed;
        }
        catch
        {
            File.Delete(temporary);
            File.Delete(completed);
            throw;
        }
    }

    private void RequireCacheSpace(long remainingBytes)
    {
        if (_availableBytes(_cacheDirectory) < remainingBytes + CacheReserveBytes)
            throw new IOException("There is not enough free space for this update. Free space on the download drive first. Slithy reserves 1 GiB for the running node and other work.");
    }

    private static void RequireSuccessfulResponse(HttpResponseMessage response)
    {
        if ((int)response.StatusCode is >= 300 and < 400)
            throw new InvalidDataException("The update server redirected the request. No update was installed.");
        response.EnsureSuccessStatusCode();
    }

    public void RemoveCachedDownload(string path)
    {
        string full = Path.GetFullPath(path);
        if (Path.GetDirectoryName(full) != _cacheDirectory || !Regex.IsMatch(Path.GetFileName(full), "\\Apackage-[0-9a-f]{32}\\.tar\\.gz\\z", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("This file is outside the GUI update cache.");
        LinuxGuiUpdatePaths.RejectLinks(full);
        File.Delete(full);
    }

    public void Dispose() => _http.Dispose();
}

internal readonly record struct LinuxGuiVersion(int Major, int Minor, int Patch, int? Beta) : IComparable<LinuxGuiVersion>
{
    internal static LinuxGuiVersion Parse(string? text)
    {
        if (text == null || text.Length > 48) throw new InvalidDataException("The Linux GUI version is invalid.");
        Match match = Regex.Match(text, "\\A(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-beta\\.(0|[1-9][0-9]*))?\\z", RegexOptions.CultureInvariant);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out int major) || !int.TryParse(match.Groups[2].Value, out int minor) ||
            !int.TryParse(match.Groups[3].Value, out int patch)) throw new InvalidDataException("The Linux GUI version is invalid.");
        int? beta = null;
        if (match.Groups[4].Success)
        {
            if (!int.TryParse(match.Groups[4].Value, out int value)) throw new InvalidDataException("The Linux GUI version is invalid.");
            beta = value;
        }
        return new(major, minor, patch, beta);
    }

    public int CompareTo(LinuxGuiVersion other)
    {
        int result = Major.CompareTo(other.Major);
        if (result == 0) result = Minor.CompareTo(other.Minor);
        if (result == 0) result = Patch.CompareTo(other.Patch);
        if (result != 0) return result;
        if (Beta == null) return other.Beta == null ? 0 : 1;
        return other.Beta == null ? -1 : Beta.Value.CompareTo(other.Beta.Value);
    }
}

internal static class LinuxGuiUpdatePaths
{
    internal const UnixFileMode PrivateDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    internal const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    internal static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    internal static string CacheDirectory => Path.Combine(Xdg("XDG_CACHE_HOME", Path.Combine(Home, ".cache")), "slithy-tove-gui", "updates");
    internal static string ApplicationsDirectory => Path.Combine(Home, ".local", "opt", "slithy-tove");
    internal static string DesktopDataDirectory => Xdg("XDG_DATA_HOME", Path.Combine(Home, ".local", "share"));

    private static string Xdg(string name, string fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return !string.IsNullOrWhiteSpace(value) && Path.IsPathFullyQualified(value) ? Path.GetFullPath(value) : fallback;
    }

    internal static void RejectLinks(string path)
    {
        string full = Path.GetFullPath(path);
        string current = Path.GetPathRoot(full)!;
        foreach (string part in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget != null || (info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0))
                throw new InvalidOperationException("An update folder or launcher is a symbolic link. Choose a regular folder before updating.");
        }
    }

    internal static void EnsureDirectory(string path)
    {
        RejectLinks(path);
        if (OperatingSystem.IsLinux())
        {
            Directory.CreateDirectory(path, PrivateDirectory);
            File.SetUnixFileMode(path, PrivateDirectory);
        }
        else Directory.CreateDirectory(path);
        RejectLinks(path);
    }

    internal static FileStream CreatePrivateFile(string path)
    {
        RejectLinks(path);
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous };
        if (OperatingSystem.IsLinux()) options.UnixCreateMode = PrivateFile;
        return new FileStream(path, options);
    }

    internal static long AvailableBytes(string path)
    {
        string full = Path.GetFullPath(path);
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        DriveInfo? drive = DriveInfo.GetDrives().Where(d => d.IsReady && (full.Equals(d.Name, comparison) || full.StartsWith(d.Name.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, comparison)))
            .OrderByDescending(d => d.Name.Length).FirstOrDefault();
        return drive?.AvailableFreeSpace ?? throw new IOException("Free space on the update drive could not be checked. No download was started.");
    }
}
