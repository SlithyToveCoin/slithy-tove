#!/usr/bin/env python3
import json
import os
import subprocess
import time
import threading
from decimal import Decimal
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


PORT = int(os.environ.get("SLITHY_STATUS_PORT", "42429"))
NODE_NAME = os.environ.get("SLITHY_NODE_NAME", "slithy-node")
NODE_REGION = os.environ.get("SLITHY_NODE_REGION", "")
TREASURY_SCRIPT = os.environ.get("SLITHY_TREASURY_SCRIPT", "")
TREASURY_ADDRESS = os.environ.get("SLITHY_TREASURY_ADDRESS", "")


def cli(*args):
    # Select one configured node. Process discovery could pick a user's miner.
    cli_bin = os.environ.get("SLITHY_STATUS_CLI", "")
    config = os.environ.get("SLITHY_STATUS_CONFIG", "")
    if not os.path.isabs(cli_bin) or not os.path.isabs(config):
        raise RuntimeError("Set absolute SLITHY_STATUS_CLI and SLITHY_STATUS_CONFIG paths.")
    cmd = [cli_bin, f"-conf={config}", *args]
    return subprocess.check_output(cmd, text=True, timeout=5, stderr=subprocess.DEVNULL).strip()


def cli_json(*args):
    value = cli(*args)
    return json.loads(value) if value else {}


# The index follows actual transactions, including spending and ordinary
# incoming payments. It runs in the collector, never in an HTTP request.
_treasury_height = 0
_treasury_hash = ""
_treasury_utxos = {}
_treasury_rewards = Decimal(0)
_treasury_spent = Decimal(0)


def reset_treasury_index():
    global _treasury_height, _treasury_hash, _treasury_rewards, _treasury_spent
    _treasury_height = 0
    _treasury_hash = ""
    _treasury_rewards = Decimal(0)
    _treasury_spent = Decimal(0)
    _treasury_utxos.clear()


def treasury_status(height):
    try:
        return advance_treasury_index(height)
    except Exception:
        # A failed partial scan must not be counted a second time on retry.
        reset_treasury_index()
        raise


def read_block(block_hash):
    return json.loads(cli("getblock", block_hash, "2"), parse_float=Decimal)


def advance_treasury_index(height):
    global _treasury_height, _treasury_hash, _treasury_rewards, _treasury_spent
    if not TREASURY_SCRIPT or not TREASURY_ADDRESS:
        return None
    if _treasury_height > height or (_treasury_height and cli("getblockhash", str(_treasury_height)) != _treasury_hash):
        reset_treasury_index()
    deadline = time.monotonic() + 5
    for block_height in range(_treasury_height + 1, min(height, _treasury_height + 100) + 1):
        block_hash = cli("getblockhash", str(block_height))
        block = read_block(block_hash)
        if _treasury_hash and block.get("previousblockhash") != _treasury_hash:
            reset_treasury_index()
            return None
        for tx in block.get("tx", []):
            is_coinbase = any("coinbase" in v for v in tx.get("vin", []))
            spent_value = Decimal(0)
            received_value = Decimal(0)
            for vin in tx.get("vin", []):
                spent = _treasury_utxos.pop((vin.get("txid"), vin.get("vout")), None)
                if spent:
                    spent_value += spent[0]
            for output in tx.get("vout", []):
                if output.get("scriptPubKey", {}).get("hex") != TREASURY_SCRIPT:
                    continue
                value = Decimal(str(output["value"]))
                received_value += value
                _treasury_utxos[(tx["txid"], output["n"])] = (value, block_height, is_coinbase)
                if is_coinbase:
                    _treasury_rewards += value
            # Change returned to the treasury is not an external payment.
            # Net spending includes the network fee paid by the treasury.
            if spent_value:
                _treasury_spent += max(Decimal(0), spent_value - received_value)
        _treasury_height, _treasury_hash = block_height, block_hash
        if time.monotonic() >= deadline:
            break
    if _treasury_height != height:
        return None
    # Refuse a mixed-chain snapshot if a reorg arrived during collection.
    if height and cli("getblockhash", str(height)) != _treasury_hash:
        reset_treasury_index()
        return None
    current = sum((value for value, _, _ in _treasury_utxos.values()), Decimal(0))
    unlocked = sum((value for value, at, coinbase in _treasury_utxos.values()
                    if not coinbase or height - at + 1 >= 100), Decimal(0))
    return {
        "treasuryAddress": TREASURY_ADDRESS,
        "accruedRewards": f"{_treasury_rewards:.8f}",
        "currentBalance": f"{current:.8f}",
        "unlockedBalance": f"{unlocked:.8f}",
        "immatureBalance": f"{current - unlocked:.8f}",
        "totalSpent": f"{_treasury_spent:.8f}",
    }


def block_summary(block_height, chain_height):
    block_hash = cli("getblockhash", str(block_height))
    block = cli_json("getblock", block_hash, "2")
    txs = block.get("tx", [])
    confirmations = chain_height - block_height + 1
    coinbase_value = 0.0
    treasury_value = 0.0

    if txs:
        for output in txs[0].get("vout", []):
            value = float(output.get("value", 0))
            script = output.get("scriptPubKey", {}).get("hex", "")
            if TREASURY_SCRIPT and script == TREASURY_SCRIPT:
                treasury_value += value
            else:
                coinbase_value += value

    return {
        "height": block_height,
        "hash": block_hash,
        "time": int(block.get("time", 0) or 0),
        "medianTime": int(block.get("mediantime", 0) or 0),
        "confirmations": confirmations,
        "transactionCount": len(txs),
        "size": int(block.get("size", 0) or 0),
        "weight": int(block.get("weight", 0) or 0),
        "difficulty": block.get("difficulty", 0),
        "minerSubsidyEstimate": f"{coinbase_value:.8f}",
        "treasurySubsidyEstimate": f"{treasury_value:.8f}",
    }


