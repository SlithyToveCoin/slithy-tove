"""Check private desktop-session cleanup without starting a desktop."""
import importlib.util
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import Mock, patch


spec = importlib.util.spec_from_file_location("xfce_session", Path(__file__).with_name("xfce-session.py"))
session = importlib.util.module_from_spec(spec)
spec.loader.exec_module(session)


class GroupInspectionTests(unittest.TestCase):
    def test_group_members_exclude_other_groups_and_zombies(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for pid, state, group in ((101, "S", 100), (102, "Z", 100), (103, "R", 200), (104, "X", 100)):
                folder = root / str(pid)
                folder.mkdir()
                (folder / "stat").write_text(f"{pid} (name with ) parentheses) {state} 1 {group} 100 0")
            self.assertEqual(session.live_group_members(100, root), [101])

    def test_disappearing_member_is_ignored(self):
        stat = Mock()
        stat.read_text.side_effect = FileNotFoundError()
        root = Mock()
        root.glob.return_value = [stat]
        self.assertEqual(session.live_group_members(100, root), [])


@unittest.skipUnless(sys.platform == "linux", "Process-group checks need Linux /proc.")
class GroupCleanupTests(unittest.TestCase):
    def test_current_group_is_never_signalled(self):
        with patch.object(session.os, "killpg") as kill:
            with self.assertRaisesRegex(ValueError, "current process group"):
                session.stop_group(Mock(pid=os.getpgrp()))
            kill.assert_not_called()

    def test_already_empty_group_is_accepted(self):
        process = subprocess.Popen([sys.executable, "-c", "pass"], start_new_session=True)
        process.wait(timeout=5)
        session.stop_group(process)
        self.assertEqual(process.returncode, 0)

    def test_exited_launcher_waits_for_child_to_finish_writing(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            code = '''import os, signal, sys, time
from pathlib import Path
root = Path(sys.argv[1])
if os.fork() == 0:
    def finish(signum, frame):
        time.sleep(.25)
        (root / "flushed").write_text("saved")
        os._exit(0)
    signal.signal(signal.SIGTERM, finish)
    (root / "ready").write_text(str(os.getpid()))
    while True:
        time.sleep(.1)
while not (root / "ready").exists():
    time.sleep(.01)
sys.exit(42)
'''
            process = subprocess.Popen([sys.executable, "-c", code, str(root)], start_new_session=True)
            try:
                self.assertEqual(process.wait(timeout=5), 42)
                self.assertTrue(session.live_group_members(process.pid))
                session.stop_group(process, grace=3)
                self.assertEqual((root / "flushed").read_text(), "saved")
                self.assertEqual(session.live_group_members(process.pid), [])
                self.assertEqual(process.returncode, 42)
            finally:
                session.stop_group(process, grace=.2)

    def test_unresponsive_group_is_killed_without_touching_other_groups(self):
        with tempfile.TemporaryDirectory() as temporary:
            ready = Path(temporary) / "ready"
            code = '''import signal, sys, time
from pathlib import Path
signal.signal(signal.SIGTERM, signal.SIG_IGN)
Path(sys.argv[1]).write_text("ready")
while True:
    time.sleep(.1)
'''
            process = subprocess.Popen([sys.executable, "-c", code, str(ready)], start_new_session=True)
            unrelated = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"], start_new_session=True)
            try:
                deadline = time.monotonic() + 5
                while not ready.exists() and time.monotonic() < deadline:
                    time.sleep(.01)
                self.assertTrue(ready.exists())
                session.stop_group(process, grace=.2)
                self.assertEqual(process.returncode, -signal.SIGKILL)
                self.assertIsNone(unrelated.poll())
                self.assertEqual(session.live_group_members(process.pid), [])
            finally:
                session.stop_group(process, grace=.2)
                session.stop_group(unrelated, grace=.2)


if __name__ == "__main__":
    unittest.main(verbosity=2)
