//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               App Settings
//===============================================
using System.Security.Cryptography;
using System.Text.Json;

namespace Slithy_Tove;

// This class is the shape of the settings saved to disk.
internal sealed class AppSettings
{
    public string PendingSendTransactionId { get; set; } = "";
    public string PendingSendWallet { get; set; } = "";
    public string PendingSendHex { get; set; } = "";
    public ulong PendingSendFeeAtomic { get; set; }
    public List<string> WalletsNeedingBackup { get; set; } = [];
    // Public nodes used by the local Slithy node for peer connections.
    public const string BorogoveOfficialNodeAddress = "";
    public const string DefaultOfficialNodeAddress = BorogoveOfficialNodeAddress;
    public const string MomeOfficialNodeAddress = "";
    public const string RathOfficialNodeAddress = "";
    public const string DefaultUpdateManifestUrl = "https://slithy.io/updates/windows/stable.json";
    public const string DefaultTreasuryStatusUrl = "https://slithy.io/data/treasury-beta-20260909.json";
    public const string CurrentNetworkResetId = "beta-20260909";
    public const long PublicCheckpointHeight = 0;
    public const string PublicCheckpointHash = "000f888cdb70403cd5310d02d7983951ee146ac799485bca337a7b52c24643f2";

    public string NetworkMode { get; set; } = "BetaTestnet";
    public string NodeConnectionMode { get; set; } = "AutomaticOfficial";
    public bool UseLocalNode { get; set; }
    public string RemoteNodeAddress { get; set; } = DefaultOfficialNodeAddress;
    public string BetaNodeAddress { get; set; } = DefaultOfficialNodeAddress;
    public string LastSuccessfulRemoteNodeAddress { get; set; } = "";
    public string PublicNodePreference { get; set; } = "auto";
    public string NetworkResetId { get; set; } = CurrentNetworkResetId;
    public string LastAutomaticPublicNodeId { get; set; } = "";
    public List<string> OfficialNodeAddresses { get; set; } =
    [
    ];
    public int LocalP2pPort { get; set; } = 53424;
    public int LocalRpcPort { get; set; } = 53426;
    public int LocalZmqPort { get; set; } = 53427;
    public int WalletRpcPort { get; set; } = 53426;
    public string RpcUser { get; set; } = "slithy";
    public string RpcPassword { get; set; } = "";
    public string LastWalletName { get; set; } = "";
    public Dictionary<string, WalletBalanceCacheEntry> WalletBalanceCache { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool CheckForUpdatesAutomatically { get; set; } = true;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public string UpdateManifestUrl { get; set; } = DefaultUpdateManifestUrl;
    public string TreasuryStatusUrl { get; set; } = DefaultTreasuryStatusUrl;
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }
    public bool LocalNodeModeClarified { get; set; }
    public int AutoLockMinutes { get; set; } = 10;
    public bool MineToCustomAddress { get; set; }
    public string CustomMiningAddress { get; set; } = "";

    // Updates are considered safe when the URL is allowed and the signing key exists.
    public bool UpdatesConfigured =>
        Uri.TryCreate(UpdateManifestUrl, UriKind.Absolute, out Uri? uri) &&
        UpdateService.IsAllowedUpdateUri(uri) &&
        ReleaseTrust.IsConfigured;

    public static IReadOnlyList<string> DefaultOfficialNodeAddresses =>
    [
    ];

    // Reserved wallet RPC nodes stay out of the active list until RPC is
    // intentionally exposed tested. P2P seed nodes are separate.
    public static IReadOnlyList<string> ReservedOfficialNodeAddresses =>
    [
    ];

