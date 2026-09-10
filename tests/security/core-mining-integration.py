#!/usr/bin/env python3
"""Two loopback regtest nodes. No public peers, installed nodes or real wallets."""
import argparse
import base64
import concurrent.futures
import datetime
from decimal import Decimal
import hashlib
import http.client
import io
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import socket
import subprocess
import sys
import tempfile
import time
from urllib.parse import quote


def wait(check, seconds=30):
    end = time.monotonic() + seconds
    while time.monotonic() < end:
        try:
            result = check()
            if result:
                return result
        except (OSError, ValueError, RuntimeError):
            pass
        time.sleep(0.1)
    raise AssertionError("Timed out waiting for isolated node condition")


def port():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return listener.getsockname()[1]


class Node:
    def __init__(self, binary, root, chain="-regtest", subdirectory="regtest", extra_args=()):
        root.mkdir(exist_ok=True)
        self.root, self.rpcport, self.p2pport = root, port(), port()
        self.output = (root / "process.log").open("w")
        self.process = subprocess.Popen([str(binary), chain, "-server=1", "-listen=1",
            "-bind=127.0.0.1", f"-port={self.p2pport}", "-rpcbind=127.0.0.1",
            f"-rpcport={self.rpcport}", f"-datadir={root}", "-connect=0", "-dnsseed=0",
            "-discover=0", "-listenonion=0", "-debug=validation", "-fallbackfee=0.0001", *extra_args],
            stdout=self.output, stderr=subprocess.STDOUT)
        wait(lambda: (root / subdirectory / ".cookie").exists())
        self.cookie = (root / subdirectory / ".cookie").read_text().strip()
        wait(lambda: self.call("getblockchaininfo"))

    def call(self, method, *params, wallet=None):
        connection = http.client.HTTPConnection("127.0.0.1", self.rpcport, timeout=30)
        try:
            path = "/" if wallet is None else "/wallet/" + quote(wallet, safe="")
            connection.request("POST", path, json.dumps(dict(jsonrpc="2.0", id=1, method=method, params=params)),
                               {"Authorization": "Basic " + base64.b64encode(self.cookie.encode()).decode(), "Content-Type": "application/json"})
            response = connection.getresponse()
            value = json.loads(response.read(), parse_float=Decimal)
            if value.get("error"):
                raise RuntimeError(str(value["error"]))
            return value["result"]
        finally:
            connection.close()

    def stop(self):
        if self.process.poll() is None:
            try:
                self.call("stop")
                self.process.wait(20)
            except Exception:
                self.process.terminate()
                try: self.process.wait(10)
                except subprocess.TimeoutExpired:
                    self.process.kill()
                    self.process.wait()
        self.output.close()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--bin", required=True, type=Path)
    parser.add_argument("--work", required=True, type=Path)
    parser.add_argument("--seed-tool", type=Path)
    parser.add_argument("--linux-menu", type=Path)
    parser.add_argument("--status-server", type=Path)
    parser.add_argument("--core-source", type=Path,
                        help="Matching core source with test/functional/test_framework")
    args = parser.parse_args()
    for name in ("bin", "work", "seed_tool", "linux_menu", "status_server", "core_source"):
        value = getattr(args, name)
        if value is not None:
            setattr(args, name, value.resolve())
    root = Path(tempfile.mkdtemp(prefix="core-test-", dir=args.work))
    nodes = []
    try:
        daemon = args.bin / ("slithyd.exe" if os.name == "nt" else "slithyd")
        a = Node(daemon, root / "a")
        nodes.append(a)
        b = Node(daemon, root / "b")
        nodes.append(b)
        for node in nodes:
            node.call("createwallet", "fixture")
        addresses = [n.call("getnewaddress", wallet="fixture") for n in nodes]
        a.call("addnode", f"127.0.0.1:{b.p2pport}", "onetry")
        wait(lambda: a.call("getpeerinfo") and b.call("getpeerinfo"))
        a.call("generatetoaddress", 1, addresses[0])
        wait(lambda: a.call("getbestblockhash") == b.call("getbestblockhash"))
        first = a.call("getblock", a.call("getblockhash", 1), 2)["tx"][0]
        treasury_script = "0014e3b65814002d9f1637fc852be0a8da2f00cbd1ec"
        outputs = first["vout"]
        assert sum(v["value"] for v in outputs) == 10
        treasury = next(v for v in outputs if v["scriptPubKey"]["hex"] == treasury_script)
        assert treasury["value"] == 1
        treasury_address = treasury["scriptPubKey"]["address"]
        try: a.call("startcpumining", treasury_address, 1)
        except RuntimeError as error: assert "treasury" in str(error)
        else: raise AssertionError("Treasury mining address was accepted")
        print("PASS: 10 SLTHY subsidy, 1 SLTHY treasury and treasury-address mining rejection", flush=True)
        treasury_rejection_test(a, b, addresses[0], treasury_script,
                                args.core_source or args.bin.parent.parent / "core")
        start = a.call("getblockcount")
        for node in nodes: node.call("startcpumining", addresses[0], 1)
        wait(lambda: a.call("getblockcount") >= start + 6)
        for node in nodes: node.call("stopcpumining")
        # Equal-work branches are allowed to retain their first-seen tip.
        # Extend the taller branch once so convergence has a unique answer.
        if a.call("getbestblockhash") != b.call("getbestblockhash"):
            winner = max(range(2), key=lambda i: nodes[i].call("getblockcount"))
            nodes[winner].call("generatetoaddress", 1, addresses[winner])
        wait(lambda: a.call("getbestblockhash") == b.call("getbestblockhash"))
        for node in nodes:
            info = node.call("getcpumininginfo")
            assert info["active"] is False and info["threads"] == 0
        print("PASS: competing CPU miners propagate blocks and stop cleanly", flush=True)
        # Concurrent RPC transitions must not leave an orphan worker behind.
        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
            futures = [pool.submit(a.call, "startcpumining", addresses[0], 2) if i % 2 == 0 else pool.submit(a.call, "stopcpumining") for i in range(8)]
            for future in futures: future.result(30)
        a.call("stopcpumining")
        assert a.call("getcpumininginfo")["threads"] == 0
        print("PASS: concurrent mining start/stop requests leave no workers", flush=True)
        wait(lambda: a.call("getbestblockhash") == b.call("getbestblockhash"))
        # Make separate branches, then reconnect and require most-work agreement.
        a.call("setnetworkactive", False)
        a.call("generatetoaddress", 2, addresses[0])
        b.call("generatetoaddress", 5, addresses[1])
        expected = b.call("getbestblockhash")
        a.call("setnetworkactive", True)
        a.call("addnode", f"127.0.0.1:{b.p2pport}", "onetry")
        wait(lambda: a.call("getbestblockhash") == expected)
        print("PASS: isolated competing branches reorganize to the higher-work chain", flush=True)
        if args.seed_tool:
            recovery_test(a, args.seed_tool, root, args.bin, args.linux_menu)
        if args.status_server:
            status_test(a, args.status_server, args.bin, root, treasury_script, treasury_address)
        a.call("startcpumining", addresses[0], 2)
        wait(lambda: a.call("getcpumininginfo")["threads"] == 2)
        a.call("stop")
        a.process.wait(20)
        assert a.process.returncode == 0
        print("PASS: daemon exits cleanly with active CPU workers", flush=True)
        fingerprints = set()
        for node in nodes:
            text = (node.root / "regtest/debug.log").read_text()
            work = re.findall(r"Slithy CPU work id=(\d+) parent=(\w+) merkle=(\w+)", text)
            assert len(work) >= 2
            assert len({w[0] for w in work}) == len(work)
            assert len({(w[1], w[2]) for w in work}) == len(work)
            for _, parent, merkle in work:
                assert (parent, merkle) not in fingerprints
                fingerprints.add((parent, merkle))
        print("PASS: worker templates are distinct across computers mining to the same wallet", flush=True)
    finally:
        for node in nodes: node.stop()
        print("Isolated node logs:", root, flush=True)


