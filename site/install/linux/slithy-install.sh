#!/usr/bin/env bash
set -euo pipefail
umask 077

FEED_URL="${SLITHY_UPDATE_FEED:-https://slithy.io/updates/linux-x64/beta-20260909/stable.json}"
INSTALL_DIR="${SLITHY_INSTALL_DIR:-/opt/slithy}"
SERVICE_NAME="${SLITHY_SERVICE_NAME:-slithy-node}"
TMP_DIR="$(mktemp -d)"

cleanup() {
  rm -rf "$TMP_DIR"
}
trap cleanup EXIT

PUBLIC_KEY="$TMP_DIR/slithy-update-public.pem"
cat > "$PUBLIC_KEY" <<'KEY'
-----BEGIN PUBLIC KEY-----
MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEuV9LGMpczQyxx7fsY5VKbgxNTgY2
yQ4qRGzsEDZNclMYbtDJzzKRVXkuwpkXB3oIfLq7vCFg4NBlUC0LXcT7KA==
-----END PUBLIC KEY-----
KEY

need() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1"
    if [[ "$1" == "curl" || "$1" == "openssl" || "$1" == "python3" || "$1" == "tar" || "$1" == "sha256sum" ]]; then
      echo "On Ubuntu or Debian, install basics with:"
      echo "  sudo apt-get update && sudo apt-get install -y ca-certificates curl python3 openssl tar coreutils"
    fi
    exit 1
  fi
}

usage() {
  cat <<USAGE
Slithy Linux installer

Usage:
  sudo ./slithy-install.sh [--yes] [--install-dir PATH] [--service NAME]

What it does:
  Downloads the signed Slithy Linux release manifest.
  Verifies the release signature.
  Downloads the Linux package.
  Checks the package SHA256 hash.
  Runs the local package installer.

Environment:
  SLITHY_UPDATE_FEED   default: https://slithy.io/updates/linux-x64/beta-20260909/stable.json
  SLITHY_INSTALL_DIR   default: /opt/slithy
  SLITHY_SERVICE_NAME  default: slithy-node
USAGE
}

ASSUME_YES=0
while [[ "$#" -gt 0 ]]; do
  case "$1" in
    -h|--help)
      usage
      exit 0
      ;;
    --yes)
      ASSUME_YES=1
      shift
      ;;
    --install-dir)
      INSTALL_DIR="${2:?Missing install dir}"
      shift 2
      ;;
    --service)
      SERVICE_NAME="${2:?Missing service name}"
      shift 2
      ;;
    *)
      usage
      exit 1
      ;;
  esac
done

