"""Validate build inventories without using application or wallet files."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

TOOL = Path(__file__).resolve().parents[2] / "tools/linux-gui-install-inventory.py"


class InventoryTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="slithy-inventory-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.package = self.root / "package"
        self.package.mkdir()
        (self.package / "slithy-desktop.dll").write_bytes(b"assembly fixture")

    def build(self, succeeds):
        # Security checks must run even when Python disables assertions.
        result = subprocess.run([sys.executable, "-O", str(TOOL), str(self.package)],
                                text=True, capture_output=True, timeout=20)
        self.assertEqual(result.returncode == 0, succeeds, result.stdout + result.stderr)

    def test_hashes_and_executable_flags(self):
        (self.package / "slithy-desktop").write_bytes(b"app fixture")
        self.build(True)
        files = json.loads((self.package / "install-files.json").read_text())
        self.assertEqual(len(files), 2)
        app = next(entry for entry in files if entry["Path"] == "slithy-desktop")
        self.assertTrue(app["Executable"])
        self.assertEqual(app["Sha256"], hashlib.sha256(b"app fixture").hexdigest())

    def test_existing_inventory_is_preserved(self):
        inventory = self.package / "install-files.json"
        inventory.write_text("previous build")
        self.build(False)
        self.assertEqual(inventory.read_text(), "previous build")

    @unittest.skipUnless(os.name == "posix", "Symlink permissions vary on Windows")
    def test_dangling_inventory_link_is_rejected(self):
        target = self.root / "must-not-be-created"
        (self.package / "install-files.json").symlink_to(target)
        self.build(False)
        self.assertFalse(target.exists())

    @unittest.skipUnless(os.name == "posix", "Symlink permissions vary on Windows")
    def test_package_links_are_rejected(self):
        target = self.root / "outside"
        target.write_bytes(b"outside fixture")
        (self.package / "linked-file").symlink_to(target)
        self.build(False)
        self.assertFalse((self.package / "install-files.json").exists())

    @unittest.skipUnless(os.name == "posix", "Sparse fixture uses Linux filesystem semantics")
    def test_oversized_package_is_rejected_before_hashing(self):
        with (self.package / "large-file").open("wb") as output:
            output.truncate(1024 * 1024 * 1024 + 1)
        self.build(False)
        self.assertFalse((self.package / "install-files.json").exists())


if __name__ == "__main__":
    unittest.main(verbosity=2)