def treasury_rejection_test(node, peer, address, script, core):
    sys.path.insert(0, str(core / "test/functional"))
    from test_framework.messages import CBlock
    node.call("setnetworkactive", False)
    original = node.call("generatetoaddress", 1, address)[0]
    raw = bytes.fromhex(node.call("getblock", original, 0))
    node.call("invalidateblock", original)
    try:
        for mode in ("wrong-script", "wrong-amount"):
            block = CBlock()
            block.deserialize(io.BytesIO(raw))
            treasury = next(v for v in block.vtx[0].vout if bytes(v.scriptPubKey).hex() == script)
            if mode == "wrong-script":
                treasury.scriptPubKey = bytes(treasury.scriptPubKey[:-1]) + bytes([treasury.scriptPubKey[-1] ^ 1])
            else:
                treasury.nValue += 1
                next(v for v in block.vtx[0].vout if v is not treasury and v.nValue > 1).nValue -= 1
            block.hashMerkleRoot = block.calc_merkle_root()
            for nonce in range(64):
                block.nNonce = nonce
                try: result = node.call("submitblock", block.serialize().hex())
                except RuntimeError as error:
                    if "high-hash" in str(error): continue
                    raise
                if result == "high-hash": continue
                assert result == "bad-cb-treasury", result
                break
            else:
                raise AssertionError("No proof-of-work candidate found within the regtest bound")
        print("PASS: consensus rejects a block with the wrong treasury script or amount", flush=True)
    finally:
        node.call("reconsiderblock", original)
        node.call("setnetworkactive", True)
        node.call("addnode", f"127.0.0.1:{peer.p2pport}", "onetry")
        wait(lambda: node.call("getbestblockhash") == peer.call("getbestblockhash"))


