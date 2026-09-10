#!/usr/bin/env bash
set -euo pipefail
umask 022

if [[ "$#" -lt 2 || "$#" -gt 3 ]]; then
  echo "Usage: ./tools/build-linux-release-tarball.sh /path/to/slithy/bin VERSION [output-dir]"
  exit 1
fi

SOURCE_BIN="$1"
SEED_TOOL="${SLITHY_SEED_TOOL:?Build the current recovery helper and set SLITHY_SEED_TOOL to its executable path.}"
[[ -f "$SEED_TOOL" ]] || { echo "Recovery helper was not found: $SEED_TOOL"; exit 1; }
VERSION="$2"
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-testnet)?$ ]] || { echo "Invalid release version."; exit 1; }
OUTPUT_DIR="${3:-review/release/linux}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PACKAGE_NAME="slithy-linux-x64-$VERSION"
WORK_DIR="$OUTPUT_DIR/$PACKAGE_NAME"
PACKAGE_PATH="$OUTPUT_DIR/$PACKAGE_NAME.tar.gz"

if [[ ! -d "$SOURCE_BIN" ]]; then
  echo "Source binary directory does not exist: $SOURCE_BIN"
  exit 1
fi

for binary in slithyd slithy-cli slithy-wallet slithy-tx; do
  if [[ ! -f "$SOURCE_BIN/$binary" ]]; then
    echo "Missing binary: $SOURCE_BIN/$binary"
    exit 1
  fi
done

if [[ -e "$WORK_DIR" || -e "$PACKAGE_PATH" ]]; then
  echo "Release output already exists. Choose a fresh output directory."
  exit 1
fi
mkdir -p "$WORK_DIR/bin" "$WORK_DIR/install/linux"
printf '%s\n' "$VERSION" > "$WORK_DIR/VERSION"
install -m 0644 "$ROOT_DIR/licenses/NATIVE-NOTICES.txt" "$WORK_DIR/NATIVE-NOTICES.txt"
install -m 0644 "$ROOT_DIR/LICENSE" "$WORK_DIR/LICENSE"
install -m 0644 "$ROOT_DIR/NOTICE" "$WORK_DIR/NOTICE"
mkdir -p "$WORK_DIR/licenses"
install -m 0644 "$ROOT_DIR/licenses/NBitcoin-LICENSE.txt" "$ROOT_DIR/licenses/DOTNET-NOTICES.md" "$WORK_DIR/licenses/"
mkdir -p "$WORK_DIR/bin/licenses"
install -m 0644 "$ROOT_DIR/LICENSE" "$ROOT_DIR/NOTICE" "$ROOT_DIR/licenses/"*.txt "$ROOT_DIR/licenses/"*.md "$WORK_DIR/bin/licenses/"

install -m 0755 "$SOURCE_BIN/slithyd" "$WORK_DIR/bin/slithyd"
install -m 0755 "$SOURCE_BIN/slithy-cli" "$WORK_DIR/bin/slithy-cli"
install -m 0755 "$SOURCE_BIN/slithy-wallet" "$WORK_DIR/bin/slithy-wallet"
install -m 0755 "$SOURCE_BIN/slithy-tx" "$WORK_DIR/bin/slithy-tx"
install -m 0755 "$ROOT_DIR/site/install/linux/slithy" "$WORK_DIR/bin/slithy"
install -m 0644 "$ROOT_DIR/licenses/BETA-TERMS.txt" "$WORK_DIR/bin/slithy-terms.txt"
install -m 0755 "$ROOT_DIR/site/install/linux/slithy-node" "$WORK_DIR/bin/slithy-node"
install -m 0755 "$ROOT_DIR/site/install/linux/slithy-update.sh" "$WORK_DIR/bin/slithy-update.sh"
install -m 0755 "$ROOT_DIR/site/install/linux/slithy-common.sh" "$WORK_DIR/bin/slithy-common.sh"
install -m 0644 "$ROOT_DIR/site/install/linux/slithy-network.json" "$WORK_DIR/bin/slithy-network.json"
install -m 0755 "$SEED_TOOL" "$WORK_DIR/bin/slithy-seed-tool"
install -m 0755 "$ROOT_DIR/site/install/linux/slithy-install.sh" "$WORK_DIR/install/linux/slithy-install.sh"
install -m 0755 "$ROOT_DIR/site/install/linux/slithy-install-local.sh" "$WORK_DIR/install/linux/slithy-install-local.sh"
install -m 0755 "$ROOT_DIR/site/install/linux/slithy-update.sh" "$WORK_DIR/install/linux/slithy-update.sh"

cat > "$WORK_DIR/README.txt" <<README
Slithy Tove Linux x64 $VERSION

Files:
  bin/slithyd
  bin/slithy-cli
  bin/slithy-wallet
  bin/slithy-tx
  bin/slithy
  bin/slithy-node
  bin/slithy-update.sh
  bin/slithy-seed-tool

Quick test:
  bin/slithy version
  bin/slithy settings

Install:
  sudo install/linux/slithy-install-local.sh .

Web install:
  curl -fsSL https://slithy.io/install/linux/slithy-install.sh -o slithy-install.sh
  chmod +x slithy-install.sh
  sudo ./slithy-install.sh

After install:
  slithy
  slithy-node
  slithy doctor

This package uses beta-20260909. It does not join the previous beta chain.
Test coins have no live-network value. Create a new beta wallet after migration.
For an existing installation, follow the coordinated beta migration guide.
The web installer may still offer an earlier public version during preparation.
README

(
  cd "$OUTPUT_DIR"
  tar --owner=0 --group=0 --numeric-owner --mode='u=rwX,go=rX' -czf "$PACKAGE_NAME.tar.gz" "$PACKAGE_NAME"
  sha256sum "$PACKAGE_NAME.tar.gz" > "$PACKAGE_NAME.tar.gz.sha256"
)

echo "$PACKAGE_PATH"
echo "$PACKAGE_PATH.sha256"
