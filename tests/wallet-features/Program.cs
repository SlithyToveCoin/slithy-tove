using System.Reflection;
using System.Text.Json;
using Slithy_Tove;

int checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    checks++;
    Console.WriteLine("PASS " + description);
}

const string words = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
string[] descriptors = SlithySeedPhrase.BuildDescriptors(words, "regtest");
using JsonDocument imports = JsonDocument.Parse(JsonSerializer.Serialize(SlithySeedPhrase.BuildImportRequests(descriptors, true)));
Check(imports.RootElement[0].GetProperty("range")[1].GetInt32() == 10000 && imports.RootElement[1].GetProperty("range")[1].GetInt32() == 10000, "restore scans receiving and change through index 10000");
using JsonDocument deeper = JsonDocument.Parse(JsonSerializer.Serialize(SlithySeedPhrase.BuildImportRequests(descriptors, true, 10001)));
Check(deeper.RootElement[0].GetProperty("range")[1].GetInt32() == 10001, "explicit deeper scan extends receiving range");
bool rejected = false;
try { SlithySeedPhrase.BuildImportRequests(descriptors, true, 100001); } catch (ArgumentOutOfRangeException) { rejected = true; }
Check(rejected, "unbounded recovery scan rejected");
string csv = WalletHistoryCsv.Export(new[] { new WalletTransfer("=CMD()", "Received", 1, 0, 1, DateTimeOffset.UnixEpoch, " \t+SUM(1,2)", 1) });
Check(csv.Contains("\"'=CMD()\"") && csv.Contains("\"' \t+SUM(1,2)\""), "CSV formulas are escaped even after whitespace");
Check(WalletHistoryCsv.Escape("a\"b,c") == "\"a\"\"b,c\"", "CSV quotes and separators are escaped");
Check(typeof(WalletTransactionInput).GetProperty("Address") is null, "input outpoints do not claim to identify senders");
checks += await WalletMockChecks.RunAsync();
checks += await WalletPaymentChecks.RunAsync();
checks += await WalletRecoveryChecks.RunAsync();

if (args.Contains("--integration"))
    checks += await WalletIntegration.RunAsync(args);
if (args.Contains("--interop"))
    checks += await WalletInterop.RunAsync(args);
Console.WriteLine($"Wallet feature checks passed: {checks}");
