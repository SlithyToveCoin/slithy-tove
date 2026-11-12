"""Network estimates belong to the chain, not the server's CPU miner."""
import datetime
import importlib.util
import json
from pathlib import Path
import subprocess
import unittest
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[2]


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


status = load("network_stats_status", "services/slithy-node-status-server.py")
exporter = load("network_stats_exporter", "services/export-node-snapshots.py")


class NetworkEstimateTests(unittest.TestCase):
    def test_estimate_uses_fixed_window_and_snapshot_height(self):
        with patch.object(status, "cli_json", return_value=312.75) as rpc:
            self.assertEqual(status.network_hashrate(7045),
                             {"hashesPerSecond": 312.75, "windowBlocks": 120})
            rpc.assert_called_once_with("getnetworkhashps", "120", "7045")

    def test_zero_is_a_valid_estimate(self):
        with patch.object(status, "cli_json", return_value=0):
            self.assertEqual(status.network_hashrate(0)["hashesPerSecond"], 0)

    def test_failed_or_malformed_estimates_are_unavailable_not_zero(self):
        for value in (None, True, False, "19.5", {}, [], -1, float("nan"),
                      float("inf"), -float("inf"), 10 ** 400):
            with self.subTest(value=value), patch.object(status, "cli_json", return_value=value):
                result = status.network_hashrate(7045)
                self.assertIsNone(result["hashesPerSecond"])
                self.assertEqual(json.dumps(result, allow_nan=False),
                                 '{"hashesPerSecond": null, "windowBlocks": 120}')
        for error in (RuntimeError("fixture-private-message"),
                      subprocess.TimeoutExpired("fixture-cli", 5), ValueError("bad JSON")):
            with self.subTest(error=type(error).__name__), patch.object(status, "cli_json", side_effect=error):
                self.assertIsNone(status.network_hashrate(7045)["hashesPerSecond"])

    def test_cli_has_a_bounded_timeout(self):
        with patch.dict(status.os.environ, {"SLITHY_STATUS_CLI": str(ROOT / "fixture-cli"),
                                           "SLITHY_STATUS_CONFIG": str(ROOT / "fixture-config")}), \
             patch.object(status.subprocess, "check_output", return_value="17.25") as run:
            self.assertEqual(status.network_hashrate(100)["hashesPerSecond"], 17.25)
            self.assertEqual(run.call_args.kwargs["timeout"], 5)


class SnapshotTests(unittest.TestCase):
    def setUp(self):
        self.tip = "a" * 64
        self.calls = []
        self.estimate = 91.25
        self.replies = {
            "getblockchaininfo": {"chain": "test", "blocks": 2, "headers": 2,
                                   "bestblockhash": self.tip, "initialblockdownload": False},
            "getnetworkinfo": {"subversion": "/fixture/"},
            "getpeerinfo": [{}, {}, {}],
            "getcpumininginfo": {"active": False, "threads": 0, "speed": 0},
        }

    def rpc(self, name, *args):
        self.calls.append((name, *args))
        if name == "getnetworkhashps":
            if isinstance(self.estimate, Exception):
                raise self.estimate
            return self.estimate
        return self.replies[name]

    def build(self, builder, current_tip=None, cached=True):
        with patch.object(status, "cli_json", side_effect=self.rpc), \
             patch.object(status, "cli", return_value=current_tip or self.tip), \
             patch.object(status, "treasury_status", return_value=None), \
             patch.object(status, "_explorer_tip", self.tip if cached else ""), \
             patch.object(status, "_explorer_blocks", []), \
             patch.object(status, "block_summary", return_value={"transactions": []}):
            return builder()

    def test_both_feeds_separate_network_estimate_from_hosted_mining(self):
        for builder in (status.build_status, status.build_explorer):
            with self.subTest(builder=builder.__name__):
                result = self.build(builder)
                self.assertEqual(result["networkHashrate"], {"hashesPerSecond": 91.25, "windowBlocks": 120})
                self.assertEqual(result["peerCount"], 3)
                self.assertFalse(result["mining"]["active"])
                self.assertEqual(result["mining"]["speed"], 0)
                self.assertIn(("getnetworkhashps", "120", "2"), self.calls)

    def test_rpc_failure_does_not_hide_peer_connections(self):
        self.estimate = RuntimeError("fixture-private-message")
        for builder in (status.build_status, status.build_explorer):
            with self.subTest(builder=builder.__name__):
                result = self.build(builder)
                self.assertIsNone(result["networkHashrate"]["hashesPerSecond"])
                self.assertEqual(result["peerCount"], 3)
                self.assertNotIn("fixture-private-message", json.dumps(result))

    def test_reorg_rejects_cached_and_new_snapshots(self):
        for builder in (status.build_status, status.build_explorer):
            for cached in (True, False):
                with self.subTest(builder=builder.__name__, cached=cached), \
                     self.assertRaisesRegex(RuntimeError, "Chain changed"):
                    self.build(builder, "b" * 64, cached)


class ExportTests(unittest.TestCase):
    def snapshot(self, estimate):
        return {"updatedAt": datetime.datetime.now(datetime.timezone.utc).isoformat(),
                "bestBlockHash": "a" * 64, "chain": "test", "peerCount": 4,
                "networkHashrate": estimate}

    def test_estimate_is_explicitly_allowed_and_private_fields_are_dropped(self):
        for value in (0, 17.25, None):
            with self.subTest(value=value):
                snapshot = self.snapshot({"hashesPerSecond": value, "windowBlocks": 120,
                                          "privateField": "fixture-private-message"})
                result = exporter.public_snapshot(snapshot, "borogove")
                self.assertEqual(result["networkHashrate"], {"hashesPerSecond": value, "windowBlocks": 120})
                self.assertEqual(result["peerCount"], 4)
                self.assertNotIn("fixture-private-message", json.dumps(result, allow_nan=False))

    def test_older_reports_remain_compatible(self):
        snapshot = self.snapshot(None)
        del snapshot["networkHashrate"]
        self.assertNotIn("networkHashrate", exporter.public_snapshot(snapshot, "borogove"))

    def test_malformed_estimates_do_not_get_published(self):
        cases = [None, [], {}, {"windowBlocks": 120},
                 {"hashesPerSecond": 1}, {"hashesPerSecond": 1, "windowBlocks": True},
                 {"hashesPerSecond": 1, "windowBlocks": 60}]
        cases.extend({"hashesPerSecond": value, "windowBlocks": 120}
                     for value in (True, "12", -1, {}, float("nan"), float("inf")))
        for value in cases:
            with self.subTest(value=value), self.assertRaises(ValueError):
                exporter.public_snapshot(self.snapshot(value), "borogove")


if __name__ == "__main__":
    unittest.main()
