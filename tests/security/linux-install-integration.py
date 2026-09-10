#!/usr/bin/env python3
"""Run the local installer inside a disposable chroot and private namespaces.

Package-manager and systemd commands are fixtures. File installation, ownership,
permissions and protection against reinstalling existing settings are real.
"""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


def run(*args, **kwargs):
    return subprocess.run(args, check=True, capture_output=True, text=True, **kwargs)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--installer", required=True, type=Path)
    parser.add_argument("--work", required=True, type=Path)
    parser.add_argument("--migration", action="store_true")
    args = parser.parse_args()
    assert os.geteuid() == 0
    assert "security-review-20260909" in args.work.resolve().parts
    # The caller must use unshare --mount --net --pid --fork.
    assert os.getpid() == 1, "Run in a fresh PID namespace"
    run("mount", "--make-rprivate", "/")
    workspace = Path(tempfile.mkdtemp(prefix="install-test-", dir=args.work))
    root = workspace / "root"
    for name in ("usr", "etc", "dev", "tmp", "opt", "var/lib", "var/log", "run", "fixturebin", "package/bin"):
        (root / name).mkdir(parents=True, exist_ok=True)
    (root / "tmp").chmod(0o1777)
    for name in ("bin", "sbin", "lib", "lib64"):
        (root / name).symlink_to("usr/" + name)
    (root / "etc/passwd").write_text("root:x:0:0:root:/root:/bin/sh\nslithy:x:65533:65533:test:/var/lib/slithy:/bin/sh\nnobody:x:65534:65534:nobody:/:/bin/sh\n")
    (root / "etc/group").write_text("root:x:0:\nslithy:x:65533:\nnogroup:x:65534:\n")
    (root / "etc/nsswitch.conf").write_text("passwd: files\ngroup: files\n")
    (root / "etc/systemd/system").mkdir(parents=True)
    previous = {}
    if args.migration:
        for name in ("opt/slithy/bin/slithyd", "opt/slithy/VERSION", "etc/slithy/menu.conf",
                     "etc/systemd/system/slithy-node.service", "var/lib/slithy/wallets/old/wallet.dat",
                     "var/lib/slithy/testnet3/blocks/blk00000.dat"):
            file = root / name
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(b"previous beta fixture: " + name.encode())
            previous[file] = file.read_bytes()
    shutil.copy2("/etc/ld.so.cache", root / "etc/ld.so.cache")
    # Overlay /usr/local/bin separately so installer symlinks remain disposable.
    local_bin = workspace / "local-bin"
    local_bin.mkdir()
    for command in ("apt-get", "systemctl"):
        file = root / "fixturebin" / command
        file.write_text("#!/bin/sh\nprintf '%s\\n' \"$0 $*\" >> /command-fixtures.log\nexit 0\n")
        file.chmod(0o755)
    for name in ("slithyd", "slithy-cli", "slithy-wallet", "slithy-tx", "slithy", "slithy-node", "slithy-update.sh", "slithy-common.sh", "slithy-seed-tool", "slithy-terms.txt"):
        file = root / "package/bin" / name
        file.write_text("#!/bin/sh\nexit 0\n")
        file.chmod(0o777)
        os.chown(file, 1234, 1234)
    (root / "package/VERSION").write_text("0.1.0\n")
    (root / "package/bin/licenses").mkdir()
    (root / "package/bin/licenses/fixture.txt").write_text("License fixture\n")
    shutil.copy2(args.installer, root / "installer.sh")
    (root / "dev/null").touch()
    mounted = []
    try:
        run("mount", "--bind", "/usr", str(root / "usr"))
        mounted.append(root / "usr")
        run("mount", "-o", "remount,bind,ro", str(root / "usr"))
        run("mount", "--bind", str(local_bin), str(root / "usr/local/bin"))
        mounted.append(root / "usr/local/bin")
        run("mount", "--bind", "/dev/null", str(root / "dev/null"))
        mounted.append(root / "dev/null")
        env = {"PATH": "/fixturebin:/usr/sbin:/usr/bin:/sbin:/bin", "HOME": "/root"}
        destination = "opt/slithy-beta-20260909" if args.migration else "opt/slithy"
        if args.migration:
            env |= {"SLITHY_INSTALL_DIR": "/" + destination, "SLITHY_SERVICE_NAME": "slithy-beta-20260909"}
            (local_bin / "slithy").symlink_to("/opt/slithy/bin/slithy")
        command = ["/usr/sbin/chroot", str(root), "/bin/bash", "/installer.sh", "/package"]
        result = subprocess.run(command, env=env, capture_output=True, text=True, timeout=60)
        (workspace / "install.log").write_text(result.stdout + result.stderr)
        assert result.returncode == 0, result.stdout + result.stderr
        notice = root / destination / "bin/licenses/fixture.txt"
        assert notice.read_text() == "License fixture\n"
        assert notice.stat().st_mode & 0o777 == 0o644
        for file in [root / destination, root / destination / "bin", *(root / destination / "bin").iterdir()]:
            assert file.stat().st_uid == 0 and file.stat().st_gid == 0
            assert file.stat().st_mode & 0o022 == 0
        attempt = subprocess.run(["/usr/sbin/chroot", str(root), "/usr/bin/setpriv", "--reuid=65534", "--regid=65534", "--clear-groups", "/bin/sh", "-c", f"echo changed >> /{destination}/bin/slithyd"], capture_output=True)
        assert attempt.returncode != 0
        print("PASS: fresh installation sets root ownership and an unrelated user cannot modify executables", flush=True)
        settings = root / "etc/slithy/beta-20260909/slithy-node.conf"
        original = settings.read_bytes()
        assert settings.stat().st_mode & 0o007 == 0
        again = subprocess.run(command, env=env, capture_output=True, text=True, timeout=60)
        assert again.returncode != 0 and settings.read_bytes() == original
        print("PASS: reinstall refuses to overwrite existing RPC configuration", flush=True)
        if args.migration:
            assert all(file.read_bytes() == data for file, data in previous.items())
            assert os.readlink(local_bin / "slithy") == "/" + destination + "/bin/slithy"
            unit = (root / "etc/systemd/system/slithy-beta-20260909.service").read_text()
            assert "-datadir=/var/lib/slithy/beta-20260909" in unit
            assert "-walletdir=/var/lib/slithy/beta-20260909/wallets" in unit
            commands = (root / "command-fixtures.log").read_text()
            assert "enable slithy-node" not in commands and "start slithy-node" not in commands
            # Roll back the launcher after a failed cutover. Old files remain available.
            (local_bin / "slithy").unlink()
            (local_bin / "slithy").symlink_to("/opt/slithy/bin/slithy")
            assert all(file.read_bytes() == data for file, data in previous.items())
            print("PASS: migration preserves old binaries, unit, wallet and chain; new paths and launcher rollback verified", flush=True)
    finally:
        for path in reversed(mounted):
            run("umount", str(path))
        print("Disposable install evidence:", workspace, flush=True)


if __name__ == "__main__":
    main()
