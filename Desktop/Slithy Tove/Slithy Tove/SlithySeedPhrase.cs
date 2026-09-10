//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Slithy Seed Phrase
//===============================================
using System.Text.Json;
using NBitcoin;

namespace Slithy_Tove;

// Builds the deterministic wallet descriptors used by recovery-word wallets.
internal static class SlithySeedPhrase
{
    private const int ImportRangeEnd = 1000;

    public static string CreateRecoveryWords()
    {
        Mnemonic mnemonic = new(Wordlist.English, WordCount.Twelve);
        return mnemonic.ToString();
    }

    public static string NormalizeRecoveryWords(string words) => Slithy.Cryptography.RecoveryWords.Normalize(words);

    public static string[] BuildDescriptors(string words, string network = "testnet") =>
        Slithy.Cryptography.RecoveryWords.Descriptors(words, network);

    public static object[] BuildImportRequests(string[] checkedDescriptors, bool scanFromStart)
    {
        string timestamp = scanFromStart ? "0" : "now";
        return
        [
            new Dictionary<string, object?>
            {
                ["desc"] = checkedDescriptors[0],
                ["active"] = true,
                ["range"] = new object[] { 0, ImportRangeEnd },
                ["next_index"] = 0,
                ["timestamp"] = scanFromStart ? 0 : timestamp,
                ["internal"] = false
            },
            new Dictionary<string, object?>
            {
                ["desc"] = checkedDescriptors[1],
                ["active"] = true,
                ["range"] = new object[] { 0, ImportRangeEnd },
                ["next_index"] = 0,
                ["timestamp"] = scanFromStart ? 0 : timestamp,
                ["internal"] = true
            }
        ];
    }

    public static void ThrowIfImportFailed(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Slithy did not return a descriptor import result.");
        }

        foreach (JsonElement item in result.EnumerateArray())
        {
            bool success = item.TryGetProperty("success", out JsonElement successValue) &&
                           successValue.ValueKind == JsonValueKind.True;
            if (success)
            {
                continue;
            }

            if (item.TryGetProperty("error", out JsonElement error) &&
                error.TryGetProperty("message", out JsonElement message))
            {
                throw new InvalidOperationException(message.GetString() ?? "Recovery-word import failed.");
            }

            throw new InvalidOperationException("Recovery-word import failed.");
        }
    }
}
