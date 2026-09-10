#!/usr/bin/env python3
"""Explicit public-beta smoke test. Mines on this client, never on an official node."""
import argparse
import importlib.util
from pathlib import Path
import tempfile
import os
import json
import urllib.request

p = argparse.ArgumentParser()
p.add_argument('--bin', required=True, type=Path)
p.add_argument('--work', required=True, type=Path)
p.add_argument('--confirm-public-beta', action='store_true')
args = p.parse_args()
assert args.confirm_public_beta
spec = importlib.util.spec_from_file_location('fixture', Path(__file__).with_name('core-mining-integration.py'))
fixture = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fixture)
root = Path(tempfile.mkdtemp(prefix='public-beta-smoke-', dir=args.work))
node = fixture.Node(args.bin / ('slithyd.exe' if os.name == 'nt' else 'slithyd'), root / 'client', '-testnet', 'beta-20260909')
try:
    assert node.call('getblockhash', 0) == '000f888cdb70403cd5310d02d7983951ee146ac799485bca337a7b52c24643f2'
    peers = ['borogove.slithy.io:53424','mome.slithy.io:53424','rath.slithy.io:53424']
    for peer in peers: node.call('addnode', peer, 'onetry')
    fixture.wait(lambda: len([x for x in node.call('getpeerinfo') if x['version'] > 0]) >= 2, seconds=90)
    # A new node can leave IBD before it has downloaded the current public tip.
    with urllib.request.urlopen('https://slithy.io/data/nodes/borogove.json', timeout=20) as response:
        public_tip = json.load(response)
    fixture.wait(lambda: node.call('getblockcount') >= public_tip['height'] and
                 node.call('getblockhash', public_tip['height']) == public_tip['bestBlockHash'], seconds=120)
    fixture.wait(lambda: not node.call('getblockchaininfo')['initialblockdownload'], seconds=90)
    node.call('createwallet', 'public-beta-smoke')
    address = node.call('getnewaddress', wallet='public-beta-smoke')
    start = node.call('getblockcount')
    node.call('startcpumining', address, 1)
    # Receiving somebody else's blocks is not evidence that this client mined.
    fixture.wait(lambda: node.call('getbalances', wallet='public-beta-smoke')['mine']['immature'] >= 9, seconds=600)
    node.call('stopcpumining')
    rewards = node.call('listtransactions', '*', 100, wallet='public-beta-smoke')
    reward = next(tx for tx in rewards if tx['category'] in ('immature', 'generate') and tx['amount'] >= 9)
    block = node.call('getblock', reward['blockhash'], 2)
    values = [v['value'] for v in block['tx'][0]['vout']]
    assert sum(values) == 10 and 9 in values and 1 in values
    assert any(v['scriptPubKey'].get('address') == address and v['value'] == 9 for v in block['tx'][0]['vout'])
    print('PASS: client mined a public beta reward with 9 SLTHY miner and 1 SLTHY treasury outputs', flush=True)
    node.call('startcpumining', address, 1)
    fixture.wait(lambda: node.call('getcpumininginfo')['active'])
    node.call('stopcpumining')
    assert node.call('getcpumininginfo')['active'] is False
    print('PASS: mining restarts and stops after a reward', flush=True)
    connected = [x for x in node.call('getpeerinfo') if x['version'] > 0]
    removed = connected[0]
    node.call('disconnectnode', removed['addr'])
    fixture.wait(lambda: any(x['version'] > 0 and x['id'] != removed['id'] for x in node.call('getpeerinfo')))
    print('PASS: client retains another compatible public peer after one connection is dropped', flush=True)
    chain = node.call('getblockchaininfo')
    print(json.dumps({'height':chain['blocks'],'tip':chain['bestblockhash'],'chainwork':chain['chainwork']}),flush=True)
finally:
    node.stop()
    print('Client stopped; disposable wallet and evidence:', root, flush=True)
