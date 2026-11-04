"""Describe the packaged program files used by the graphical per-user installer."""
import hashlib
import json
from pathlib import Path
import sys

def create_inventory(root):
    root = Path(root).resolve(strict=True)
    if not root.is_dir() or not (root / "slithy-desktop.dll").is_file():
        raise ValueError("Choose a complete GUI publish folder.")
    inventory = root / "install-files.json"
    if inventory.exists() or inventory.is_symlink():
        raise ValueError("Generate the install inventory once, after packaging.")
    executables = {"slithy-desktop", "slithy-install", "slithy-uninstall", "Binaries/slithyd", "install-desktop.sh", "createdump"}
    files = []
    total = 0
    for path in sorted(root.rglob("*")):
        if path.is_symlink():
            raise ValueError("A package path is a symbolic link: " + path.relative_to(root).as_posix())
        if path.is_dir():
            continue
        if not path.is_file():
            raise ValueError("The package contains a non-regular file.")
        total += path.stat().st_size
        if len(files) >= 4096 or total > 1024 * 1024 * 1024:
            raise ValueError("The package exceeds the installer's size or file-count limit.")
        digest = hashlib.sha256()
        with path.open("rb") as source:
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(chunk)
        name = path.relative_to(root).as_posix()
        files.append({"Path": name, "Sha256": digest.hexdigest(), "Executable": name in executables})
    # Exclusive creation also refuses a dangling link or a second build writing
    # the same inventory. Validation must still run when Python uses -O.
    with inventory.open("x", encoding="utf-8") as output:
        output.write(json.dumps(files, indent=2) + "\n")
    print(f"Prepared graphical installer inventory for {len(files)} program files.")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: python3 tools/linux-gui-install-inventory.py /path/to/publish-folder")
    try:
        create_inventory(sys.argv[1])
    except (OSError, ValueError) as error:
        raise SystemExit(str(error))
