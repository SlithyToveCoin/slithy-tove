import datetime
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("treasury_publication", ROOT / "services/fetch_beta_treasury_status.py")
feed = importlib.util.module_from_spec(spec)
spec.loader.exec_module(feed)


class PublicationTests(unittest.TestCase):
    def snapshot(self):
        return dict(network="slithy-testnet", treasuryAddress="dummy-address", chainHeight=120,
                    updatedAt=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                    currentBalance="3.00000001", unlockedBalance="1.00000001",
                    immatureBalance="2.00000000", accruedRewards="4.00000000", totalSpent="1.00000000")

    def test_rejects_wrong_identity_stale_and_inconsistent_balances(self):
        for changes in ({"network": "main"}, {"treasuryAddress": "different"},
                        {"updatedAt": "2020-01-01T00:00:00Z"}, {"currentBalance": "NaN"},
                        {"currentBalance": "9"}, {"totalSpent": "0.000000001"}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                feed.validate(self.snapshot() | changes, "slithy-testnet", "dummy-address")

    def test_failed_validation_preserves_previous_publication(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "source.json"
            output = Path(directory) / "treasury.json"
            source.write_text(json.dumps(self.snapshot()))
            feed.publish(source, output, "slithy-testnet", "dummy-address")
            previous = output.read_bytes()
            source.write_text(json.dumps(self.snapshot() | {"currentBalance": "99"}))
            with self.assertRaises(ValueError):
                feed.publish(source, output, "slithy-testnet", "dummy-address")
            self.assertEqual(previous, output.read_bytes())
            self.assertEqual({"source.json", "treasury.json"}, {p.name for p in Path(directory).iterdir()})