    internal static string CleanHostedRpcSetting(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        string normalized = value.Trim()
            .Replace(":42425", ":53426", StringComparison.OrdinalIgnoreCase)
            .Replace(":63426", ":53426", StringComparison.OrdinalIgnoreCase)
            .Replace(":43426", ":53426", StringComparison.OrdinalIgnoreCase);
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri))
        {
            return normalized;
        }

        bool hostedRpc =
            uri.Port == 53426 &&
            (uri.Host.Equals("192.168.2.75", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.Equals("node1.slithy.io", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.Equals("node2.slithy.io", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.Equals("node3.slithy.io", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.Equals("borogove.slithy.io", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.Equals("mome.slithy.io", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.Equals("rath.slithy.io", StringComparison.OrdinalIgnoreCase));
        return hostedRpc ? "" : normalized;
    }

    public static IReadOnlyList<string> DefaultPeerSeedAddresses =>
    [
        "borogove.slithy.io:53424",
        "mome.slithy.io:53424",
        "rath.slithy.io:53424"
    ];

    public static IReadOnlyList<PublicNodeChoice> PublicNodes =>
    [
        new("borogove", "Borogove", "East Coast", "borogove.slithy.io", 53424),
        new("mome", "Mome", "West Coast", "mome.slithy.io", 53424),
        new("rath", "Rath", "Europe", "rath.slithy.io", 53424)
    ];

    public IReadOnlyList<string> GetPeerSeedAddresses()
    {
        string requestedId = PublicNodePreference.Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? LastAutomaticPublicNodeId
            : PublicNodePreference;
        PublicNodeChoice? preferred = PublicNodes.FirstOrDefault(
            node => node.Id.Equals(requestedId, StringComparison.OrdinalIgnoreCase));
        if (preferred is null)
        {
            return PublicNodes.Select(node => node.PeerAddress).ToArray();
        }

        return PublicNodes
            .OrderBy(node => node.Id.Equals(preferred.Id, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .Select(node => node.PeerAddress)
            .ToArray();
    }
}

internal sealed record PublicNodeChoice(
    string Id,
    string Name,
    string Region,
    string Host,
    int P2pPort)
{
    public string DisplayText => $"{Name} ({Region})";
    public string PeerAddress => $"{Host}:{P2pPort}";
}

internal sealed class WalletBalanceCacheEntry
{
    // This is the last balance shown for a wallet. Shows the last value
    // while the real wallet balance refresh catches up in the background.
    public string WalletName { get; set; } = "";
    public string Address { get; set; } = "";
    public ulong BalanceAtomic { get; set; }
    public ulong UnlockedBalanceAtomic { get; set; }
    public long BlocksToUnlock { get; set; }
    public long NodeHeight { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }

    public decimal Balance => BalanceAtomic / 100_000_000m;
    public decimal UnlockedBalance => UnlockedBalanceAtomic / 100_000_000m;
}

internal sealed class AppSettingsStore
{
    // This class knows where settings, wallets, logs, and updates live on disk.
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public string AppDataDirectory { get; }

    public AppSettingsStore(string? appDataDirectory = null)
    {
        AppDataDirectory = appDataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Slithy Tove");
    }

    // A beta reset starts in a new folder. Old wallets and chain files stay where they were.
    public string NetworkDataDirectory => Path.Combine(AppDataDirectory, "networks", AppSettings.CurrentNetworkResetId);
    public string NodeDataDirectory => Path.Combine(NetworkDataDirectory, "daemon");
    public string WalletDirectory => Path.Combine(NetworkDataDirectory, "wallets");
    public string LogDirectory => Path.Combine(NetworkDataDirectory, "logs");
    public string UpdateDirectory => Path.Combine(AppDataDirectory, "updates");
    private string SettingsPath => Path.Combine(NetworkDataDirectory, "config", "settings.json");

    public AppSettings Load()
    {
        // Settings belong to this beta generation, including cached balances and pending payments.
        try
        {
            if (File.Exists(SettingsPath))
            {
                AppSettings settings = JsonSerializer.Deserialize<AppSettings>(
                    File.ReadAllText(SettingsPath), _jsonOptions) ?? new AppSettings();
                // Older builds used different ports. Force the current ports here
                // so users do not get stuck on stale settings.
                if ((settings.LocalP2pPort == 42434 &&
                     settings.LocalRpcPort == 42435 &&
                     settings.LocalZmqPort == 42436) ||
                    (settings.LocalP2pPort == 42534 &&
                     settings.LocalRpcPort == 42535 &&
                     settings.LocalZmqPort == 42536))
                {
                    settings.LocalP2pPort = 53424;
                    settings.LocalRpcPort = 53426;
                    settings.LocalZmqPort = 53427;
                }
                if (settings.WalletRpcPort is 42425 or 42527 or 63426)
                {
                    settings.WalletRpcPort = 53426;
                }
                settings.NetworkMode = "BetaTestnet";
                settings.LocalP2pPort = 53424;
                settings.LocalRpcPort = 53426;
                settings.LocalZmqPort = 53427;
                settings.WalletRpcPort = 53426;
                if (string.IsNullOrWhiteSpace(settings.RpcPassword) ||
                    settings.RpcPassword.Equals("slithy-beta", StringComparison.OrdinalIgnoreCase) ||
                    settings.RpcPassword.Equals("slithy-dev", StringComparison.OrdinalIgnoreCase))
                {
                    settings.RpcPassword = CreateLocalRpcPassword();
                }
                settings.UseLocalNode = false;
                settings.NodeConnectionMode = "AutomaticOfficial";
                settings.LocalNodeModeClarified = true;
                settings.CheckForUpdatesAutomatically = true;
                if (string.IsNullOrWhiteSpace(settings.PublicNodePreference) ||
                    (settings.PublicNodePreference != "auto" &&
                     !AppSettings.PublicNodes.Any(node => node.Id.Equals(settings.PublicNodePreference, StringComparison.OrdinalIgnoreCase))))
                {
                    settings.PublicNodePreference = "auto";
                }
                if (!string.IsNullOrWhiteSpace(settings.LastAutomaticPublicNodeId) &&
                    !AppSettings.PublicNodes.Any(node => node.Id.Equals(settings.LastAutomaticPublicNodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    settings.LastAutomaticPublicNodeId = "";
                }
                if (settings.OfficialNodeAddresses.Count == 0)
                {
                    settings.OfficialNodeAddresses = AppSettings.DefaultOfficialNodeAddresses.ToList();
                }
                settings.RemoteNodeAddress = AppSettings.CleanHostedRpcSetting(settings.RemoteNodeAddress);
                if (string.IsNullOrWhiteSpace(settings.BetaNodeAddress))
                {
                    settings.BetaNodeAddress = AppSettings.DefaultOfficialNodeAddress;
                }
                settings.BetaNodeAddress = AppSettings.CleanHostedRpcSetting(settings.BetaNodeAddress);
                settings.LastSuccessfulRemoteNodeAddress =
                    AppSettings.CleanHostedRpcSetting(settings.LastSuccessfulRemoteNodeAddress);
                // Normalize every saved node address before using it.
                for (int index = 0; index < settings.OfficialNodeAddresses.Count; index++)
                {
                    settings.OfficialNodeAddresses[index] =
                        AppSettings.CleanHostedRpcSetting(settings.OfficialNodeAddresses[index]);
                }
                settings.OfficialNodeAddresses = settings.OfficialNodeAddresses
                    .Where(node => !string.IsNullOrWhiteSpace(node))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (!string.IsNullOrWhiteSpace(AppSettings.DefaultOfficialNodeAddress) &&
                    !settings.OfficialNodeAddresses.Contains(AppSettings.DefaultOfficialNodeAddress, StringComparer.OrdinalIgnoreCase))
                {
                    settings.OfficialNodeAddresses.Insert(0, AppSettings.DefaultOfficialNodeAddress);
                }
                settings.OfficialNodeAddresses = settings.OfficialNodeAddresses
                    .OrderBy(node => node.Equals(AppSettings.DefaultOfficialNodeAddress, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(node => node, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (settings.UpdateManifestUrl.Equals(
                    "https://slithy.io/updates/windows/stable.json",
                    StringComparison.OrdinalIgnoreCase))
                {
                    settings.UpdateManifestUrl = AppSettings.DefaultUpdateManifestUrl;
                }
                settings.UpdateManifestUrl = settings.UpdateManifestUrl
                    .Replace("http://192.168.2.75:42427/updates/windows/stable.json", AppSettings.DefaultUpdateManifestUrl, StringComparison.OrdinalIgnoreCase)
                    .Replace("http://node1.slithy.io:42427/updates/windows/stable.json", AppSettings.DefaultUpdateManifestUrl, StringComparison.OrdinalIgnoreCase)
                    .Replace("https://node1.slithy.io/updates/windows/stable.json", AppSettings.DefaultUpdateManifestUrl, StringComparison.OrdinalIgnoreCase);
                if (settings.TreasuryStatusUrl.Equals(
                    "https://slithy.io/data/treasury.json",
                    StringComparison.OrdinalIgnoreCase))
                {
                    settings.TreasuryStatusUrl = AppSettings.DefaultTreasuryStatusUrl;
                }
                if (settings.TreasuryStatusUrl.Contains(":42428", StringComparison.OrdinalIgnoreCase))
                {
                    settings.TreasuryStatusUrl = AppSettings.DefaultTreasuryStatusUrl;
                }
                settings.TreasuryStatusUrl = settings.TreasuryStatusUrl
                    .Replace("http://192.168.2.75:42429", AppSettings.DefaultTreasuryStatusUrl, StringComparison.OrdinalIgnoreCase)
                    .Replace("http://node1.slithy.io:42429", AppSettings.DefaultTreasuryStatusUrl, StringComparison.OrdinalIgnoreCase)
                    .Replace("http://borogove.slithy.io:42429/treasury.json", AppSettings.DefaultTreasuryStatusUrl, StringComparison.OrdinalIgnoreCase);
                while (settings.TreasuryStatusUrl.EndsWith("/treasury.json/treasury.json", StringComparison.OrdinalIgnoreCase))
                {
                    settings.TreasuryStatusUrl = settings.TreasuryStatusUrl[..^"/treasury.json".Length];
                }
                // If an old incompatible wallet file is found, forget it so startup
                // asks the user to pick or create a wallet instead of failing.
                if (!string.IsNullOrWhiteSpace(settings.LastWalletName))
                {
                    string legacyWallet = Path.Combine(WalletDirectory, settings.LastWalletName);
                    string legacyKeys = legacyWallet + ".keys";
                    if (File.Exists(legacyWallet) && File.Exists(legacyKeys))
                    {
                        settings.LastWalletName = "";
                    }
                }

                return settings;
            }
        }
        catch (Exception failure)
        {
            // Do not invent a new RPC password when an existing file is damaged.
            if (File.Exists(SettingsPath + ".previous"))
            {
                AppSettings recovered = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath + ".previous"), _jsonOptions)
                    ?? throw new InvalidDataException("The saved settings backup is invalid.", failure);
                if (File.Exists(SettingsPath)) File.Move(SettingsPath, SettingsPath + ".damaged-" + Guid.NewGuid().ToString("N"));
                return recovered;
            }
            throw new InvalidDataException("Slithy could not read its settings. Existing wallet and configuration files were not replaced.", failure);
        }

        return new AppSettings
        {
            UseLocalNode = false,
            NodeConnectionMode = "AutomaticOfficial",
            LocalNodeModeClarified = true,
            RpcPassword = CreateLocalRpcPassword(),
            OfficialNodeAddresses = AppSettings.DefaultOfficialNodeAddresses.ToList()
        };
    }

    private static string CreateLocalRpcPassword()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }


    public void Save(AppSettings settings)
    {
        // Make sure the config folder exists before writing settings.json.
        string? directory = Path.GetDirectoryName(SettingsPath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        string temporary = SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings, _jsonOptions);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(SettingsPath)) File.Replace(temporary, SettingsPath, SettingsPath + ".previous");
            else File.Move(temporary, SettingsPath);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