_explorer_tip = ""
_explorer_blocks = []


def build_explorer():
    global _explorer_tip, _explorer_blocks
    chain = cli_json("getblockchaininfo")
    network = cli_json("getnetworkinfo")
    peers = cli_json("getpeerinfo")
    mining = {}
    try:
        mining = cli_json("getcpumininginfo")
    except Exception:
        pass

    height = int(chain.get("blocks", 0))
    first_height = max(0, height - 24)
    tip = chain.get("bestblockhash", "")
    if tip != _explorer_tip:
        recent_blocks = [block_summary(at, height) for at in range(height, first_height - 1, -1)]
        if cli("getblockhash", str(height)) != tip:
            raise RuntimeError("Chain changed during explorer collection")
        _explorer_blocks = recent_blocks
        _explorer_tip = tip

    return {
        "network": "slithy-" + str(chain.get("chain", "unknown")),
        "node": NODE_NAME,
        "region": NODE_REGION,
        "chain": chain.get("chain", ""),
        "height": height,
        "headers": int(chain.get("headers", 0)),
        "bestBlockHash": chain.get("bestblockhash", ""),
        "difficulty": chain.get("difficulty", 0),
        "initialBlockDownload": bool(chain.get("initialblockdownload", False)),
        "peerCount": len(peers) if isinstance(peers, list) else 0,
        "version": str(network.get("subversion", "")).strip("/"),
        "mining": {
            "active": mining.get("active"),
            "threads": int(mining.get("threads", 0) or 0),
            "speed": int(mining.get("speed", 0) or 0),
            "algorithm": mining.get("algorithm", ""),
        },
        "blocks": _explorer_blocks,
        "updatedAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }


def build_status():
    chain = cli_json("getblockchaininfo")
    network = cli_json("getnetworkinfo")
    peers = cli_json("getpeerinfo")
    mining = {}
    try:
        mining = cli_json("getcpumininginfo")
    except Exception:
        pass

    height = int(chain.get("blocks", 0))
    payload = {
        "network": {"test": "slithy-testnet", "main": "slithy-mainnet", "regtest": "regtest"}.get(chain.get("chain"), "unknown"),
        "node": NODE_NAME,
        "region": NODE_REGION,
        "chain": chain.get("chain", ""),
        "height": height,
        "chainHeight": height,
        "headers": int(chain.get("headers", 0)),
        "bestBlockHash": chain.get("bestblockhash", ""),
        "difficulty": chain.get("difficulty", 0),
        "initialBlockDownload": bool(chain.get("initialblockdownload", False)),
        "peerCount": len(peers) if isinstance(peers, list) else 0,
        "version": str(network.get("subversion", "")).strip("/"),
        "p2pPort": 53424,
        "rpcPublic": False,
        "mining": {
            "active": mining.get("active") if isinstance(mining.get("active"), bool) else None,
            "threads": int(mining.get("threads", 0) or 0),
            "speed": int(mining.get("speed", 0) or 0),
            "algorithm": mining.get("algorithm", ""),
        },
        "updatedAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }

    treasury = treasury_status(height)
    if treasury:
        payload.update(treasury)
    return payload


_snapshots = {}
_snapshot_lock = threading.Lock()


def collect_snapshots():
    while True:
        for name, builder in (("status", build_status), ("explorer", build_explorer)):
            try:
                snapshot = builder()
                with _snapshot_lock:
                    _snapshots[name] = (time.monotonic(), snapshot)
            except Exception as error:
                # Do not return CLI arguments, paths or authentication errors to visitors.
                print(f"Could not refresh {name}: {type(error).__name__}", flush=True)
        time.sleep(15)


class StatusServer(ThreadingHTTPServer):
    daemon_threads = True
    request_queue_size = 32
    slots = threading.BoundedSemaphore(16)

    def get_request(self):
        connection, address = super().get_request()
        connection.settimeout(5)
        return connection, address

    def process_request(self, request, client_address):
        if not self.slots.acquire(blocking=False):
            self.shutdown_request(request)
            return
        try:
            super().process_request(request, client_address)
        except Exception:
            self.slots.release()
            raise

    def process_request_thread(self, request, client_address):
        try:
            super().process_request_thread(request, client_address)
        finally:
            self.slots.release()


class Handler(BaseHTTPRequestHandler):
    def send_payload(self, code, payload):
        body = json.dumps(payload, indent=2).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Cache-Control", "public, max-age=15")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        path = self.path.split("?", 1)[0]
        if len(self.path) > 2048:
            self.send_payload(414, {"error": "request path too long"})
            return
        if path not in ("/", "/status.json", "/treasury.json", "/data/treasury.json", "/explorer.json"):
            self.send_payload(404, {"error": "not found"})
            return
        name = "explorer" if path == "/explorer.json" else "status"
        with _snapshot_lock:
            cached = _snapshots.get(name)
        if not cached or time.monotonic() - cached[0] > 120:
            self.send_payload(503, {"error": "node status is unavailable", "node": NODE_NAME})
            return
        if "treasury.json" in path and "currentBalance" not in cached[1]:
            self.send_payload(503, {"error": "treasury index is catching up", "node": NODE_NAME})
            return
        self.send_payload(200, cached[1])

    def log_message(self, fmt, *args):
        return


if __name__ == "__main__":
    threading.Thread(target=collect_snapshots, daemon=True).start()
    StatusServer((os.environ.get("SLITHY_STATUS_BIND", "127.0.0.1"), PORT), Handler).serve_forever()