def status_test(node, server, binaries, root, script, address):
    config = root / "status.conf"
    config.write_text(f"regtest=1\ndatadir={node.root}\n[regtest]\nrpcconnect=127.0.0.1\nrpcport={node.rpcport}\n")
    config.chmod(0o600)
    status_port = port()
    env = os.environ | {"SLITHY_STATUS_CLI": str(binaries / "slithy-cli"), "SLITHY_STATUS_CONFIG": str(config),
        "SLITHY_STATUS_PORT": str(status_port), "SLITHY_TREASURY_SCRIPT": script, "SLITHY_TREASURY_ADDRESS": address}
    with (root / "status-process.log").open("w") as log:
        process = subprocess.Popen(["python3", str(server)], env=env, stdout=log, stderr=subprocess.STDOUT)
        def read():
            connection = http.client.HTTPConnection("127.0.0.1", status_port, timeout=5)
            try:
                connection.request("GET", "/treasury.json")
                reply = connection.getresponse()
                result = json.loads(reply.read())
                return result if reply.status == 200 else None
            finally:
                connection.close()
        try:
            snapshot = wait(read, 120)
            assert snapshot["chainHeight"] == node.call("getblockcount")
            assert snapshot["network"] == "regtest" and snapshot["mining"]["active"] is False
            utxos = node.call("scantxoutset", "start", ["addr(" + address + ")"])
            assert Decimal(snapshot["currentBalance"]) == utxos["total_amount"]
            print("PASS: status service treasury balance matches the node UTXO scan", flush=True)
        finally:
            process.terminate()
            process.wait(10)


