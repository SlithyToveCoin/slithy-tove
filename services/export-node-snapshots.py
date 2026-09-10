#!/usr/bin/env python3
"""Publish node snapshots after fetching them over verified SSH connections."""
import argparse
import datetime
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile


def export(target, directory):
    name = target["name"]
    if name not in ("borogove", "mome", "rath"):
        raise ValueError("Unknown official node name")
    host = target["sshHost"]
    if not re.fullmatch(r"[A-Za-z0-9_.@-]+", host) or host.startswith("-"):
        raise ValueError("Invalid SSH host")
    port = int(target.get("sshPort", 22))
    if not 1 <= port <= 65535:
        raise ValueError("Invalid SSH port")
    key = Path(target["keyFile"])
    if not key.is_absolute() or not key.is_file():
        raise ValueError("Set an absolute SSH key file path")
    command = ["ssh", "-o", "BatchMode=yes", "-o", "StrictHostKeyChecking=yes",
               "-o", "ConnectTimeout=8", "-i", str(key), "-p", str(port), host,
               "status"]
    result = subprocess.run(command, check=True, capture_output=True, timeout=20)
    if len(result.stdout) > 1048576:
        raise ValueError("Node snapshot exceeds size limit")
    snapshot = json.loads(result.stdout)
    updated = datetime.datetime.fromisoformat(snapshot["updatedAt"].replace("Z", "+00:00"))
    now = datetime.datetime.now(datetime.timezone.utc)
    if not -120 <= (now - updated).total_seconds() <= 120 or not snapshot.get("bestBlockHash"):
        raise ValueError("Node snapshot is stale or incomplete")
    snapshot["node"] = name
    directory.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(mode="w", dir=directory, delete=False, encoding="utf-8") as file:
            temporary = Path(file.name)
            json.dump(snapshot, file)
            file.write("\n")
            file.flush()
            os.fsync(file.fileno())
        temporary.chmod(0o644)
        os.replace(temporary, directory / f"{name}.json")
    finally:
        if temporary and temporary.exists():
            temporary.unlink()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", required=True, help="Private JSON array of name, sshHost, sshPort and keyFile")
    parser.add_argument("--output", required=True, help="Public data/nodes directory")
    args = parser.parse_args()
    failures = 0
    for target in json.loads(Path(args.config).read_text()):
        try:
            export(target, Path(args.output))
        except Exception as error:
            failures += 1
            print(f"{target.get('name', 'node')}: snapshot unavailable ({type(error).__name__})")
    raise SystemExit(1 if failures else 0)
