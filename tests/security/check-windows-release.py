"""Check the staged release and exercise the copied updater in disposable folders."""
import argparse
from pathlib import Path
import subprocess
import uuid
import zipfile

p = argparse.ArgumentParser()
p.add_argument('release', type=Path)
p.add_argument('fixture', type=Path)
args = p.parse_args()
root = Path('review/security-fixes') / ('roundtrip-' + uuid.uuid4().hex)
root.mkdir(parents=True)
app = args.release.resolve() / 'Slithy Tove.exe'
for test in ('update-helper', 'updates', 'update-rollback', 'network-reset', 'treasury'):
    result = subprocess.run([str(app), '--self-test-' + test], capture_output=True, timeout=90)
    (root / (test + '.log')).write_bytes(result.stdout + result.stderr)
    if result.returncode:
        raise RuntimeError(f'{test} failed: {result.returncode}')
    print('PASS', test, flush=True)
fixture = args.fixture.resolve()
for mode in ('success', 'rollback'):
    package = root / (mode + '.zip')
    with zipfile.ZipFile(package, 'w') as z:
        for file in fixture.iterdir():
            if file.is_file():
                z.write(file, file.name)
        z.writestr('introduced-marker', 'new file')
        if mode == 'rollback':
            z.writestr('fail-startup', 'simulate failed startup')
    result = subprocess.run([str(fixture / 'Slithy Tove.exe'), str((root / mode).resolve()),
                             str(package.resolve()), mode], capture_output=True, timeout=90)
    (root / (mode + '.log')).write_bytes(result.stdout + result.stderr)
    if result.returncode:
        raise RuntimeError(f'{mode} failed: {result.returncode}')
    print('PASS copied-runtime ZIP update:', mode, flush=True)
print('Evidence:', root)
