#!/usr/bin/env python3
"""Check recovery failure messages with fake RPC responses, without a wallet."""
import json
from pathlib import Path
import shlex
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SOURCE = (ROOT / "site/install/linux/slithy").read_text()
FUNCTIONS = "create_wallet_from_seed_json() {" + SOURCE.split("create_wallet_from_seed_json() {", 1)[1].split("mark_seed_wallet() {", 1)[0]
PRIVATE_FIXTURE = "xprv-DISPOSABLE-ERROR-REDACTION-FIXTURE"


class RecoveryErrorTests(unittest.TestCase):
    def run_case(self, mode):
        with tempfile.TemporaryDirectory(prefix="slithy-recovery-error-") as directory:
            root = Path(directory)
            seed = root / "seed.json"
            seed.write_text(json.dumps({"externalDescriptor": PRIVATE_FIXTURE, "internalDescriptor": PRIVATE_FIXTURE}))
            preamble = f"""set -euo pipefail
umask 077
SLITHY_TMP={shlex.quote(str(root))}
MODE={shlex.quote(mode)}
PRIVATE_FIXTURE={shlex.quote(PRIVATE_FIXTURE)}
WALLET_NAME=fixture
json_value() {{ python3 -c 'import json,sys; print(json.load(sys.stdin)[sys.argv[1]])' "$1"; }}
save_config() {{ :; }}
relock_wallet() {{ printf locked > "$SLITHY_TMP/relocked"; }}
mark_seed_wallet() {{ printf marked > "$SLITHY_TMP/marked"; }}
call_cli() {{
  if [[ "$1" == getdescriptorinfo ]]; then
    if [[ "$MODE" == checksum-error ]]; then printf 'Invalid descriptor %s\\n' "$PRIVATE_FIXTURE" >&2; return 1; fi
    printf '%s\\n' '{{"checksum":"fixture"}}'
  else printf created > "$SLITHY_TMP/created"; fi
}}
call_wallet_cli() {{
  if [[ "$1" != importdescriptors ]]; then return 0; fi
  case "$MODE" in
    import-stderr) printf 'Invalid descriptor %s\\n' "$PRIVATE_FIXTURE" >&2; return 1 ;;
    import-json) printf '[{{"success":false,"error":{{"message":"%s"}}}},{{"success":true}}]\\n' "$PRIVATE_FIXTURE" ;;
    empty-result) printf '[]\\n' ;;
    *) printf '[{{"success":true}},{{"success":true}}]\\n' ;;
  esac
}}
"""
            command = '\ncreate_wallet_from_seed_json fixture "" "$(cat "$SLITHY_TMP/seed.json")" now\n'
            result = subprocess.run(["bash", "-c", preamble + FUNCTIONS + command],
                                    capture_output=True, text=True, timeout=15)
            self.assertNotIn(PRIVATE_FIXTURE, result.stdout + result.stderr)
            self.assertEqual(result.returncode == 0, mode == "success", result.stdout + result.stderr)
            self.assertEqual((root / "marked").exists(), mode == "success")
            self.assertEqual((root / "created").exists(), mode != "checksum-error")

    def test_descriptor_error_does_not_expose_key(self):
        self.run_case("checksum-error")

    def test_import_rpc_error_does_not_expose_key(self):
        self.run_case("import-stderr")

    def test_import_result_error_does_not_expose_key(self):
        self.run_case("import-json")

    def test_empty_import_result_is_not_success(self):
        self.run_case("empty-result")

    def test_both_successful_branches_are_accepted(self):
        self.run_case("success")


if __name__ == "__main__":
    unittest.main(verbosity=2)
