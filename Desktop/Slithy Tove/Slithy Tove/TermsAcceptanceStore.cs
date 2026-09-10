using System.Security.Cryptography;
using System.Text.Json;

namespace Slithy_Tove;

// This record belongs to this Windows user, not to a wallet or a beta chain.
internal sealed class TermsAcceptanceStore(string directory)
{
    public const string CurrentVersion = "1.0";
    private string RecordPath => Path.Combine(directory, "terms-acceptance.json");
    internal sealed record Acceptance(string Version, string Sha256, DateTimeOffset AcceptedUtc);

    public bool IsAccepted(byte[] terms)
    {
        try
        {
            Acceptance? record = JsonSerializer.Deserialize<Acceptance>(File.ReadAllText(RecordPath));
            return record?.Version == CurrentVersion && record.Sha256 == Hash(terms)
                && record.AcceptedUtc > DateTimeOffset.UnixEpoch;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    public void Accept(byte[] terms)
    {
        Directory.CreateDirectory(directory);
        string temporary = RecordPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(
                new Acceptance(CurrentVersion, Hash(terms), DateTimeOffset.UtcNow)));
            File.Move(temporary, RecordPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string Hash(byte[] terms) => Convert.ToHexString(SHA256.HashData(terms));
}
