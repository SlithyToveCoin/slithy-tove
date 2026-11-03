"""Describe the packaged program files used by the graphical per-user installer."""
import hashlib
import json
from pathlib import Path
import sys

root = Path(sys.argv[1]).resolve(strict=True)
assert root.is_dir() and (root / "slithy-desktop.dll").is_file()
inventory = root / "install-files.json"
assert not inventory.exists(), "Generate the install inventory once, after packaging."
executables = {"slithy-desktop", "slithy-install", "slithy-uninstall", "Binaries/slithyd", "install-desktop.sh", "createdump"}
files = []
for path in sorted(root.rglob("*")):
    assert not path.is_symlink(), path
    if path.is_file():
        name = path.relative_to(root).as_posix()
        files.append({"Path": name, "Sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "Executable": name in executables})
assert len(files) <= 4096
inventory.write_text(json.dumps(files, indent=2) + "\n")
print(f"Prepared graphical installer inventory for {len(files)} program files.")
