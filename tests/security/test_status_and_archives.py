"""Disposable fixtures for the public status index and Linux extraction rules."""
import importlib.util
import io
from contextlib import redirect_stdout
import pathlib
import re
import subprocess
import sys
import tarfile
import tempfile
import unittest
from unittest.mock import patch

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


class ExplorerTests(unittest.TestCase):
    def test_coinbase_and_addresses(self):
        status.TREASURY_SCRIPT = 'treasury-script'
        tx = {'txid': 'a'*64, 'vin': [{'coinbase': '00'}], 'vout': [
            {'n': 0, 'value': '9.00000001', 'scriptPubKey': {'address': 'miner', 'hex': 'miner-script'}},
            {'n': 1, 'value': '1', 'scriptPubKey': {'address': 'fund', 'hex': 'treasury-script'}}]}
        result = status.transaction_summary(tx)
        self.assertTrue(result['coinbase'])
        self.assertEqual(result['inputs'], [{'coinbase': True}])
        self.assertEqual(result['outputs'][0]['value'], '9.00000001')
        self.assertTrue(result['outputs'][1]['treasury'])

    def test_previous_output_and_limits(self):
        tx = {'txid': 'b'*64, 'vin': [{'txid': 'a'*64, 'vout': 0,
              'prevout': {'value': '0.12345678', 'scriptPubKey': {'address': 'previous'}}}]*20,
              'vout': [{'n': 0, 'value': 0, 'scriptPubKey': {'type': 'nulldata'}}]*20}
        result = status.transaction_summary(tx)
        self.assertFalse(result['coinbase'])
        self.assertEqual(result['inputs'][0]['address'], 'previous')
        self.assertEqual(result['inputCount'], 20)
        self.assertEqual(len(result['inputs']), 16)
        self.assertEqual(len(result['outputs']), 16)
        self.assertIsNone(result['outputs'][0]['address'])
        tx['vin'] = [{'txid': 'c'*64, 'vout': 1}]
        self.assertIsNone(status.transaction_summary(tx)['inputs'][0]['address'])


class StatusCollectionTests(unittest.TestCase):
    def test_collector_errors_do_not_print_private_rpc_messages(self):
        output = io.StringIO()
        with patch.object(status, "build_status", side_effect=RuntimeError("fixture-private-diagnostic")), \
             patch.object(status, "build_explorer", side_effect=RuntimeError("fixture-private-diagnostic")), \
             patch.object(status.time, "sleep", side_effect=InterruptedError), redirect_stdout(output):
            with self.assertRaises(InterruptedError):
                status.collect_snapshots()
        self.assertNotIn("fixture-private-diagnostic", output.getvalue())
        self.assertIn("RuntimeError", output.getvalue())

    def test_http_routes_cannot_select_rpc_methods_and_stale_data_is_rejected(self):
        handler = object.__new__(status.Handler)
        replies = []
        handler.send_payload = lambda code, payload: replies.append(code)
        now = status.time.monotonic()
        with patch.object(status, "cli", side_effect=AssertionError("HTTP called RPC")), \
             patch.dict(status._snapshots, {"status": (now, {"height": 2})}, clear=True):
            for path, expected in (("/status.json?method=stop", 200), ("/rpc", 404),
                                   ("/../slithy.conf", 404), ("/" + "a" * 2048, 414),
                                   ("/treasury.json", 503)):
                handler.path = path
                handler.do_GET()
                self.assertEqual(replies[-1], expected)
            status._snapshots["status"] = (now - 121, {"height": 2})
            handler.path = "/status.json"
            handler.do_GET()
            self.assertEqual(replies[-1], 503)

    def test_reorg_during_collection_does_not_publish_mixed_chain_data(self):
        replies = {"getblockchaininfo": {"chain": "regtest", "blocks": 2, "headers": 2,
                                        "bestblockhash": "a" * 64},
                   "getnetworkinfo": {"subversion": "/fixture/"}, "getpeerinfo": [],
                   "getcpumininginfo": {"active": False}}
        with patch.object(status, "cli_json", side_effect=lambda name: replies[name]), \
             patch.object(status, "treasury_status", return_value={"currentBalance": "1.00000000"}), \
             patch.object(status, "cli", return_value="b" * 64):
            with self.assertRaisesRegex(RuntimeError, "Chain changed"):
                status.build_status()
        with patch.object(status, "cli_json", side_effect=lambda name: replies[name]), \
             patch.object(status, "treasury_status", return_value={"currentBalance": "1.00000000"}), \
             patch.object(status, "cli", return_value="a" * 64):
            snapshot = status.build_status()
            self.assertEqual(snapshot["bestBlockHash"], "a" * 64)
            self.assertEqual(snapshot["currentBalance"], "1.00000000")


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
