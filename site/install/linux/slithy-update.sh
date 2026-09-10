#!/usr/bin/env bash
set -euo pipefail
umask 077

FEED_URL="${SLITHY_UPDATE_FEED:-https://slithy.io/updates/linux-x64/beta-20260909/stable.json}"
INSTALL_DIR="${SLITHY_INSTALL_DIR:-/opt/slithy}"
SERVICE_NAME="${SLITHY_SERVICE_NAME:-slithy-node}"
TMP_DIR="$(mktemp -d)"
STAGE=""
BACKUP=""
BIN_REPLACED=0
WAS_ACTIVE=0
STOPPED=0
COMMITTED=0

service_is_stopped() {
  local state
  state="$(systemctl show "$SERVICE_NAME" -p ActiveState --value)" || return 1
  # A crashed process can leave systemd in failed rather than inactive.
  # Check process ownership before clearing that recorded failure.
  [[ "$(systemctl show "$SERVICE_NAME" -p MainPID --value)" == 0 ]] || return 1
  [[ "$(systemctl show "$SERVICE_NAME" -p ControlPID --value)" == 0 ]] || return 1
  if [[ "$state" == failed ]]; then
    systemctl reset-failed "$SERVICE_NAME" || return 1
    state="$(systemctl show "$SERVICE_NAME" -p ActiveState --value)" || return 1
  fi
  [[ "$state" == inactive ]]
}

cleanup() {
  local status=$?
  trap - EXIT
  if [[ "$COMMITTED" -eq 0 ]]; then
    if [[ "$BIN_REPLACED" -eq 1 ]]; then
      if [[ "$WAS_ACTIVE" -eq 1 ]]; then
        systemctl stop "$SERVICE_NAME" || true
        if ! service_is_stopped; then
          echo "Cannot confirm shutdown. Files were left in place; recovery files are at $BACKUP and $STAGE." >&2
          rm -rf "$TMP_DIR"
          exit 1
        fi
      fi
      # Preserve the failed candidate for diagnosis; restore the entire old bin.
      if [[ -d "$INSTALL_DIR/bin" ]]; then mv -- "$INSTALL_DIR/bin" "$BACKUP/failed-bin" || status=1; fi
      if [[ -d "$BACKUP/bin" ]]; then mv -- "$BACKUP/bin" "$INSTALL_DIR/bin" || status=1; fi
      if [[ -f "$BACKUP/VERSION" ]]; then
        cp -- "$BACKUP/VERSION" "$INSTALL_DIR/VERSION" || status=1
      else
        rm -f -- "$INSTALL_DIR/VERSION"
      fi
    fi
    if [[ "$STOPPED" -eq 1 && "$WAS_ACTIVE" -eq 1 ]]; then
      systemctl start "$SERVICE_NAME" || { echo "Rollback needs a manual service restart." >&2; status=1; }
    fi
  fi
  if [[ -n "$STAGE" && -d "$STAGE" ]]; then rm -rf -- "$STAGE"; fi
  rm -rf "$TMP_DIR"
  exit "$status"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

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
    exit 1
  fi
}

need curl
need python3
need openssl
need sha256sum
need tar
need flock
need realpath

usage() {
  cat <<USAGE
Usage: slithy-update.sh check|install [--yes] [--install-dir PATH] [--service NAME]

Environment:
  SLITHY_UPDATE_FEED   default: https://slithy.io/updates/linux-x64/beta-20260909/stable.json
  SLITHY_INSTALL_DIR   default: /opt/slithy
  SLITHY_SERVICE_NAME  default: slithy-node
USAGE
}

ACTION="${1:-}"
shift || true
ASSUME_YES=0
while [[ "$#" -gt 0 ]]; do
  case "$1" in
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

if [[ "$ACTION" != "check" && "$ACTION" != "install" ]]; then
  usage
  exit 1
fi

ENVELOPE="$TMP_DIR/stable.json"
PAYLOAD="$TMP_DIR/payload.json"
SIGNATURE="$TMP_DIR/signature.bin"
SIGNATURE_DER="$TMP_DIR/signature.der"

curl --proto '=https' --proto-redir '=https' --connect-timeout 15 --max-time 60 --max-filesize 1048576 -fsSL "$FEED_URL" -o "$ENVELOPE"

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
REQUIRED="$(python3 -c "import json; print(str(json.load(open('$PAYLOAD')).get('required', False)).lower())")"
UPDATE_LEVEL="$(python3 -c "import json; data=json.load(open('$PAYLOAD')); level=data.get('updateLevel') or ('required' if data.get('required', False) else 'normal'); print(level)")"
RESTART_REQUIRED="$(python3 -c "import json; print(str(json.load(open('$PAYLOAD')).get('restartRequired', True)).lower())")"

