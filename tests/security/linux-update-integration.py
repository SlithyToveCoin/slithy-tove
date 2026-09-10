#!/usr/bin/env python3
"""Exercise the updater against disposable files and a separately named systemd service.

Run as root on the isolated development host. The copied updater trusts a new
test key, never the release key. A curl fixture supplies local signed packages.
"""
import argparse
import base64
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tarfile
import tempfile
import time
import uuid


def run(*args, **kwargs):
    return subprocess.run(args, check=True, capture_output=True, text=True, **kwargs)


def raw_signature(der):
    def length(at):
        count = der[at]
        if count < 128:
            return count, at + 1
        size = count & 127
        return int.from_bytes(der[at + 1:at + 1 + size], "big"), at + 1 + size
    assert der[0] == 48
    _, pos = length(1)
    result = b""
    for _ in range(2):
        assert der[pos] == 2
        size, pos = length(pos + 1)
        result += int.from_bytes(der[pos:pos + size], "big").to_bytes(32, "big")
        pos += size
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--updater", type=Path, required=True)
    parser.add_argument("--work", type=Path, required=True)
    args = parser.parse_args()
    assert os.geteuid() == 0, "Root is needed for the disposable service and ownership checks"
    root = args.work.resolve()
    assert root.is_dir() and "security-review-20260909" in root.parts
    workspace = Path(tempfile.mkdtemp(prefix="update-test-", dir=root))
    service = "slithy-security-test-" + uuid.uuid4().hex[:12]
    unit = Path("/run/systemd/system") / (service + ".service")
    install = workspace / "installation"
    binaries = install / "bin"
    binaries.mkdir(parents=True)
    (install / "VERSION").write_text("0.1.0\n")
    old = "#!/bin/sh\nexec /bin/sleep 300\n"
    (binaries / "slithyd").write_text(old)
    (binaries / "slithyd").chmod(0o755)
    (binaries / "old-marker").write_text("preserve on rollback")
    key = workspace / "test-private.pem"
    run("openssl", "ecparam", "-name", "prime256v1", "-genkey", "-noout", "-out", str(key))
    key.chmod(0o600)
    public = run("openssl", "pkey", "-in", str(key), "-pubout").stdout.strip()
    updater = workspace / "updater.sh"
    source = args.updater.read_text()
    source, count = re.subn(r"-----BEGIN PUBLIC KEY-----.*?-----END PUBLIC KEY-----", lambda _: public, source, flags=re.S)
    assert count == 1
    updater.write_text(source)
    fixture_bin = workspace / "fixture-bin"
    fixture_bin.mkdir()
    curl = fixture_bin / "curl"
    curl.write_text("#!/usr/bin/python3\nimport os,sys,shutil\nfrom pathlib import Path\na=sys.argv[1:]\nurl=next(x for x in a if x.startswith('https://'))\nassert url in ('https://fixture.invalid/feed','https://fixture.invalid/package')\nshutil.copyfile(Path(os.environ['SLITHY_FIXTURE']) / ('envelope.json' if url.endswith('/feed') else 'package.tar.gz'), a[a.index('-o')+1])\n")
    curl.chmod(0o755)
    env = os.environ | {"PATH": str(fixture_bin) + ":" + os.environ["PATH"],
                        "SLITHY_FIXTURE": str(workspace), "SLITHY_UPDATE_FEED": "https://fixture.invalid/feed"}
    unit_text = f"[Unit]\nDescription=Disposable Slithy updater security test\n[Service]\nType=simple\nExecStart={binaries}/slithyd\nRestart=no\n"
    unit.write_text(unit_text)

    def package(fails=False, version="0.2.0", network=None):
        with tarfile.open(workspace / "package.tar.gz", "w:gz") as archive:
            for name in ("slithyd", "slithy-cli", "slithy-wallet", "slithy-tx", "slithy", "slithy-node", "slithy-update.sh", "slithy-common.sh", "slithy-seed-tool", "slithy-terms.txt", "new-marker"):
                data = (("#!/bin/sh\nexit 1\n" if fails else old) if name == "slithyd" else "fixture\n").encode()
                member = tarfile.TarInfo("bin/" + name)
                member.mode, member.uid, member.gid, member.size = 0o777, 1234, 1234, len(data)
                archive.addfile(member, io.BytesIO(data))
            if network is not None:
                data = json.dumps(network).encode()
                member = tarfile.TarInfo("bin/slithy-network.json")
                member.size = len(data)
                archive.addfile(member, io.BytesIO(data))
        payload = json.dumps(dict(version=version, packageUrl="https://fixture.invalid/package",
                                  sha256=hashlib.sha256((workspace / "package.tar.gz").read_bytes()).hexdigest(), releaseNotes="Disposable test")).encode()
        (workspace / "payload.json").write_bytes(payload)
        run("openssl", "dgst", "-sha256", "-sign", str(key), "-out", str(workspace / "sig.der"), str(workspace / "payload.json"))
        (workspace / "envelope.json").write_text(json.dumps(dict(payloadBase64=base64.b64encode(payload).decode(), signatureBase64=base64.b64encode(raw_signature((workspace / "sig.der").read_bytes())).decode())))

    def update(success):
        result = subprocess.run(["bash", str(updater), "install", "--yes", "--install-dir", str(install), "--service", service], env=env, text=True, capture_output=True, timeout=60)
        (workspace / f"result-{time.time_ns()}.log").write_text(result.stdout + result.stderr)
        assert (result.returncode == 0) == success, result.stdout + result.stderr

    def active():
        return subprocess.run(["systemctl", "is-active", "--quiet", service]).returncode == 0

    try:
        run("systemctl", "daemon-reload")
        run("systemctl", "start", service)
        assert active()
        package(fails=True)
        update(False)
        assert active() and (install / "VERSION").read_text().strip() == "0.1.0"
        assert (binaries / "old-marker").exists() and not (binaries / "new-marker").exists()
        assert (binaries / "slithyd").read_text() == old
        print("PASS: failed service update restores complete old bin and restarts service", flush=True)
        package()
        update(True)
        assert active() and (install / "VERSION").read_text().strip() == "0.2.0"
        assert not (binaries / "old-marker").exists()
        for path in [install, binaries, *binaries.iterdir()]:
            assert path.stat().st_uid == 0 and path.stat().st_gid == 0
            assert path.stat().st_mode & 0o022 == 0, path
        print("PASS: signed update strips hostile archive ownership and writable modes", flush=True)
        run("systemctl", "stop", service)
        package(version="0.3.0")
        update(True)
        assert not active()
        print("PASS: a stopped service stays stopped", flush=True)
        package(version="0.1.0")
        update(False)
        assert (install / "VERSION").read_text().strip() == "0.3.0"
        print("PASS: signed downgrade rejected", flush=True)
        package(version="0.4.0")
        envelope = json.loads((workspace / "envelope.json").read_text())
        envelope["signatureBase64"] = base64.b64encode(bytes(64)).decode()
        (workspace / "envelope.json").write_text(json.dumps(envelope))
        update(False)
        assert (install / "VERSION").read_text().strip() == "0.3.0"
        print("PASS: invalid signature rejected before replacement", flush=True)
        network = dict(id="beta-fixture", chain="test", genesis="test-genesis", dataSubdirectory="beta-fixture")
        package(version="0.4.0", network=network)
        update(False)
        assert (install / "VERSION").read_text().strip() == "0.3.0" and not active()
        print("PASS: legacy installation cannot receive a different beta through an ordinary update", flush=True)
        (binaries / "slithy-network.json").write_text(json.dumps(network))
        package(version="0.4.0", network=network | {"genesis": "wrong-genesis"})
        update(False)
        assert (install / "VERSION").read_text().strip() == "0.3.0"
        print("PASS: different genesis rejected before replacing installed files", flush=True)
        package(version="0.4.0", network=network)
        update(True)
        assert (install / "VERSION").read_text().strip() == "0.4.0"
        print("PASS: matching-network update succeeds after migration", flush=True)
        unit.write_text(unit_text + "\n[Unit]\nRefuseManualStop=yes\n")
        run("systemctl", "daemon-reload")
        run("systemctl", "start", service)
        package(version="0.5.0", network=network)
        update(False)
        assert active() and (install / "VERSION").read_text().strip() == "0.4.0"
        print("PASS: refused service stop prevents replacement", flush=True)
    finally:
        unit.write_text(unit_text)
        subprocess.run(["systemctl", "daemon-reload"], capture_output=True)
        subprocess.run(["systemctl", "stop", service], capture_output=True)
        unit.unlink(missing_ok=True)
        subprocess.run(["systemctl", "daemon-reload"], capture_output=True)
        subprocess.run(["systemctl", "reset-failed", service], capture_output=True)
        key.unlink(missing_ok=True)
        print("Disposable service removed; logs:", workspace, flush=True)


if __name__ == "__main__":
    main()
