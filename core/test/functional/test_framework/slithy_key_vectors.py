#!/usr/bin/env python3
"""Re-encode witness address fixtures with Slithy's network prefixes.

Run from the repository root. Base58 keys and script payloads stay unchanged.
The Python reference encoder generates the expectations for the C++ codec.
"""

import json
from pathlib import Path

from segwit_addr import decode_segwit_address, encode_segwit_address


def main():
    path = Path(__file__).resolve().parents[3] / "src/test/data/key_io_valid.json"
    vectors = json.loads(path.read_text(encoding="utf-8"))
    prefixes = {"main": "slithy", "test": "tslithy", "testnet4": "tb", "signet": "tb", "regtest": "rslithy"}
    for row in vectors:
        address, _, metadata = row
        if metadata["isPrivkey"] or not address.lower().startswith(("bc1", "bcrt1", "tb1", "slithy1", "tslithy1", "rslithy1")):
            continue
        version, program = decode_segwit_address(address.lower().rsplit("1", 1)[0], address)
        if version is None:
            raise ValueError("Invalid source witness vector")
        row[0] = encode_segwit_address(prefixes[metadata["chain"]], version, program)
    path.write_text(json.dumps(vectors, indent=4) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