def recovery_test(node, helper, root, binaries, menu):
    # These well-known BIP39 fixture words never belong to a user's wallet.
    words = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about"
    result = subprocess.run([str(helper), "descriptors", "--network", "regtest"], input=words,
                            capture_output=True, text=True, check=True)
    descriptors = json.loads(result.stdout)
    password = secrets.token_urlsafe(24)
    received = []
    for name in ("seed-original", "seed-restored"):
        node.call("createwallet", name, False, True, password, False, True)
        node.call("walletpassphrase", password, 60, wallet=name)
        requests = []
        for internal, key in ((False, "externalDescriptor"), (True, "internalDescriptor")):
            desc = descriptors[key]
            checksum = node.call("getdescriptorinfo", desc)["checksum"]
            requests.append(dict(desc=desc + "#" + checksum, timestamp=0, active=True, internal=internal, range=[0, 20]))
        imported = node.call("importdescriptors", requests, wallet=name)
        assert all(item["success"] for item in imported)
        node.call("walletlock", wallet=name)
        assert node.call("getwalletinfo", wallet=name)["unlocked_until"] == 0
        received.append(node.call("getnewaddress", wallet=name))
    assert received[0] == received[1]
    node.call("generatetoaddress", 2, received[0])
    first = node.call("getbalances", wallet="seed-original")
    restored = node.call("getbalances", wallet="seed-restored")
    assert first["mine"] == restored["mine"] and first["mine"]["immature"] > 0
    node.call("backupwallet", str(root / "seed-backup.dat"), wallet="seed-original")
    assert (root / "seed-backup.dat").stat().st_size > 0
    print("PASS: Linux recovery words rebuild the encrypted wallet address and mining balance", flush=True)
    if menu:
        tools = root / "menu-bin"
        tools.mkdir()
        for name in ("slithyd", "slithy-cli"):
            (tools / name).symlink_to(binaries / name)
        shutil.copy2(helper, tools / "slithy-seed-tool")
        for name in ("slithy", "slithy-common.sh", "slithy-network.json"):
            shutil.copy2(menu.parent / name, tools / name)
        terms = Path(__file__).resolve().parents[2] / "licenses/BETA-TERMS.txt"
        shutil.copy2(terms, tools / "slithy-terms.txt")
        menu = tools / "slithy"
        user, rpc_password = node.cookie.split(":", 1)
        home = root / "menu-home"
        home.mkdir()
        legacy_config = home / ".config/slithy/menu.conf"
        legacy_config.parent.mkdir(parents=True)
        legacy_config.write_text('WALLET_NAME="old-beta-wallet"\n')
        # Consent UI has its own tests. This fixture never touches a user's agreement.
        agreement = home / "agreement"
        agreement.mkdir(mode=0o700)
        record = agreement / "terms-acceptance.json"
        record.write_text(json.dumps({"version": "1.0", "sha256": hashlib.sha256(terms.read_bytes()).hexdigest(),
            "acceptedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat()}))
        record.chmod(0o600)
        env = os.environ | {"HOME": str(home), "SLITHY_BIN_DIR": str(tools), "SLITHY_CHAIN": "regtest",
            "SLITHY_RPC_PORT": str(node.rpcport), "SLITHY_RPC_USER": user, "SLITHY_RPC_PASSWORD": rpc_password,
            "SLITHY_USE_SUDO": "0", "SLITHY_SYSTEM_CONFIG_FILE": str(home / "absent.conf"),
            "SLITHY_TERMS_STATE_DIR": str(agreement),
            "SLITHY_WALLET_DIR": str(node.root / "regtest/wallets")}
        command = ["bash", str(menu), "restore-words", "seed-menu"]
        result = subprocess.run(command, input=words + "\n" + password + "\n" + password + "\n",
                                env=env, capture_output=True, text=True, timeout=120)
        assert result.returncode == 0, result.stdout + result.stderr
        # Rescanning finds the used address and advances the next receive index.
        # Verify ownership and recovered funds instead of requesting index zero again.
        assert node.call("getaddressinfo", received[0], wallet="seed-menu")["ismine"]
        assert node.call("getbalances", wallet="seed-menu")["mine"] == node.call("getbalances", wallet="seed-original")["mine"]
        assert node.call("getwalletinfo", wallet="seed-menu")["unlocked_until"] == 0
        saved = home / ".config/slithy/beta-20260909/menu.conf"
        assert saved.stat().st_mode & 0o077 == 0
        assert password not in saved.read_text()
        assert legacy_config.read_text() == 'WALLET_NAME="old-beta-wallet"\n'
        print("PASS: real Linux terminal restores words over stdin and relocks the wallet", flush=True)
        recipient = node.call("getnewaddress", wallet="fixture")
        # Small batches fit the RPC timeout on slower contributors' machines.
        for count in [10] * 10 + [1]:
            node.call("generatetoaddress", count, recipient)
        sent = subprocess.run(["bash", str(menu), "send", recipient, "1"], input="send\n" + password + "\n",
                              env=env, capture_output=True, text=True, timeout=60)
        assert sent.returncode == 0, sent.stdout + sent.stderr
        txid = re.search(r"Transaction ID: ([0-9a-f]{64})", sent.stdout)[1]
        payment = node.call("gettransaction", txid, wallet="seed-menu")
        assert payment["fee"] < 0 and payment["amount"] == -1
        node.call("generatetoaddress", 1, recipient)
        assert node.call("gettransaction", txid, wallet="seed-menu")["confirmations"] >= 1
        assert node.call("getwalletinfo", wallet="seed-menu")["unlocked_until"] == 0
        print("PASS: Linux terminal sends a confirmed payment with a real fee and relocks", flush=True)


if __name__ == "__main__":
    main()