if [[ "$EUID" -ne 0 && "$INSTALL_DIR" == /opt/* ]]; then
  echo "Installing to $INSTALL_DIR needs sudo."
  echo "Run: sudo ./slithy-install.sh"
  exit 1
fi

need curl
need python3
need openssl
need sha256sum
need tar

ENVELOPE="$TMP_DIR/stable.json"
PAYLOAD="$TMP_DIR/payload.json"
SIGNATURE="$TMP_DIR/signature.bin"
SIGNATURE_DER="$TMP_DIR/signature.der"

echo "Fetching Slithy Linux release feed..."
curl --proto "=https" --proto-redir "=https" --connect-timeout 15 --max-time 60 --max-filesize 1048576 -fsSL "$FEED_URL" -o "$ENVELOPE"

python3 - "$ENVELOPE" "$PAYLOAD" "$SIGNATURE" "$SIGNATURE_DER" <<'PY'
import base64
import json
import sys

envelope_path, payload_path, signature_path, signature_der_path = sys.argv[1:5]
envelope = json.load(open(envelope_path, "r", encoding="utf-8"))
payload = base64.b64decode(envelope["payloadBase64"])
signature = base64.b64decode(envelope["signatureBase64"])
open(payload_path, "wb").write(payload)
open(signature_path, "wb").write(signature)
if len(signature) != 64:
    raise SystemExit("Update signature must be 64 bytes.")
r = int.from_bytes(signature[:32], "big")
s = int.from_bytes(signature[32:], "big")

def der_int(value):
    raw = value.to_bytes((value.bit_length() + 7) // 8 or 1, "big")
    if raw[0] & 0x80:
        raw = b"\x00" + raw
    return b"\x02" + bytes([len(raw)]) + raw

body = der_int(r) + der_int(s)
open(signature_der_path, "wb").write(b"\x30" + bytes([len(body)]) + body)
data = json.loads(payload.decode("utf-8"))
for key in ("version", "packageUrl", "sha256", "releaseNotes"):
    if key not in data or not data[key]:
        raise SystemExit(f"Missing manifest field: {key}")
PY

openssl dgst -sha256 -verify "$PUBLIC_KEY" -signature "$SIGNATURE_DER" "$PAYLOAD" >/dev/null

VERSION="$(python3 -c "import json; print(json.load(open('$PAYLOAD'))['version'])")"
PACKAGE_URL="$(python3 -c "import json; print(json.load(open('$PAYLOAD'))['packageUrl'])")"
SHA256_EXPECTED="$(python3 -c "import json; print(json.load(open('$PAYLOAD'))['sha256'].lower())")"
RELEASE_NOTES="$(python3 -c "import json; print(json.load(open('$PAYLOAD')).get('releaseNotes',''))")"

echo "Release feed verified."
echo "Version: $VERSION"
echo "Notes: $RELEASE_NOTES"
echo "Package: $PACKAGE_URL"

if [[ "$ASSUME_YES" -ne 1 ]]; then
  echo
  read -r -p "Install Slithy $VERSION to $INSTALL_DIR? [y/N] " answer
  case "$answer" in
    y|Y|yes|YES) ;;
    *) echo "Install cancelled."; exit 0 ;;
  esac
fi

PACKAGE="$TMP_DIR/release.tar.gz"
EXTRACTED="$TMP_DIR/extracted"

echo "Downloading Slithy Linux package..."
curl --proto "=https" --proto-redir "=https" --connect-timeout 15 --max-time 900 --max-filesize 536870912 -fL --progress-bar "$PACKAGE_URL" -o "$PACKAGE"

SHA256_ACTUAL="$(sha256sum "$PACKAGE" | awk '{print tolower($1)}')"
if [[ "$SHA256_ACTUAL" != "$SHA256_EXPECTED" ]]; then
  echo "Package hash did not match signed manifest."
  echo "Expected: $SHA256_EXPECTED"
  echo "Actual:   $SHA256_ACTUAL"
  exit 1
fi

echo "Package hash verified."
mkdir -p "$EXTRACTED"
python3 - "$PACKAGE" "$EXTRACTED" <<'PY'
import pathlib,shutil,sys,tarfile
root=pathlib.Path(sys.argv[2])
with tarfile.open(sys.argv[1],'r:gz') as archive:
    members=archive.getmembers()
    if len(members)>10000 or sum(m.size for m in members)>1073741824:
        raise SystemExit('Package exceeds extraction limits.')
    seen=set()
    for m in members:
        path=pathlib.PurePosixPath(m.name)
        if path.is_absolute() or '..' in path.parts or '\\' in m.name or not (m.isfile() or m.isdir()) or m.name in seen:
            raise SystemExit('Unsafe package member.')
        seen.add(m.name)
    # Never restore archive ownership, permissions, links or device files.
    for m in members:
        target=root/pathlib.PurePosixPath(m.name)
        if m.isdir(): target.mkdir(parents=True,exist_ok=True)
        else:
            target.parent.mkdir(parents=True,exist_ok=True)
            with archive.extractfile(m) as source, target.open('xb') as dest:
                shutil.copyfileobj(source,dest)
        target.chmod(0o755 if m.isdir() or m.mode & 0o111 else 0o644)
PY

PACKAGE_ROOT="$(find "$EXTRACTED" -mindepth 1 -maxdepth 1 -type d -print -quit)"
if [[ -z "$PACKAGE_ROOT" ]]; then
  echo "Package did not contain a release folder."
  exit 1
fi

LOCAL_INSTALLER="$PACKAGE_ROOT/install/linux/slithy-install-local.sh"
if [[ ! -x "$LOCAL_INSTALLER" ]]; then
  LOCAL_INSTALLER="$PACKAGE_ROOT/install/linux/slithy-install.sh"
fi

if [[ ! -x "$LOCAL_INSTALLER" ]]; then
  echo "Package did not contain a usable Linux installer."
  exit 1
fi

echo "Running local package installer..."
SLITHY_INSTALL_DIR="$INSTALL_DIR" SLITHY_SERVICE_NAME="$SERVICE_NAME" "$LOCAL_INSTALLER" "$PACKAGE_ROOT"

echo
echo "Slithy Linux install finished."
echo "Run: slithy"
echo "Node console: slithy-node"
