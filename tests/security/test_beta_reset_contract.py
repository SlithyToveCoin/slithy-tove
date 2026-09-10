import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]


class BetaResetContract(unittest.TestCase):
    def test_network_identity_matches_clients_and_core(self):
        network = json.loads((ROOT / "site/install/linux/slithy-network.json").read_text())
        settings = (ROOT / "Desktop/Slithy Tove/Slithy Tove/AppSettings.cs").read_text(encoding="utf-8-sig")
        common = (ROOT / "site/install/linux/slithy-common.sh").read_text()
        core = (ROOT / "core/src/kernel/chainparams.cpp").read_text()
        beta = core.split("class CTestNetParams", 1)[1].split("class CTestNet4Params", 1)[0]
        base = (ROOT / "core/src/chainparamsbase.cpp").read_text()
        self.assertIn(f'CurrentNetworkResetId = "{network["id"]}"', settings)
        self.assertIn(f'PublicCheckpointHash = "{network["genesis"]}"', settings)
        rpc = (ROOT / "Desktop/Slithy Tove/Slithy Tove/NodeRpcClient.cs").read_text(encoding="utf-8-sig")
        self.assertIn(f'BetaGenesisHash = "{network["genesis"]}"', rpc)
        self.assertIn(f'SLITHY_BETA_GENESIS="{network["genesis"]}"', common)
        self.assertIn(network["genesis"], beta)
        magic = ''.join(re.findall(r'pchMessageStart\[\d\] = 0x([0-9a-f]{2});', beta))
        self.assertEqual(network["messageMagic"], magic)
        self.assertIn(f'CBaseChainParams>("{network["dataSubdirectory"]}", 53426)', base)
        self.assertEqual(network["genesisRewardAtomic"], 0)

    def test_reset_does_not_move_existing_app_data(self):
        form = (ROOT / "Desktop/Slithy Tove/Slithy Tove/Form1.cs").read_text(encoding="utf-8-sig")
        settings = (ROOT / "Desktop/Slithy Tove/Slithy Tove/AppSettings.cs").read_text(encoding="utf-8-sig")
        self.assertNotIn("ArchiveDataForNetworkReset", form)
        self.assertNotIn("ResetLocalDaemonDataAsync", form)
        self.assertNotIn("MigrateLegacyBetaDataDirectory", settings)
        self.assertIn('Path.Combine(AppDataDirectory, "networks", AppSettings.CurrentNetworkResetId)', settings)


if __name__ == "__main__":
    unittest.main()
