#!/usr/bin/env python3
"""Publish node snapshots after fetching them over verified SSH connections."""
import argparse
import datetime
import json
import math
import os
from pathlib import Path
import re
import subprocess
import tempfile


def public_snapshot(snapshot, name):
    if not isinstance(snapshot, dict):
        raise ValueError("Node snapshot must be an object")
    updated = datetime.datetime.fromisoformat(snapshot["updatedAt"].replace("Z", "+00:00"))
    now = datetime.datetime.now(datetime.timezone.utc)
    if updated.tzinfo is None or not -120 <= (now - updated).total_seconds() <= 120:
        raise ValueError("Node snapshot is stale or incomplete")
    if not isinstance(snapshot.get("bestBlockHash"), str) or not re.fullmatch(r"[0-9a-fA-F]{64}", snapshot["bestBlockHash"]):
        raise ValueError("Invalid block hash")

    # This file goes on the website. New collector fields stay private until
    # they are deliberately added here, including fields inside mining.
    result = {"node": name, "updatedAt": snapshot["updatedAt"], "bestBlockHash": snapshot["bestBlockHash"]}
    for field in ("network", "region", "chain", "version", "treasuryAddress",
                  "accruedRewards", "currentBalance", "unlockedBalance", "immatureBalance", "totalSpent"):
        if field in snapshot:
            value = snapshot[field]
            if not isinstance(value, str) or len(value) > 256:
                raise ValueError("Invalid public text field")
            result[field] = value
    for field in ("height", "chainHeight", "headers", "peerCount", "p2pPort"):
        if field in snapshot:
            value = snapshot[field]
            if type(value) is not int or value < 0:
                raise ValueError("Invalid public count")
            result[field] = value
    for field in ("initialBlockDownload", "rpcPublic"):
        if field in snapshot:
            if type(snapshot[field]) is not bool:
                raise ValueError("Invalid public state")
            result[field] = snapshot[field]
    if "difficulty" in snapshot:
        value = snapshot["difficulty"]
        if type(value) not in (int, float) or not math.isfinite(value) or value < 0:
            raise ValueError("Invalid difficulty")
        result["difficulty"] = value
    if "networkHashrate" in snapshot:
        hashrate = snapshot["networkHashrate"]
        if not isinstance(hashrate, dict) or "hashesPerSecond" not in hashrate:
            raise ValueError("Invalid network hashrate")
        value = hashrate["hashesPerSecond"]
        if value is not None and (type(value) not in (int, float) or not math.isfinite(value) or value < 0):
            raise ValueError("Invalid network hashrate")
        if type(hashrate.get("windowBlocks")) is not int or hashrate["windowBlocks"] != 120:
            raise ValueError("Invalid network hashrate window")
        # Publish the estimate, never arbitrary fields returned by a collector.
        result["networkHashrate"] = {"hashesPerSecond": value, "windowBlocks": 120}
    if "mining" in snapshot:
        mining = snapshot["mining"]
        if not isinstance(mining, dict):
            raise ValueError("Invalid mining status")
        clean_mining = {}
        for field in ("threads", "speed"):
            if field in mining:
                if type(mining[field]) is not int or mining[field] < 0:
                    raise ValueError("Invalid mining count")
                clean_mining[field] = mining[field]
        if "active" in mining:
            if mining["active"] is not None and type(mining["active"]) is not bool:
                raise ValueError("Invalid mining state")
            clean_mining["active"] = mining["active"]
        if "algorithm" in mining:
            if not isinstance(mining["algorithm"], str) or len(mining["algorithm"]) > 64:
                raise ValueError("Invalid mining algorithm")
            clean_mining["algorithm"] = mining["algorithm"]
        result["mining"] = clean_mining
    return result


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
    snapshot = public_snapshot(json.loads(result.stdout), name)
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
