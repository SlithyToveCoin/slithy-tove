//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Public Node RPC Test
//===============================================
namespace Slithy_Tove;

using System.Net.Sockets;
using System.Text.Json;

// Self-test that checks public node services without using hosted-node RPC.
// Hosted RPC should stay restricted. Public nodes expose P2P and public status.
internal static class PublicNodeRpcSelfTest
{
    public static async Task<bool> RunAsync()
    {
        try
        {
            foreach (string peer in AppSettings.DefaultPeerSeedAddresses)
            {
                (string host, int port) = SplitPeer(peer);
                using TcpClient client = new();
                await client.ConnectAsync(host, port).WaitAsync(TimeSpan.FromSeconds(10));
            }

            using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
            string treasuryJson = await http.GetStringAsync(AppSettings.DefaultTreasuryStatusUrl);
            using JsonDocument document = JsonDocument.Parse(treasuryJson);
            string node = document.RootElement.TryGetProperty("node", out JsonElement nodeElement)
                ? nodeElement.GetString() ?? ""
                : "";
            if (!node.Equals("borogove.slithy.io", StringComparison.OrdinalIgnoreCase) &&
                !node.Equals("borogove", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Public treasury status returned unexpected node value: {node}");
                return false;
            }

            Console.WriteLine("Public node test passed. Official P2P nodes are reachable, and treasury status is public.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Public node test failed: {ex.Message}");
            return false;
        }
    }

    private static (string Host, int Port) SplitPeer(string peer)
    {
        string[] parts = peer.Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[1], out int port))
        {
            throw new InvalidOperationException($"Invalid peer address: {peer}");
        }

        return (parts[0], port);
    }
}

