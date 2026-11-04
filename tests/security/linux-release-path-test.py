#!/usr/bin/env python3
"""Check terminal release downloads with quoted paths and disposable signed fixtures.

The test replaces the public key in temporary script copies. No release key,
network download, system service or installed program is used.
"""
import base64
import hashlib
import io
import json
import os
from pathlib import Path
import re
import subprocess
import tarfile
import tempfile

ROOT = Path(__file__).resolve().parents[2]


def run(*args, **kwargs):
    return subprocess.run(args, check=True, text=True, capture_output=True, **kwargs)


def p1363(der):
    assert der[0] == 0x30 and der[1] < 128
    result, offset = b"", 2
    for _ in range(2):
        assert der[offset] == 2
        count = der[offset + 1]
        offset += 2
        result += int.from_bytes(der[offset:offset + count], "big").to_bytes(32, "big")
        offset += count
    return result


def main():
    with tempfile.TemporaryDirectory(prefix="slithy-release-path-") as folder:
        root = Path(folder)
        temporary = root / "temp with ' quotes and $(printf literal)"
        temporary.mkdir()
        fixture_bin = root / "fixture-bin"
        fixture_bin.mkdir()
        key = root / "fixture-private.pem"
        run("openssl", "ecparam", "-name", "prime256v1", "-genkey", "-noout", "-out", str(key))
        key.chmod(0o600)
        public = run("openssl", "pkey", "-in", str(key), "-pubout").stdout.strip()
        scripts = {}
        for name in ("slithy-install.sh", "slithy-update.sh"):
            source = (ROOT / "site/install/linux" / name).read_text()
            source, count = re.subn(r"-----BEGIN PUBLIC KEY-----.*?-----END PUBLIC KEY-----",
                                    lambda _: public, source, flags=re.S)
            assert count == 1
            scripts[name] = root / name
            scripts[name].write_text(source)
        curl = fixture_bin / "curl"
        curl.write_text("""#!/usr/bin/env python3
import os, shutil, sys
from pathlib import Path
args = sys.argv[1:]
url = next(arg for arg in args if arg.startswith('https://'))
assert url in ('https://fixture.invalid/feed', 'https://fixture.invalid/package')
name = 'feed.json' if url.endswith('/feed') else 'package.tar.gz'
shutil.copyfile(Path(os.environ['SLITHY_FIXTURE']) / name, args[args.index('-o') + 1])
""")
        curl.chmod(0o755)
        package = root / "package.tar.gz"
        with tarfile.open(package, "w:gz") as archive:
            data = b'#!/usr/bin/env bash\nset -eu\nprintf tested > "$SLITHY_FIXTURE/installed"\n'
            entry = tarfile.TarInfo("fixture/install/linux/slithy-install-local.sh")
            entry.size, entry.mode = len(data), 0o755
            archive.addfile(entry, io.BytesIO(data))
        payload = json.dumps({"version": "0.2.0-testnet", "packageUrl": "https://fixture.invalid/package",
                              "sha256": hashlib.sha256(package.read_bytes()).hexdigest(),
                              "releaseNotes": "Disposable path test"}).encode()
        (root / "payload.json").write_bytes(payload)
        run("openssl", "dgst", "-sha256", "-sign", str(key), "-out", str(root / "sig.der"), str(root / "payload.json"))
        envelope = {"payloadBase64": base64.b64encode(payload).decode(),
                    "signatureBase64": base64.b64encode(p1363((root / "sig.der").read_bytes())).decode()}
        feed = root / "feed.json"
        feed.write_text(json.dumps(envelope))
        install = root / "installation"
        env = os.environ | {"PATH": str(fixture_bin) + os.pathsep + os.environ["PATH"],
                            "SLITHY_FIXTURE": str(root), "TMPDIR": str(temporary),
                            "SLITHY_UPDATE_FEED": "https://fixture.invalid/feed",
                            "SLITHY_INSTALL_DIR": str(install)}
        run("bash", str(scripts["slithy-install.sh"]), "--yes", env=env, timeout=30)
        assert (root / "installed").read_text() == "tested"
        print("PASS: bootstrap verifies and extracts a signed fixture with quotes in TMPDIR")
        result = run("bash", str(scripts["slithy-update.sh"]), "check", env=env, timeout=30)
        assert "Update available: 0.2.0-testnet" in result.stdout
        print("PASS: updater reads signed fields with quotes in TMPDIR")
        assert list(temporary.iterdir()) == []
        print("PASS: temporary downloads are removed")
        envelope["signatureBase64"] = base64.b64encode(bytes(64)).decode()
        feed.write_text(json.dumps(envelope))
        (root / "installed").unlink()
        for name, args in (("slithy-install.sh", ["--yes"]), ("slithy-update.sh", ["check"])):
            result = subprocess.run(["bash", str(scripts[name]), *args], env=env,
                                    text=True, capture_output=True, timeout=30)
            assert result.returncode != 0 and not (root / "installed").exists()
        print("PASS: invalid signatures do not reach installation")


if __name__ == "__main__":
    main()
