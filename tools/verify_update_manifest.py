#!/usr/bin/env python3

import argparse
import base64
import json
import re
import urllib.request
from pathlib import Path

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.hazmat.primitives.asymmetric.utils import encode_dss_signature
from cryptography.exceptions import InvalidSignature


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest-url", default="https://slithy.io/updates/windows/stable.json")
    parser.add_argument("--release-trust", default="Desktop/Slithy Tove/Slithy Tove/ReleaseTrust.cs")
    args = parser.parse_args()

    source = Path(args.release_trust).read_text(encoding="utf-8")
    match = re.search(
        r'"""(?P<pem>.*?-----BEGIN PUBLIC KEY-----.*?-----END PUBLIC KEY-----.*?)"""',
        source,
        flags=re.S,
    )
    if not match:
        raise RuntimeError("Could not find UpdatePublicKeyPem in ReleaseTrust.cs")

    public_key_pem = "\n".join(line.strip() for line in match.group("pem").splitlines() if line.strip())
    public_key = serialization.load_pem_public_key(public_key_pem.encode("ascii"))

    with urllib.request.urlopen(args.manifest_url, timeout=20) as response:
        envelope = json.loads(response.read().decode("utf-8"))

    payload = base64.b64decode(envelope["payloadBase64"])
    signature = base64.b64decode(envelope["signatureBase64"])
    if len(signature) != 64:
        raise RuntimeError("Manifest signature is not a P-256 P1363 signature")

    der_signature = encode_dss_signature(
        int.from_bytes(signature[:32], "big"),
        int.from_bytes(signature[32:], "big"),
    )

    valid = True
    try:
        public_key.verify(der_signature, payload, ec.ECDSA(hashes.SHA256()))
    except InvalidSignature:
        valid = False

    data = json.loads(payload.decode("utf-8"))
    print(json.dumps({
        "valid": valid,
        "version": data.get("version"),
        "packageUrl": data.get("packageUrl"),
        "sha256": data.get("sha256"),
        "updateLevel": data.get("updateLevel"),
        "required": data.get("required"),
        "publishedAt": data.get("publishedAt"),
    }, indent=2))
    return 0 if valid else 1


if __name__ == "__main__":
    raise SystemExit(main())
