"""Exercise the real installer in a disposable desktop session, without a node."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time

from desktop_smoke import X11, wait_for

parser = argparse.ArgumentParser()
parser.add_argument('package', type=Path)
parser.add_argument('output', type=Path)
args = parser.parse_args()
package, output = args.package.resolve(), args.output.resolve()
output.mkdir(parents=True, exist_ok=True)
home = Path(os.environ['HOME']).resolve()
assert str(home).startswith('/tmp/slithy-xfce-test-'), 'Use xfce-session.py, not your desktop profile.'
apps = home / '.local/opt/slithy-tove'
assert not apps.exists(), 'This test requires a new disposable home.'
data = Path(os.environ['XDG_DATA_HOME'])
wallet = data / 'slithy-tove-gui/beta-20260909/wallets/canary/wallet.dat'
wallet.parent.mkdir(parents=True)
wallet.write_bytes(b'Fixture: preserve this file. Not an actual wallet.')
wallet_hash = hashlib.sha256(wallet.read_bytes()).hexdigest()
launcher = data / 'applications/io.slithy.Tove.desktop'
env = dict(os.environ, HTTP_PROXY='http://127.0.0.1:1', HTTPS_PROXY='http://127.0.0.1:1', NO_PROXY='127.0.0.1,localhost')
checks = []

def passed(label):
    checks.append(label)
    print('PASS: ' + label, flush=True)

def open_window(binary, title, arguments=()):
    process = subprocess.Popen([str(binary), *arguments], env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    window = x.window(title, timeout=40)
    time.sleep(3)
    return process, window

def finish(process, window):
    x.click(window, x.geometry(window)['WIDTH'] - 20, 16)
    process.wait(timeout=20)
    assert process.returncode == 0

def action_y(window):
    width, height, rgb = x.pixels(window)
    # Find the enabled green action after package verification has finished.
    # The destination can wrap differently on each machine.
    column = width - 42
    rows = [row for row in range(240, height - 30)
            if rgb[(row * width + column) * 3:(row * width + column) * 3 + 3] == bytes((48, 72, 59))]
    return rows[len(rows) // 2] if len(rows) >= 12 else None

def click_action(window):
    row = wait_for(lambda: action_y(window), 'verified package action', timeout=120)
    x.click(window, x.geometry(window)['WIDTH'] - 42, row)

x = X11(env)
process = None
try:
    process, window = open_window(package / 'slithy-install', 'Install Slithy Tove', ['--preview'])
    click_action(window)
    assert not apps.exists() and not launcher.exists()
    passed('installer preview cannot install files')
    finish(process, window)
    process = None
    process, window = open_window(package / 'slithy-install', 'Install Slithy Tove')
    assert x.geometry(window)['WIDTH'] == 620
    assert x.prop(window, '_NET_WM_ICON')
    wait_for(lambda: action_y(window), 'installer verification', timeout=120)
    x.screenshot(output / 'install-ready.png', window)
    passed('renamed installer opens the branded installation window')
    click_action(window)
    wait_for(launcher.exists, 'installed launcher', timeout=60)
    time.sleep(1)
    x.screenshot(output / 'installed.png', window)
    targets = list(apps.glob('*/slithy-desktop'))
    assert len(targets) == 1
    target = targets[0].parent
    assert hashlib.sha256((target / 'slithy-desktop.dll').read_bytes()).digest() == hashlib.sha256((package / 'slithy-desktop.dll').read_bytes()).digest()
    subprocess.run(['desktop-file-validate', str(launcher)], check=True)
    assert '[Desktop Action Uninstall]' in launcher.read_text()
    passed('real package installed and launcher validated')
    finish(process, window)
    process = None
    note = target / 'my-note.txt'
    note.write_text('User file, not part of the package.')
    process, window = open_window(target / 'slithy-uninstall', 'Uninstall Slithy Tove')
    wait_for(lambda: action_y(window), 'uninstaller verification', timeout=120)
    x.screenshot(output / 'uninstall-ready.png', window)
    click_action(window)
    wait_for(lambda: not launcher.exists(), 'removed launcher', timeout=60)
    x.screenshot(output / 'uninstalled.png', window)
    assert not (target / 'slithy-desktop').exists()
    assert note.read_text() == 'User file, not part of the package.'
    assert hashlib.sha256(wallet.read_bytes()).hexdigest() == wallet_hash
    passed('graphical uninstall removes program while preserving wallet and extra file')
    finish(process, window)
    process = None
    assert not list(data.rglob('gui-node-owner.json'))
    passed('install and uninstall did not start a node')
finally:
    if process is not None and process.poll() is None:
        process.terminate()
        process.wait(timeout=15)
    x.close()
    (output / 'checks.json').write_text(json.dumps(checks, indent=2) + '\n')
