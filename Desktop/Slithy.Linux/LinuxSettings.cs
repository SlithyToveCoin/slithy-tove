using System.Text.Json;

namespace Slithy.Linux;

internal sealed class LinuxSettings
{
    public bool AutoLockEnabled { get; set; } = true;
    public int AutoLockMinutes { get; set; } = 10;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool AutoCheckUpdates { get; set; } = true;
    public string LastWalletName { get; set; } = "";
    public string PublicNodePreference { get; set; } = "auto";
    public string MiningMode { get; set; } = "Balanced";
    public string CustomMiningAddress { get; set; } = "";
    public bool MineToCustomAddress { get; set; }
    public string TermsAcceptedVersion { get; set; } = "";

    public void Validate()
    {
        AutoLockMinutes = Math.Clamp(AutoLockMinutes, 1, 1440);
        if (PublicNodePreference is not ("auto" or "borogove" or "mome" or "rath")) PublicNodePreference = "auto";
        if (MiningMode is not ("Quiet" or "Balanced" or "Full power")) MiningMode = "Balanced";
        LastWalletName ??= "";
        CustomMiningAddress ??= "";
        TermsAcceptedVersion ??= "";
    }

    public bool ShouldAutoLock(DateTimeOffset lastActivity, DateTimeOffset now) =>
        AutoLockEnabled && now - lastActivity >= TimeSpan.FromMinutes(Math.Clamp(AutoLockMinutes, 1, 1440));
}

internal sealed class LinuxSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    internal const UnixFileMode PrivateDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    internal const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    public string ConfigDirectory { get; }
    public string DataDirectory { get; }
    public string WalletDirectory => Path.Combine(DataDirectory, "wallets");
    public string LogPath => Path.Combine(DataDirectory, "node.log");
    public string SettingsPath => Path.Combine(ConfigDirectory, "settings.json");

    public LinuxSettingsStore(string? rootOverride = null)
    {
        if (rootOverride != null)
        {
            if (!Path.IsPathFullyQualified(rootOverride)) throw new ArgumentException("The test directory must be an absolute path.", nameof(rootOverride));
            ConfigDirectory = Path.Combine(rootOverride, "config");
            DataDirectory = Path.Combine(rootOverride, "data");
            return;
        }
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        ConfigDirectory = Path.Combine(Xdg("XDG_CONFIG_HOME", Path.Combine(home, ".config")), "slithy-tove-gui", "beta-20260909");
        DataDirectory = Path.Combine(Xdg("XDG_DATA_HOME", Path.Combine(home, ".local", "share")), "slithy-tove-gui", "beta-20260909");
    }

    private static string Xdg(string variable, string fallback)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        return !string.IsNullOrEmpty(value) && Path.IsPathFullyQualified(value) ? value : fallback;
    }

    public LinuxSettings Load()
    {
        if (!File.Exists(SettingsPath)) return new();
        RejectLink(SettingsPath);
        if (new FileInfo(SettingsPath).Length > 65536) throw new InvalidOperationException("The settings file is too large. Move it aside before starting Slithy.");
        try
        {
            LinuxSettings settings = JsonSerializer.Deserialize<LinuxSettings>(File.ReadAllText(SettingsPath)) ?? new();
            settings.Validate();
            return settings;
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("The settings file could not be read. Your wallets have not been changed. Move settings.json aside to use the defaults.");
        }
    }

    public void Save(LinuxSettings settings)
    {
        settings.Validate();
        EnsurePrivateDirectory(ConfigDirectory);
        RejectLink(SettingsPath);
        string temporary = Path.Combine(ConfigDirectory, ".settings-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            FileStreamOptions options = new() { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (OperatingSystem.IsLinux()) options.UnixCreateMode = PrivateFile;
            using (FileStream stream = new(temporary, options))
            {
                JsonSerializer.Serialize(stream, settings, JsonOptions);
                stream.Flush(true);
            }
            File.Move(temporary, SettingsPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static void RejectLink(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("The GUI data and settings paths must not be symbolic links.");
    }

    internal static void EnsurePrivateDirectory(string path)
    {
        RejectLink(path);
        if (OperatingSystem.IsLinux())
        {
            Directory.CreateDirectory(path, PrivateDirectory);
            File.SetUnixFileMode(path, PrivateDirectory);
        }
        else Directory.CreateDirectory(path);
    }
}
