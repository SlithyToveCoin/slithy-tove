//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Treasury Status Service
//===============================================
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Slithy_Tove;

// Public treasury status as read from Borogove or the website feed.
internal sealed record PublicTreasuryStatus(
    string Network,
    long ChainHeight,
    string TreasuryAddress,
    decimal AccruedRewards,
    decimal CurrentBalance,
    decimal UnlockedBalance,
    decimal TotalSpent,
    DateTimeOffset UpdatedAt);

// Reads the public treasury status JSON and converts it into app-friendly numbers.
internal sealed class TreasuryStatusService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(6)
    };

    public async Task<PublicTreasuryStatus?> TryGetPublicStatusAsync(string statusUrl)
    {
        if (!Uri.TryCreate(statusUrl, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        if (uri.Scheme != Uri.UriSchemeHttps &&
            !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
        {
            return null;
        }

        try
        {
            TreasuryStatusFeed? feed = await Client.GetFromJsonAsync<TreasuryStatusFeed>(uri);
            if (feed is null ||
                string.IsNullOrWhiteSpace(feed.TreasuryAddress) ||
                string.IsNullOrWhiteSpace(feed.UpdatedAt))
            {
                return null;
            }

            DateTimeOffset updated = DateTimeOffset.Parse(feed.UpdatedAt, CultureInfo.InvariantCulture);
            if (feed.Network is not ("test" or "testnet" or "slithy-test" or "slithy-testnet") ||
                feed.TreasuryAddress != TreasuryStatusCalculator.DevelopmentTreasuryAddress ||
                feed.ChainHeight < 0 || updated > DateTimeOffset.UtcNow.AddMinutes(2) ||
                updated < DateTimeOffset.UtcNow.AddMinutes(-15)) return null;

            return new PublicTreasuryStatus(
                feed.Network ?? "unknown",
                feed.ChainHeight,
                feed.TreasuryAddress,
                ParseAmount(feed.AccruedRewards),
                ParseAmount(feed.CurrentBalance),
                ParseAmount(feed.UnlockedBalance),
                ParseAmount(feed.TotalSpent),
                updated);
        }
        catch
        {
            return null;
        }
    }

    private static decimal ParseAmount(string? value)
    {
        if (!decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out decimal amount) || amount < 0 || decimal.Round(amount, 8) != amount)
            throw new InvalidDataException("The treasury feed contains an invalid amount.");
        return amount;
    }

    private sealed class TreasuryStatusFeed
    {
        [JsonPropertyName("network")]
        public string? Network { get; set; }

        [JsonPropertyName("chainHeight")]
        public long ChainHeight { get; set; }

        [JsonPropertyName("treasuryAddress")]
        public string? TreasuryAddress { get; set; }

        [JsonPropertyName("accruedRewards")]
        public string? AccruedRewards { get; set; }

        [JsonPropertyName("currentBalance")]
        public string? CurrentBalance { get; set; }

        [JsonPropertyName("unlockedBalance")]
        public string? UnlockedBalance { get; set; }

        [JsonPropertyName("totalSpent")]
        public string? TotalSpent { get; set; }

        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }
    }
}

