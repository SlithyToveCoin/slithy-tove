import datetime
import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("node_publication", ROOT / "services/export-node-snapshots.py")
feed = importlib.util.module_from_spec(spec)
spec.loader.exec_module(feed)


class NodePublicationTests(unittest.TestCase):
    def snapshot(self):
        return dict(network="slithy-testnet", node="untrusted-name", region="East Coast", chain="test",
                    height=120, chainHeight=120, headers=120, bestBlockHash="a" * 64,
                    difficulty=0.001, initialBlockDownload=False, peerCount=3,
                    version="Slithy Tove:31.0.0", p2pPort=53424, rpcPublic=False,
                    mining=dict(active=False, threads=0, speed=0, algorithm="yespower"),
                    updatedAt=datetime.datetime.now(datetime.timezone.utc).isoformat())

    def test_drops_unknown_top_level_and_nested_fields(self):
        snapshot = self.snapshot() | {"rpcPassword": "fixture-not-a-password", "privateConfig": {"path": "/private/fixture"}}
        snapshot["mining"]["privateField"] = "fixture"
        clean = feed.public_snapshot(snapshot, "borogove")
        self.assertNotIn("rpcPassword", clean)
        self.assertNotIn("privateConfig", clean)
        self.assertNotIn("privateField", clean["mining"])
        self.assertIn("privateField", snapshot["mining"])
        self.assertEqual(clean["node"], "borogove")
        self.assertEqual(clean["bestBlockHash"], "a" * 64)

    def test_rejects_invalid_types_and_freshness(self):
        for changes in ({"updatedAt": "2020-01-01T00:00:00Z"},
                        {"updatedAt": "2026-01-01T00:00:00"}, {"bestBlockHash": "not-a-hash"},
                        {"height": True}, {"region": {"privateField": "fixture"}},
                        {"difficulty": float("nan")}, {"mining": {"active": "false"}},
                        {"mining": {"algorithm": {"privateField": "fixture"}}}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                feed.public_snapshot(self.snapshot() | changes, "borogove")

    def test_export_is_filtered_and_failed_validation_preserves_previous_file(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            key = root / "fixture-key"
            key.touch()
            output = root / "public"
            target = dict(name="borogove", sshHost="fixture.invalid", keyFile=str(key))
            snapshot = self.snapshot() | {"privateField": "fixture"}
            result = subprocess.CompletedProcess([], 0, stdout=json.dumps(snapshot).encode(), stderr=b"")
            with patch.object(feed.subprocess, "run", return_value=result):
                feed.export(target, output)
            published = output / "borogove.json"
            self.assertNotIn("privateField", json.loads(published.read_text()))
            previous = published.read_bytes()
            result.stdout = json.dumps(snapshot | {"bestBlockHash": "invalid"}).encode()
            with patch.object(feed.subprocess, "run", return_value=result), self.assertRaises(ValueError):
                feed.export(target, output)
            self.assertEqual(previous, published.read_bytes())
            self.assertEqual(["borogove.json"], [p.name for p in output.iterdir()])