echo "Slithy update feed verified."
echo "Version: $VERSION"
echo "Update level: $UPDATE_LEVEL"
echo "Required: $REQUIRED"
echo "Restart required: $RESTART_REQUIRED"
echo "Notes: $RELEASE_NOTES"

INSTALLED_VERSION="not installed"
if [[ -f "$INSTALL_DIR/VERSION" ]]; then
  INSTALLED_VERSION="$(tr -d '\r\n' < "$INSTALL_DIR/VERSION")"
elif [[ -f "$INSTALL_DIR/bin/VERSION" ]]; then
  INSTALLED_VERSION="$(tr -d '\r\n' < "$INSTALL_DIR/bin/VERSION")"
fi

echo "Installed version: $INSTALLED_VERSION"
VERSION_ORDER="$(python3 - "$INSTALLED_VERSION" "$VERSION" <<'PY'
import re,sys
def version(value):
    match=re.fullmatch(r'(\d+)\.(\d+)\.(\d+)(?:-testnet)?',value)
    if not match: raise SystemExit('Unrecognized release version; installation stopped.')
    return tuple(map(int,match.groups()))
candidate=version(sys.argv[2])
current=(-1,-1,-1) if sys.argv[1]=='not installed' else version(sys.argv[1])
print(1 if candidate>current else 0 if candidate==current else -1)
PY
)"
if [[ "$VERSION_ORDER" == 0 ]]; then
  echo "Slithy is up to date."
  exit 0
elif [[ "$VERSION_ORDER" == -1 ]]; then
  echo "The signed feed is older than the installed release. Refusing a downgrade."
  exit 1
else
  echo "Update available: $VERSION"
fi

if [[ "$ACTION" == "check" ]]; then
  exit 0
fi

if [[ "$ASSUME_YES" -ne 1 ]]; then
  echo
  read -r -p "Install Slithy $VERSION to $INSTALL_DIR? [y/N] " answer
  case "$answer" in
    y|Y|yes|YES) ;;
    *) echo "Update cancelled."; exit 0 ;;
  esac
fi

PACKAGE="$TMP_DIR/release.tar.gz"
EXTRACTED="$TMP_DIR/extracted"
BACKUP="$INSTALL_DIR.backup.$(date +%Y%m%d-%H%M%S)"

curl --proto '=https' --proto-redir '=https' --connect-timeout 15 --max-time 900 --max-filesize 536870912 -fsSL "$PACKAGE_URL" -o "$PACKAGE"
SHA256_ACTUAL="$(sha256sum "$PACKAGE" | awk '{print tolower($1)}')"
if [[ "$SHA256_ACTUAL" != "$SHA256_EXPECTED" ]]; then
  echo "Package hash did not match signed manifest."
  exit 1
fi

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

SOURCE_DIR="$EXTRACTED"
if [[ ! -f "$SOURCE_DIR/slithyd" && -d "$EXTRACTED/bin" ]]; then
  SOURCE_DIR="$EXTRACTED"
elif [[ ! -f "$SOURCE_DIR/slithyd" ]]; then
  FOUND="$(find "$EXTRACTED" -type f -name slithyd -print -quit)"
  if [[ -z "$FOUND" ]]; then
    echo "Package does not contain slithyd."
    exit 1
  fi
  SOURCE_DIR="$(dirname "$FOUND")"
fi

