"""Check the per-user launcher without changing a real desktop profile."""
import os
from pathlib import Path
import shutil
import stat
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


class DesktopInstallTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="slithy-launcher-test-")
        self.root = Path(self.temporary.name)
        self.home = self.root / "home"
        self.data = self.home / ".local/share"
        self.home.mkdir()
        self.package = self.make_package("package with spaces")

    def tearDown(self):
        self.temporary.cleanup()

    def make_package(self, name):
        package = self.root / name
        (package / "Binaries").mkdir(parents=True)
        (package / "Assets").mkdir()
        for name in ("slithy-desktop", "Binaries/slithyd"):
            (package / name).write_text("#!/bin/sh\nexit 0\n")
        source = ROOT / "Desktop/Slithy.Linux/install-desktop.sh"
        (package / "install-desktop.sh").write_bytes(source.read_bytes().replace(b"\r\n", b"\n"))
        shutil.copyfile(ROOT / "Desktop/Slithy Tove/Slithy Tove/Assets/slithy-icon-256.png", package / "Assets/slithy-icon-256.png")
        return package

    def install(self, package=None, xdg=None):
        env = dict(os.environ, HOME=str(self.home), XDG_DATA_HOME=str(self.data if xdg is None else xdg))
        return subprocess.run(["bash", str((package or self.package) / "install-desktop.sh")],
                              env=env, cwd=self.root, capture_output=True, text=True, timeout=20)

    @property
    def launcher(self):
        return self.data / "applications/io.slithy.Tove.desktop"

    @property
    def icon(self):
        return self.data / "icons/hicolor/256x256/apps/io.slithy.Tove.png"

    def test_icon_and_window_identity(self):
        result = self.install()
        self.assertEqual(result.returncode, 0, result.stderr)
        content = self.launcher.read_text()
        self.assertIn('Exec="' + str(self.package / "slithy-desktop") + '"\n', content)
        self.assertIn("StartupWMClass=io.slithy.Tove\n", content)
        self.assertIn("Icon=io.slithy.Tove\n", content)
        self.assertEqual(self.icon.read_bytes(), (self.package / "Assets/slithy-icon-256.png").read_bytes())
        self.assertEqual(stat.S_IMODE(self.launcher.stat().st_mode), 0o644)
        self.assertEqual(stat.S_IMODE(self.icon.stat().st_mode), 0o644)
        self.assertTrue(os.access(self.package / "slithy-desktop", os.X_OK))
        if shutil.which("desktop-file-validate"):
            subprocess.run(["desktop-file-validate", str(self.launcher)], check=True)

    def test_new_package_updates_existing_launcher(self):
        self.assertEqual(self.install().returncode, 0)
        replacement = self.make_package("new version")
        self.assertEqual(self.install(replacement).returncode, 0)
        self.assertIn(str(replacement / "slithy-desktop"), self.launcher.read_text())

    def test_owned_legacy_launcher_is_removed(self):
        legacy = self.data / "applications/slithy-tove-gui.desktop"
        legacy.parent.mkdir(parents=True)
        legacy.write_text('[Desktop Entry]\nName=Slithy Tove GUI Beta\nComment=Linux wallet and CPU miner for the Slithy test network\nExec="/old folder/slithy-desktop"\n')
        self.assertEqual(self.install().returncode, 0)
        self.assertFalse(legacy.exists())

    def test_custom_legacy_launcher_is_preserved(self):
        legacy = self.data / "applications/slithy-tove-gui.desktop"
        legacy.parent.mkdir(parents=True)
        legacy.write_text("[Desktop Entry]\nName=My custom shortcut\n")
        self.assertEqual(self.install().returncode, 0)
        self.assertEqual(legacy.read_text(), "[Desktop Entry]\nName=My custom shortcut\n")

    def test_launcher_symlink_is_rejected(self):
        self.launcher.parent.mkdir(parents=True)
        protected = self.root / "protected"
        protected.write_text("unchanged")
        self.launcher.symlink_to(protected)
        self.assertNotEqual(self.install().returncode, 0)
        self.assertEqual(protected.read_text(), "unchanged")

    def test_icon_symlink_is_rejected(self):
        self.icon.parent.mkdir(parents=True)
        protected = self.root / "protected"
        protected.write_text("unchanged")
        self.icon.symlink_to(protected)
        self.assertNotEqual(self.install().returncode, 0)
        self.assertEqual(protected.read_text(), "unchanged")

    def test_relative_xdg_uses_home(self):
        self.assertEqual(self.install(xdg="relative-folder").returncode, 0)
        self.assertTrue(self.launcher.is_file())
        self.assertFalse((self.root / "relative-folder").exists())

    def test_missing_icon_rejects_incomplete_package(self):
        (self.package / "Assets/slithy-icon-256.png").unlink()
        self.assertNotEqual(self.install().returncode, 0)
        self.assertFalse(self.launcher.exists())

    def test_shell_metacharacters_in_package_path_are_rejected(self):
        package = self.make_package("package$special")
        self.assertNotEqual(self.install(package).returncode, 0)
        self.assertFalse(self.launcher.exists())


if __name__ == "__main__":
    unittest.main(verbosity=2)
