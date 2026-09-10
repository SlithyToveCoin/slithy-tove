"""Disposable fixtures for the public status index and Linux extraction rules."""
import importlib.util
import io
import pathlib
import re
import subprocess
import sys
import tarfile
import tempfile
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("status", ROOT / "services/slithy-node-status-server.py")
status = importlib.util.module_from_spec(spec)
spec.loader.exec_module(status)


class TreasuryTests(unittest.TestCase):
    def setUp(self):
        status.reset_treasury_index()
        status.TREASURY_SCRIPT = "fixture-script"
        status.TREASURY_ADDRESS = "fixture-address"
        self.blocks = {}
        for height in (1, 2):
            self.blocks[height] = {
                "hash": f"hash-{height}", "previousblockhash": f"hash-{height-1}",
                "tx": [{"txid": f"reward-{height}", "vin": [{"coinbase": "00"}],
                        "vout": [{"n": 0, "value": 1, "scriptPubKey": {"hex": "fixture-script"}}]}],
            }
        status.cli = lambda method, height: self.blocks[int(height)]["hash"]
        status.cli_json = lambda method, block_hash, verbosity: next(b for b in self.blocks.values() if b["hash"] == block_hash)
        status.read_block = lambda block_hash: next(b for b in self.blocks.values() if b["hash"] == block_hash)

    def test_spending_change_and_reorg(self):
        self.blocks[2]["tx"].append({"txid": "spend", "vin": [{"txid": "reward-1", "vout": 0}],
                                     "vout": [{"n": 0, "value": "0.3", "scriptPubKey": {"hex": "fixture-script"}}]})
        result = status.treasury_status(2)
        self.assertEqual(result["accruedRewards"], "2.00000000")
        self.assertEqual(result["currentBalance"], "1.30000000")
        self.assertEqual(result["unlockedBalance"], "0.30000000")
        self.assertEqual(result["totalSpent"], "0.70000000")
        self.blocks[2]["hash"] = "replacement-2"
        self.blocks[2]["tx"] = self.blocks[2]["tx"][:1]
        result = status.treasury_status(2)
        self.assertEqual(result["currentBalance"], "2.00000000")
        self.assertEqual(result["totalSpent"], "0.00000000")

    def test_requests_read_cache_without_rpc(self):
        handler = object.__new__(status.Handler)
        handler.path = "/status.json"
        replies = []
        handler.send_payload = lambda code, payload: replies.append(code)
        status._snapshots.clear()
        handler.do_GET()
        self.assertEqual(replies, [503])
        status._snapshots["status"] = (status.time.monotonic(), {"height": 2})
        handler.do_GET()
        self.assertEqual(replies, [503, 200])


class ArchiveTests(unittest.TestCase):
    def run_archive(self, script, name, kind=tarfile.REGTYPE):
        source = (ROOT / "site/install/linux" / script).read_text()
        extractor = re.search(r'python3 - "\$PACKAGE" "\$EXTRACTED" <<\x27PY\x27\n(.*?)\nPY', source, re.S)[1]
        with tempfile.TemporaryDirectory() as temp:
            root = pathlib.Path(temp)
            archive = root / "package.tar.gz"
            output = root / "extracted"
            output.mkdir()
            with tarfile.open(archive, "w:gz") as tar:
                member = tarfile.TarInfo(name)
                member.mode = 0o777
                member.type = kind
                member.linkname = "../../outside"
                member.size = 4 if kind == tarfile.REGTYPE else 0
                tar.addfile(member, io.BytesIO(b"test") if member.size else None)
            result = subprocess.run([sys.executable, "-c", extractor, str(archive), str(output)], capture_output=True)
            self.assertFalse((root / "outside").exists())
            return result.returncode

    def test_installer_and_updater_reject_unsafe_members(self):
        for script in ("slithy-install.sh", "slithy-update.sh"):
            with self.subTest(script=script):
                self.assertEqual(self.run_archive(script, "bin/slithyd"), 0)
                self.assertNotEqual(self.run_archive(script, "../outside"), 0)
                self.assertNotEqual(self.run_archive(script, "/outside"), 0)
                self.assertNotEqual(self.run_archive(script, "bin/link", tarfile.SYMTYPE), 0)
                self.assertNotEqual(self.run_archive(script, "bin/device", tarfile.CHRTYPE), 0)


if __name__ == "__main__":
    unittest.main()
