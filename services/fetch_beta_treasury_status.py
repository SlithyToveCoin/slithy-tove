#!/usr/bin/env python3
"""Publish a validated treasury snapshot fetched by the verified SSH exporter."""
import argparse
import datetime
from decimal import Decimal, InvalidOperation
import json
import os
from pathlib import Path
import tempfile


def validate(status, network, address):
    if status.get("network") != network or status.get("treasuryAddress") != address:
        raise ValueError("Treasury network or address does not match the operator configuration")
    height = status.get("chainHeight")
    if type(height) is not int or height < 0:
        raise ValueError("Invalid chain height")
    updated = datetime.datetime.fromisoformat(status["updatedAt"].replace("Z", "+00:00"))
    age = (datetime.datetime.now(datetime.timezone.utc) - updated).total_seconds()
    if not -120 <= age <= 120:
        raise ValueError("Treasury snapshot is stale")
    amounts = {}
    for key in ("currentBalance", "unlockedBalance", "immatureBalance", "accruedRewards", "totalSpent"):
        try:
            value = Decimal(str(status[key]))
            if not value.is_finite() or value < 0 or value != value.quantize(Decimal("0.00000001")):
                raise ValueError("Invalid treasury amount")
        except (KeyError, InvalidOperation) as error:
            raise ValueError("Treasury amounts are missing or invalid") from error
        amounts[key] = value
    if amounts["currentBalance"] != amounts["unlockedBalance"] + amounts["immatureBalance"]:
        raise ValueError("Treasury balances do not add up")
    return status


def publish(source, output, network, address):
    # Read a bounded local snapshot, not an unauthenticated public node response.
    with source.open("rb") as file:
        raw = file.read(1048577)
    if len(raw) > 1048576:
        raise ValueError("Treasury snapshot exceeds size limit")
    status = validate(json.loads(raw), network, address)
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(mode="w", dir=output.parent, delete=False, encoding="utf-8") as file:
            temporary = Path(file.name)
            json.dump(status, file, indent=2)
            file.write("\n")
            file.flush()
            os.fsync(file.fileno())
        temporary.chmod(0o644)
        os.replace(temporary, output)
    finally:
        if temporary and temporary.exists():
            temporary.unlink()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True, type=Path, help="Local node snapshot from export-node-snapshots.py")
    parser.add_argument("--network", required=True)
    parser.add_argument("--address", required=True, help="Expected public treasury address")
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    publish(args.source, args.output, args.network, args.address)
