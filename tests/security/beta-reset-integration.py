#!/usr/bin/env python3
"""Exercise the replacement beta and a previous beta on loopback, never public peers."""
import argparse
import importlib.util
import os
from pathlib import Path
import tempfile
import time
import subprocess
from urllib.parse import quote

spec = importlib.util.spec_from_file_location("core_fixture", Path(__file__).with_name("core-mining-integration.py"))
fixture = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fixture)
Node, wait = fixture.Node, fixture.wait

GENESIS = "000f888cdb70403cd5310d02d7983951ee146ac799485bca337a7b52c24643f2"
OLD_GENESIS = "5194b67c90ed692062c1ba8971917818fdb433006b3b349eb9538b7a3b4ce41c"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--bin", type=Path, required=True)
    parser.add_argument("--old-bin", type=Path, required=True)
    parser.add_argument("--work", type=Path, required=True)
    parser.add_argument("--retargets", action="store_true", help="Mine across the first two actual beta retarget boundaries")
    parser.add_argument("--windows-app", type=Path, help="Test Windows readiness with an aged genesis")
    args = parser.parse_args()
    root = Path(tempfile.mkdtemp(prefix="beta-reset-test-", dir=args.work))
    name = "slithyd.exe" if os.name == "nt" else "slithyd"
    nodes = []
    try:
        old = Node(args.old_bin / name, root / "previous", "-testnet", "testnet3")
        nodes.append(old)
        assert old.call("getblockhash", 0) == OLD_GENESIS
        old.call("createwallet", "old-beta-fixture")
        old_address = old.call("getnewaddress", wallet="old-beta-fixture")
        old.stop()
        # Starting a new binary in the same datadir must not read the old database.
        before = {p.relative_to(old.root): p.read_bytes() for p in (old.root / "testnet3").rglob("*") if p.is_file()}
        preserved = old.root
        extra = ("-maxtipage=1", "-deprecatedrpc=startingheight") if args.windows_app else ()
        a = Node(args.bin / name, preserved, "-testnet", "beta-20260909", extra)
        nodes.append(a)
        b = Node(args.bin / name, root / "new-b", "-testnet", "beta-20260909", extra)
        nodes.append(b)
        for node in (a, b):
            assert node.call("getblockhash", 0) == GENESIS
            genesis = node.call("getblock", GENESIS, 2)
            assert sum(v["value"] for v in genesis["tx"][0]["vout"]) == 0
            assert node.call("listwallets") == []
        print("PASS: new beta genesis agrees, starts at zero reward and opens no old wallet", flush=True)
        def app_ready(expected):
            user, password = a.cookie.split(":", 1)
            env = os.environ | {"SLITHY_FIXTURE_RPC": f"http://{quote(user)}:{quote(password)}@127.0.0.1:{a.rpcport}",
                                "SLITHY_FIXTURE_READY": str(expected).lower()}
            result = subprocess.run([str(args.windows_app.resolve()), "--self-test-bootstrap-node"], env=env,
                                    capture_output=True, text=True, timeout=30)
            assert result.returncode == 0, result.stdout + result.stderr
        if args.windows_app:
            assert a.call("getblockchaininfo")["initialblockdownload"]
            app_ready(False)
        a.call("addnode", f"127.0.0.1:{b.p2pport}", "onetry")
        wait(lambda: any(peer.get("version", 0) > 0 for peer in a.call("getpeerinfo")))
        if args.windows_app:
            print("Bootstrap peer fields:", [{k: v for k, v in peer.items() if k in
                  ("version", "startingheight", "synced_blocks", "synced_headers", "blocks", "presynced_headers")}
                  for peer in a.call("getpeerinfo")], flush=True)
            wait(lambda: any(peer.get("version", 0) > 0 and peer.get("startingheight", -1) == 0
                             for peer in a.call("getpeerinfo")))
            app_ready(True)
            print("PASS: Windows rejects offline bootstrap and accepts a known zero-height peer despite aged genesis", flush=True)
        # Start the old binary again, using a distinct fixture root for its process.
        legacy = Node(args.old_bin / name, root / "legacy-peer", "-testnet", "testnet3")
        nodes.append(legacy)
        legacy.call("addnode", f"127.0.0.1:{a.p2pport}", "onetry")
        time.sleep(3)
        assert not any(peer.get("version", 0) > 0 for peer in legacy.call("getpeerinfo"))
        assert legacy.call("getblockcount") == 0
        print("PASS: previous beta cannot complete a peer handshake with the replacement network", flush=True)
        a.call("createwallet", "new-beta-fixture")
        address = a.call("getnewaddress", wallet="new-beta-fixture")
        assert address != old_address
        a.call("startcpumining", address, 1)
        wait(lambda: a.call("getblockcount") >= 2, seconds=300)
        a.call("stopcpumining")
        wait(lambda: a.call("getbestblockhash") == b.call("getbestblockhash"))
        block = a.call("getblock", a.call("getblockhash", 1), 2)
        amounts = [v["value"] for v in block["tx"][0]["vout"]]
        assert sum(amounts) == 10 and 1 in amounts and 9 in amounts
        print("PASS: actual beta CPU mining pays 9 plus 1 SLTHY and propagates to the second node", flush=True)
        if args.retargets:
            a.call("startcpumining", address, 1)
            wait(lambda: a.call("getblockcount") >= 61, seconds=1200)
            a.call("stopcpumining")
            wait(lambda: a.call("getbestblockhash") == b.call("getbestblockhash"))
            for height in (30, 60):
                first = a.call("getblockheader", a.call("getblockhash", height - 30))
                last = a.call("getblockheader", a.call("getblockhash", height - 1))
                current = a.call("getblockheader", a.call("getblockhash", height))
                bits = int(last["bits"], 16)
                target = (bits & 0x7fffff) << (8 * ((bits >> 24) - 3))
                elapsed = max(900, min(14400, last["time"] - first["time"]))
                target = min((1 << 256) - 1, target * elapsed // 3600)
                size = (target.bit_length() + 7) // 8
                compact = target << (8 * (3 - size)) if size <= 3 else target >> (8 * (size - 3))
                if compact & 0x800000:
                    compact >>= 8
                    size += 1
                expected = compact | (size << 24)
                assert int(current["bits"], 16) == expected
                print(f"PASS: beta retarget at {height} matches exact arithmetic ({current['bits']})", flush=True)
        assert all((preserved / path).read_bytes() == data for path, data in before.items())
        print("PASS: previous beta wallet and chain files are unchanged", flush=True)
    finally:
        for node in nodes:
            if not node.output.closed:
                node.stop()
        print("Isolated reset evidence:", root, flush=True)


if __name__ == "__main__":
    main()
