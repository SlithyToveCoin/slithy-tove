"""Fail on new secret findings. Suppress verified, unchanged upstream test vectors."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument('--gitleaks', default='gitleaks')
parser.add_argument('--history', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
fixtures = json.loads((root / 'tools/upstream-scan-fixtures.json').read_text())
with tempfile.TemporaryDirectory(prefix='slithy-source-scan-') as temporary:
    report = Path(temporary) / 'report.json'
    scan_root = root
    if not args.history:
        # Scan source selected by Git, not ignored builds or disposable test wallets.
        scan_root = Path(temporary) / 'source'
        scan_root.mkdir()
        names = subprocess.check_output(['git', '-C', str(root), 'ls-files',
            '--cached', '--others', '--exclude-standard', '-z']).decode().split('\0')
        for name in sorted(set(filter(None, names))):
            source = root / name
            if source.is_symlink() or not source.resolve().is_relative_to(root):
                raise SystemExit('Review source link before scanning: ' + name)
            if not source.exists():
                continue  # A tracked file deleted in the current working tree.
            target = scan_root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
    command = [args.gitleaks, 'git' if args.history else 'dir', str(scan_root),
               '--redact=100', '--no-banner', '--report-format=json',
               '--report-path=' + str(report)]
    if args.history:
        command.append('--log-opts=--all')
    result = subprocess.run(command, cwd=root, capture_output=True, text=True)
    if result.returncode not in (0, 1) or not report.exists():
        raise SystemExit('Secret scanner failed. Check its installation and permissions.')
    findings = json.loads(report.read_text())
    failures = []
    for finding in findings:
        name = finding['File'].replace('\\', '/')
        path = Path(name)
        if path.is_absolute():
            name = path.resolve().relative_to(scan_root).as_posix()
        allowed = fixtures.get(name)
        commit = finding.get('Commit')
        if commit:
            data = subprocess.check_output(['git', '-C', str(root), 'show', commit + ':' + name])
        else:
            data = (root / name).read_bytes()
        exact = [finding['RuleID'], finding['StartLine']]
        if not allowed or hashlib.sha256(data).hexdigest() != allowed['sha256'] or exact not in allowed['findings']:
            failures.append((name, finding['StartLine'], finding['RuleID']))
    for name, line, rule in failures:
        print(f'REVIEW {name}:{line} ({rule}); matched value withheld')
    if failures:
        raise SystemExit(1)
    print(f'PASS: no new secret findings; {len(findings)} unchanged upstream vector/constant findings verified.')