if [[ "$INSTALL_DIR" != /* || "$INSTALL_DIR" == / || -L "$INSTALL_DIR" ]]; then
  echo "Use an absolute installation directory, not a filesystem root or symlink." >&2
  exit 1
fi
INSTALL_DIR="$(realpath -m "$INSTALL_DIR")"
case "$INSTALL_DIR" in /|/usr|/usr/local|/opt|/home|/var|/etc|/bin|/sbin)
  echo "Refusing to replace a system directory." >&2; exit 1;;
esac
[[ "$SERVICE_NAME" =~ ^[A-Za-z0-9_.@-]+$ ]] || exit 1
if [[ "$EUID" -ne 0 && "$INSTALL_DIR" == /opt/* ]]; then
  echo "Installing to $INSTALL_DIR needs sudo."
  exit 1
fi

install -d -m 0755 "$INSTALL_DIR"
if [[ "$EUID" -eq 0 ]]; then chown root:root "$INSTALL_DIR"; fi
[[ ! -L "$INSTALL_DIR/.update.lock" ]] || exit 1
exec 9>"$INSTALL_DIR/.update.lock"
flock -n 9 || { echo "Another update is running."; exit 1; }
STAGE="$(mktemp -d "$INSTALL_DIR/.update-stage.XXXXXXXX")"
BACKUP="$(mktemp -d "$INSTALL_DIR/.update-backup.XXXXXXXX")"
mkdir "$STAGE/bin"
if [[ -d "$SOURCE_DIR/bin" ]]; then SOURCE_DIR="$SOURCE_DIR/bin"; fi
for binary in slithyd slithy-cli slithy-wallet slithy-tx slithy slithy-node slithy-update.sh slithy-common.sh slithy-seed-tool slithy-terms.txt; do
  [[ -f "$SOURCE_DIR/$binary" ]] || { echo "Package is missing $binary."; exit 1; }
done
cp -R --no-preserve=ownership,mode "$SOURCE_DIR/." "$STAGE/bin/"
# An executable update must not switch the chain beneath an existing service.
if [[ -f "$SOURCE_DIR/slithy-network.json" || -f "$INSTALL_DIR/bin/slithy-network.json" ]]; then
  python3 - "$SOURCE_DIR/slithy-network.json" "$INSTALL_DIR/bin/slithy-network.json" <<'PY'
import json
from pathlib import Path
import sys

try:
    candidate, installed = [json.loads(Path(path).read_text()) for path in sys.argv[1:]]
    fields = ('id', 'chain', 'genesis', 'dataSubdirectory')
    if any(not isinstance(candidate.get(key), str) or not candidate[key] for key in fields):
        raise ValueError('Incomplete network identity')
    if any(candidate[key] != installed.get(key) for key in fields):
        raise ValueError('Different beta network')
except (OSError, ValueError, TypeError, AttributeError):
    print('This update changes the beta network. Use the coordinated migration guide; no installed program files or services were changed.', file=sys.stderr)
    raise SystemExit(1)
PY
fi
find "$STAGE/bin" -type d -exec chmod 0755 {} +
find "$STAGE/bin" -type f -exec chmod 0755 {} +
chmod 0644 "$STAGE/bin/slithy-terms.txt"
if [[ -d "$STAGE/bin/licenses" ]]; then
  find "$STAGE/bin/licenses" -type f -exec chmod 0644 {} +
fi
if [[ "$EUID" -eq 0 ]]; then chown -R root:root "$STAGE/bin"; fi
printf '%s\n' "$VERSION" > "$STAGE/VERSION"
chmod 0644 "$STAGE/VERSION"
if [[ -f "$INSTALL_DIR/VERSION" ]]; then cp -- "$INSTALL_DIR/VERSION" "$BACKUP/VERSION"; fi

# A stopped service stays stopped. A running service must confirm shutdown
# before any executable is replaced.
if command -v systemctl >/dev/null &&
   [[ "$(systemctl show "$SERVICE_NAME" -p LoadState --value 2>/dev/null)" == loaded ]]; then
  CURRENT_STATE="$(systemctl show "$SERVICE_NAME" -p ActiveState --value)"
  if [[ "$CURRENT_STATE" != inactive && "$CURRENT_STATE" != failed ]]; then
    WAS_ACTIVE=1
    STOPPED=1
    systemctl stop "$SERVICE_NAME"
  fi
  service_is_stopped || {
    echo "Service has not stopped. Update cancelled." >&2; exit 1;
  }
fi

if [[ -d "$INSTALL_DIR/bin" ]]; then mv -- "$INSTALL_DIR/bin" "$BACKUP/bin"; fi
BIN_REPLACED=1
mv -- "$STAGE/bin" "$INSTALL_DIR/bin"
mv -f -- "$STAGE/VERSION" "$INSTALL_DIR/VERSION"

if [[ "$WAS_ACTIVE" -eq 1 ]]; then
  systemctl start "$SERVICE_NAME"
  sleep 3
  systemctl is-active --quiet "$SERVICE_NAME" || {
    echo "Updated service failed. Restoring the previous release." >&2
    systemctl stop "$SERVICE_NAME" || true
    exit 1
  }
fi
COMMITTED=1
echo "Slithy Linux tools updated to $VERSION."
echo "Previous program files: $BACKUP"
