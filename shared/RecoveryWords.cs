using NBitcoin;

namespace Slithy.Cryptography;

// Both front ends use this format. Changing a path changes the restored wallet.
public static class RecoveryWords
{
    public const int FormatVersion = 1;

    public static bool IsMainNetwork(string network) => network.ToLowerInvariant() switch
    {
        "main" or "mainnet" => true,
        "test" or "testnet" or "regtest" => false,
        _ => throw new ArgumentException("Unsupported Slithy recovery network.", nameof(network))
    };

    public static string AccountPath(string network) => IsMainNetwork(network) ? "84'/0'/0'" : "84'/1'/0'";

    public static string Normalize(string words)
    {
        string normalized = string.Join(' ', words.Split(
            [' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant();
        try
        {
            Mnemonic mnemonic = new(normalized, Wordlist.English);
            if (!mnemonic.IsValidChecksum)
                throw new FormatException("Recovery word checksum does not match.");
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new InvalidOperationException("Check the recovery words, their spelling and their order.", ex);
        }
        return normalized;
    }

    public static string[] Descriptors(string words, string network)
    {
        Mnemonic mnemonic = new(Normalize(words), Wordlist.English);
        ExtKey account = mnemonic.DeriveExtKey().Derive(new KeyPath(AccountPath(network)));
        string key = account.GetWif(IsMainNetwork(network) ? Network.Main : Network.TestNet).ToString();
        // These strings contain spending keys. Do not log them or put them in argv.
        return [$"wpkh({key}/0/*)", $"wpkh({key}/1/*)"];
    }
}
