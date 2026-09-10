#!/usr/bin/env python3
"""Exercise the menu gate with fake tools. No node or wallet is started."""
import hashlib
import json
import os
import pathlib
import pty
import select
import shutil
import subprocess
import tempfile
import time

repo = pathlib.Path(__file__).resolve().parents[2]
with tempfile.TemporaryDirectory(prefix='slithy-terms-') as scratch:
    root = pathlib.Path(scratch)
    binary = root / 'bin'
    binary.mkdir()
    for name in ('slithy', 'slithy-common.sh'):
        (binary / name).write_text((repo / 'site/install/linux' / name).read_text())
    terms = binary / 'slithy-terms.txt'
    terms.write_text((repo / 'licenses/BETA-TERMS.txt').read_text())
    for name in ('slithyd', 'slithy-cli'):
        tool = binary / name
        tool.write_text('#!/bin/sh\nprintf "%s\\n" "$*" >> "$CALL_LOG"\ncase "$*" in *getblockhash*) echo 000f888cdb70403cd5310d02d7983951ee146ac799485bca337a7b52c24643f2;; *) echo "{}";; esac\n')
        tool.chmod(0o755)
    env = dict(os.environ, HOME=str(root), SLITHY_BIN_DIR=str(binary),
               SLITHY_CONFIG_DIR=str(root / 'config'), SLITHY_SYSTEM_CONFIG_FILE=str(root / 'absent'),
               SLITHY_TERMS_STATE_DIR=str(root / 'agreement'), SLITHY_USE_SUDO='0',
               SLITHY_RPC_PASSWORD='test-fixture', CALL_LOG=str(root / 'calls'), TERM='xterm')
    record = root / 'agreement/terms-acceptance.json'

    def run(command):
        return subprocess.run(['bash', str(binary / 'slithy'), command], env=env,
                              input='', text=True, capture_output=True, timeout=10)

    def interactive(answer):
        master, slave = pty.openpty()
        process = subprocess.Popen(['bash', str(binary / 'slithy'), 'test-no-operation'],
                                   env=env, stdin=slave, stdout=slave, stderr=slave)
        os.close(slave)
        output = b''
        deadline = time.monotonic() + 10
        sent = False
        try:
            while time.monotonic() < deadline:
                ready, _, _ = select.select([master], [], [], 0.1)
                if ready:
                    try:
                        data = os.read(master, 65536)
                    except OSError:
                        break
                    if not data:
                        break
                    output += data
                    if b'Your choice:' in output and not sent:
                        os.write(master, answer.encode() + b'\n')
                        sent = True
                if process.poll() is not None:
                    break
            process.wait(timeout=2)
        finally:
            if process.poll() is None:
                process.kill()
                process.wait()
            os.close(master)
        assert sent, output.decode(errors='replace')
        return output.decode(errors='replace')

    assert run('create-wallet').returncode != 0
    assert not record.exists() and not (root / 'calls').exists()
    assert run('wallet-files').returncode == 0
    assert run('terms').returncode == 0
    stopped = run('stop-mining')
    assert stopped.returncode == 0, stopped.stdout + stopped.stderr
    assert 'stopcpumining' in (root / 'calls').read_text()
    print('PASS Noninteractive start blocked; wallet paths, terms and stop commands remain available')
    interactive('no')
    assert not record.exists()
    print('PASS Decline leaves acceptance unset')
    interactive('AGREE')
    value = json.loads(record.read_text())
    assert value['version'] == '1.0' and value['sha256'] == hashlib.sha256(terms.read_bytes()).hexdigest()
    assert record.stat().st_mode & 0o777 == 0o600
    assert 'Agreement needed' not in run('test-no-operation').stderr
    print('PASS Explicit agreement stored privately and remembered')
    terms.write_text(terms.read_text() + '\nRevised test content\n')
    assert 'Agreement needed' in run('create-wallet').stderr
    print('PASS Changed terms require agreement')
    record.write_text('broken')
    assert 'Agreement needed' in run('create-wallet').stderr
    terms.unlink()
    assert run('terms').returncode != 0
    assert run('create-wallet').returncode != 0
    assert run('wallet-files').returncode == 0
    print('PASS Missing terms and corrupt records fail closed without hiding wallet paths')
    terms.write_text((repo / 'licenses/BETA-TERMS.txt').read_text())
    record.unlink()
    record.parent.rmdir()
    record.parent.write_text('File blocks creation of the record directory')
    assert 'could not be saved' in interactive('AGREE')
    print('PASS Save failure does not start an action')
