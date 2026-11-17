"""Package only the reviewed public media files, never a whole working folder."""
import hashlib
import json
from pathlib import Path
import struct
import zipfile

ROOT = Path(__file__).resolve().parents[1]
KIT = ROOT / "site" / "press-kit"
ORIGIN = "https://slithy.io/press-kit/"
UPDATED = "2026-11-17"
FILES = {
    "logos/slithy-tove-logo.svg": "Slithy Tove profile logo. Vector, with transparent corners.",
    "logos/slithy-tove-logo-256.png": "Slithy Tove profile logo, 256 pixels square.",
    "logos/slithy-tove-logo-512.png": "Slithy Tove profile logo, 512 pixels square.",
    "logos/slithy-tove-logo-1024.png": "Slithy Tove profile logo, 1024 pixels square.",
    "logos/slithy-tove-mark-dark.svg": "Dark green Slithy Tove mark. Transparent vector.",
    "logos/slithy-tove-mark-dark-1024.png": "Dark green Slithy Tove mark. Transparent PNG.",
    "logos/slithy-tove-mark-cream.svg": "Cream Slithy Tove mark. Transparent vector.",
    "logos/slithy-tove-mark-cream-1024.png": "Cream Slithy Tove mark. Transparent PNG.",
    "screenshots/slithy-tove-windows-home.png": "Slithy Tove Windows beta 0.1.51: wallet and CPU miner. Actual app UI with sample data, not live balances or mining results.",
    "screenshots/slithy-tove-windows-mining-settings.png": "Slithy Tove Windows beta 0.1.51: CPU thread settings. Actual app UI with sample data.",
    "screenshots/slithy-tove-linux-home.png": "Slithy Tove Linux beta 0.1.51-beta.1: wallet and CPU miner in preview mode. Actual app UI rendered in a headless test session, not a Linux desktop capture. Sample data, not live balances or mining results.",
    "screenshots/slithy-tove-linux-mining-settings.png": "Slithy Tove Linux beta 0.1.51-beta.1: CPU thread settings in preview mode. Actual app UI rendered in a headless test session, not a Linux desktop capture.",
    "social/slithy-tove-beta-card.png": "Slithy Tove public beta sharing image. Test coins have no market value.",
    "slithy-tove-facts.txt": "Project descriptions, fact sheet, official links and contact.",
    "README.txt": "File notes, credits, suggested captions and beta notice.",
}


def digest(content):
    return hashlib.sha256(content).hexdigest()


def main():
    entries = []
    for name, caption in FILES.items():
        file = KIT / name
        if not file.is_file() or file.is_symlink():
            raise ValueError(f"Missing or unsafe media file: {name}")
        data = file.read_bytes()
        entry = {"file": name, "url": ORIGIN + name, "bytes": len(data),
                 "sha256": digest(data), "caption": caption}
        if name.endswith(".png"):
            if data[:8] != b"\x89PNG\r\n\x1a\n":
                raise ValueError(f"Invalid PNG: {name}")
            entry["width"], entry["height"] = struct.unpack(">II", data[16:24])
            # Screenshots and exports should have no embedded comments or private metadata.
            offset = 8
            while offset < len(data):
                length = struct.unpack(">I", data[offset:offset + 4])[0]
                kind = data[offset + 4:offset + 8]
                if kind in (b"tEXt", b"zTXt", b"iTXt", b"eXIf"):
                    raise ValueError(f"Review embedded PNG metadata before publishing: {name}")
                offset += 12 + length
        entries.append(entry)
    manifest = {"project": "Slithy Tove", "updated": UPDATED, "pressKit": ORIGIN,
                "contact": "support@slithy.io", "credit": "Slithy Tove / CS Idea Labs LLC",
                "screenshots": "Actual beta app interfaces with sample data. Not live balances or mining benchmarks.",
                "assets": entries}
    (KIT / "assets.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    archive_files = sorted([*FILES, "assets.json"])
    checksums = "".join(f"{digest((KIT / name).read_bytes())}  {name}\n" for name in archive_files)
    (KIT / "SHA256SUMS.txt").write_text(checksums, encoding="utf-8")
    archive_files.append("SHA256SUMS.txt")
    archive = KIT / "slithy-tove-press-kit.zip"
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as bundle:
        for name in archive_files:
            info = zipfile.ZipInfo("slithy-tove-press-kit/" + name, (2026, 11, 17, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            bundle.writestr(info, (KIT / name).read_bytes())
    with zipfile.ZipFile(archive) as bundle:
        assert bundle.testzip() is None
        assert set(bundle.namelist()) == {"slithy-tove-press-kit/" + name for name in archive_files}
    print(json.dumps({"assets": len(entries), "archiveFiles": len(archive_files),
                      "archiveBytes": archive.stat().st_size, "sha256": digest(archive.read_bytes())}))


if __name__ == "__main__":
    main()
