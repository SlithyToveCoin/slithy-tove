"""Exercise terminal update notices without a node or network connection."""
import os
from pathlib import Path
import shlex
import subprocess
import tempfile
import unittest

REPO = Path(__file__).resolve().parents[2]
SOURCE = (REPO / "site/install/linux/slithy").read_text()
FUNCTIONS = SOURCE.split("cached_update_notice() {", 1)[1].split("# Read the public fund total", 1)[0]
FUNCTIONS = "cached_update_notice() {" + FUNCTIONS


class UpdateNoticeTests(unittest.TestCase):
    def run_case(self, script, response="Update available: 0.1.50-testnet", cache=None, delay="0"):
        with tempfile.TemporaryDirectory(prefix="slithy-notice-") as temporary:
            root = Path(temporary)
            (root / "response").write_text(response + "\n")
            if cache is not None:
                (root / "update-status.txt").write_text(cache)
            preamble = f"""set -euo pipefail
umask 077
CONFIG_DIR={shlex.quote(str(root))}
UPDATE_CACHE_FILE="$CONFIG_DIR/update-status.txt"
UPDATE_CACHE_SECONDS=900
UPDATE_CHECK_PID=""
UPDATE_LAST_REQUEST=0
UPDATE_LAST_NOTIFICATION=""
check_updates() {{ printf 'checked\n' >> "$CONFIG_DIR/checks"; sleep {shlex.quote(delay)}; cat "$CONFIG_DIR/response"; }}
"""
            result = subprocess.run(["bash", "-c", preamble + FUNCTIONS + "\n" + script], capture_output=True, text=True, timeout=15)
            self.assertEqual(result.returncode, 0, result.stderr + result.stdout)
            return result.stdout

    def test_fetch_is_nonblocking_and_coalesced(self):
        result = self.run_case("""
start=$(date +%s%N)
update_notice
refresh_update_notice
end=$(date +%s%N)
(( end - start < 500000000 ))
wait "$UPDATE_CHECK_PID"
[[ $(wc -l < "$CONFIG_DIR/checks") = 1 ]]
[[ $(stat -c %a "$UPDATE_CACHE_FILE") = 600 ]]
cached_update_notice
""", delay="1")
        self.assertIn("checking in the background", result)
        self.assertIn("[!] Update available: 0.1.50-testnet", result)

    def test_offline_check_preserves_available_notice(self):
        result = self.run_case('update_notice; wait "$UPDATE_CHECK_PID"; cached_update_notice',
            response="Connection failed", cache="1\n[!] Update available: 0.1.50-testnet. Open Updates to install.\n")
        self.assertIn("[!] Update available: 0.1.50-testnet", result)
        self.assertNotIn("unable", result)

    def test_unavailable_is_not_reported_as_current(self):
        result = self.run_case('refresh_update_notice; wait "$UPDATE_CHECK_PID"; cached_update_notice', response="Connection failed")
        self.assertIn("unable to check", result)
        self.assertNotIn("up to date", result)

    def test_successful_current_check(self):
        result = self.run_case('refresh_update_notice; wait "$UPDATE_CHECK_PID"; cached_update_notice', response="Slithy is up to date.")
        self.assertIn("Slithy is up to date", result)

    def test_cache_stamp_is_data(self):
        self.run_case('refresh_update_notice; wait "$UPDATE_CHECK_PID"; [[ ! -e "$CONFIG_DIR/executed" ]]',
            cache='$(touch "$CONFIG_DIR/executed")\nUpdate: old\n')

    def test_future_cache_stamp_does_not_suppress_checks(self):
        self.run_case('refresh_update_notice; wait "$UPDATE_CHECK_PID"; [[ -e "$CONFIG_DIR/checks" ]]',
            cache="9999999999\nUpdate: old\n")

    def test_recent_cache_avoids_fetch(self):
        self.run_case('printf "%s\\n%s\\n" "$(date +%s)" "Update: Slithy is up to date." > "$UPDATE_CACHE_FILE"; update_notice; [[ -z "$UPDATE_CHECK_PID" && ! -e "$CONFIG_DIR/checks" ]]')

    def test_symlink_cache_is_not_overwritten(self):
        self.run_case('printf protected > "$CONFIG_DIR/target"; ln -s "$CONFIG_DIR/target" "$UPDATE_CACHE_FILE"; refresh_update_notice; wait "$UPDATE_CHECK_PID"; [[ $(cat "$CONFIG_DIR/target") = protected ]]')

    def test_control_characters_are_not_rendered(self):
        result = self.run_case('cached_update_notice', cache="1\n[!] Update available: 0.1.50-testnet\x1b[31m\n")
        self.assertNotIn("\x1b", result)

    def test_waiting_menu_accepts_choice(self):
        self.run_case('read_live_menu_choice choice <<< "4"; [[ "$choice" = 4 ]]')

    def test_watch_view_has_update_route(self):
        watch = SOURCE.split("watch_mining() {", 1)[1].split("show_balance() {", 1)[0]
        self.assertIn("update_notice", watch)
        self.assertIn("u|U) updates_menu", watch)
        self.assertIn("q|Q) return", watch)


if __name__ == "__main__":
    unittest.main(verbosity=2)
