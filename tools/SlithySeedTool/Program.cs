using System.Text.Json;
using NBitcoin;



try
{
    int networkIndex = Array.IndexOf(args, "--network");
    string network = networkIndex < 0 ? "testnet" : networkIndex + 1 < args.Length ? args[networkIndex + 1] : throw new ArgumentException("Missing network.");
    _ = Slithy.Cryptography.RecoveryWords.IsMainNetwork(network);
    string command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
    switch (command)
    {
        case "create":
            WriteWalletJson(CreateRecoveryWords(), network);
            return 0;

        case "descriptors":
            string words = ReadWords(args);
            WriteWalletJson(words, network);
            return 0;

        case "check":
            _ = NormalizeRecoveryWords(ReadWords(args));
            Console.WriteLine("Recovery words are valid.");
            return 0;

        default:
            PrintUsage();
            return command is "help" or "-h" or "--help" ? 0 : 1;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static string CreateRecoveryWords()
{
    Mnemonic mnemonic = new(Wordlist.English, WordCount.Twelve);
    return mnemonic.ToString();
}

static string ReadWords(string[] args)
{
    if (args.Contains("--words")) throw new ArgumentException("Pass recovery words through standard input, not command arguments.");

    string stdin = Console.In.ReadToEnd();
    if (!string.IsNullOrWhiteSpace(stdin))
    {
        return stdin;
    }

    throw new InvalidOperationException("Recovery words were empty.");
}

static string NormalizeRecoveryWords(string recoveryWords) => Slithy.Cryptography.RecoveryWords.Normalize(recoveryWords);

static void WriteWalletJson(string recoveryWords, string network)
{
    string normalized = NormalizeRecoveryWords(recoveryWords);
    string[] descriptors = Slithy.Cryptography.RecoveryWords.Descriptors(normalized, network);
    var result = new
    {
        words = normalized,
        accountPath = Slithy.Cryptography.RecoveryWords.AccountPath(network),
        network,
        formatVersion = Slithy.Cryptography.RecoveryWords.FormatVersion,
        externalDescriptor = descriptors[0],
        internalDescriptor = descriptors[1]
    };

    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions
    {
        WriteIndented = false
    }));
}

static void PrintUsage()
{
    Console.WriteLine("""
Slithy seed helper

Usage:
  slithy-seed-tool create
  slithy-seed-tool descriptors --network testnet < protected-words-file
  slithy-seed-tool check < protected-words-file
""");
}
