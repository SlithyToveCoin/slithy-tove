#!/usr/bin/env bash
set -euo pipefail
umask 077

INSTALL_DIR="${SLITHY_INSTALL_DIR:-/opt/slithy}"
BIN_DIR="$INSTALL_DIR/bin"
SERVICE_NAME="${SLITHY_SERVICE_NAME:-slithy-node}"
CHAIN="${SLITHY_CHAIN:-testnet}"
P2P_PORT="${SLITHY_P2P_PORT:-53424}"
RPC_PORT="${SLITHY_RPC_PORT:-53426}"
RPC_USER="${SLITHY_RPC_USER:-slithy}"
SOURCE_DIR="${1:-}"
CONFIG_DIR="/etc/slithy/beta-20260909"
CONFIG_FILE="$CONFIG_DIR/menu.conf"
NODE_CONFIG_FILE="$CONFIG_DIR/slithy-node.conf"
VERSION_FILE="$INSTALL_DIR/VERSION"

usage() {
  cat <<USAGE
Slithy Linux installer

Usage:
  sudo ./slithy-install.sh /path/to/extracted/slithy-linux-x64

Environment:
  SLITHY_INSTALL_DIR    default /opt/slithy
  SLITHY_SERVICE_NAME   default slithy-node
  SLITHY_CHAIN          default testnet
  SLITHY_P2P_PORT       default 53424
  SLITHY_RPC_PORT       default 53426
  SLITHY_RPC_USER       default slithy
  SLITHY_RPC_PASSWORD   optional, generated if not provided
USAGE
}

if [[ "$#" -ne 1 || "$SOURCE_DIR" == "-h" || "$SOURCE_DIR" == "--help" ]]; then
  usage
  exit 1
fi

if [[ "$EUID" -ne 0 ]]; then
  echo "Run this installer with sudo."
  exit 1
fi

if [[ ! -d "$SOURCE_DIR" ]]; then
  echo "Source directory does not exist: $SOURCE_DIR"
  exit 1
fi

# These values enter systemd and node configuration files. Reject characters
# that those formats would treat as another directive or argument.
[[ "$INSTALL_DIR" =~ ^/[A-Za-z0-9_/-]+$ && ! -L "$INSTALL_DIR" ]] || { echo "Use an absolute installation path without spaces or symlinks."; exit 1; }
INSTALL_DIR="$(realpath -m "$INSTALL_DIR")"
case "$INSTALL_DIR" in /|/usr|/usr/local|/opt|/home|/var|/etc|/bin|/sbin) echo "Choose an application subdirectory."; exit 1;; esac
BIN_DIR="$INSTALL_DIR/bin"
VERSION_FILE="$INSTALL_DIR/VERSION"
[[ "$SERVICE_NAME" =~ ^[A-Za-z0-9_-]+$ && "$RPC_USER" =~ ^[A-Za-z0-9_-]+$ ]] || exit 1
[[ "$P2P_PORT" =~ ^[0-9]{1,5}$ && "$RPC_PORT" =~ ^[0-9]{1,5}$ ]] || exit 1
(( 10#$P2P_PORT > 0 && 10#$P2P_PORT <= 65535 && 10#$RPC_PORT > 0 && 10#$RPC_PORT <= 65535 && 10#$P2P_PORT != 10#$RPC_PORT )) || exit 1
case "$CHAIN" in test|testnet) ;; *) echo "This release installer is for the test network. Live-network installation is not enabled yet."; exit 1;; esac

find_source_bin() {
  if [[ -f "$SOURCE_DIR/bin/slithyd" ]]; then
    echo "$SOURCE_DIR/bin"
    return
  fi

  if [[ -f "$SOURCE_DIR/slithyd" ]]; then
    echo "$SOURCE_DIR"
    return
  fi

  local found
  found="$(find "$SOURCE_DIR" -type f -name slithyd -print -quit)"
  if [[ -n "$found" ]]; then
    dirname "$found"
    return
  fi

  echo ""
}

SOURCE_BIN="$(find_source_bin)"
if [[ -z "$SOURCE_BIN" ]]; then
  echo "Could not find slithyd under $SOURCE_DIR"
  exit 1
fi

for binary in slithyd slithy-cli slithy-wallet slithy-tx slithy slithy-node slithy-update.sh slithy-common.sh slithy-seed-tool slithy-terms.txt; do
  if [[ ! -f "$SOURCE_BIN/$binary" ]]; then
    echo "Missing $binary in $SOURCE_BIN"
    exit 1
  fi
done

# Existing installations must use the signed updater. Reinstalling used to
# replace RPC credentials underneath a running node.
if [[ -e "$NODE_CONFIG_FILE" || -e "$VERSION_FILE" || -e "/etc/systemd/system/$SERVICE_NAME.service" ]]; then
  echo "Slithy is already installed. Use the updater for the same network, or the beta migration guide for a reset. No settings were changed."
  exit 1
fi

apt-get update
apt-get install -y ca-certificates curl python3 openssl ufw

if ! getent group slithy >/dev/null 2>&1; then
  groupadd --system slithy
fi

if ! id slithy >/dev/null 2>&1; then
  useradd --system --create-home --home-dir /var/lib/slithy --shell /usr/sbin/nologin --gid slithy slithy
fi

if [[ -z "${SLITHY_RPC_PASSWORD+x}" ]]; then
  RPC_PASSWORD="$(openssl rand -base64 32 | tr -d '\n')"
else
  RPC_PASSWORD="$SLITHY_RPC_PASSWORD"
fi
[[ -n "$RPC_PASSWORD" && "$RPC_PASSWORD" != *$'\n'* && "$RPC_PASSWORD" != *$'\r'* && "$RPC_PASSWORD" != *'#'* ]] || { echo "RPC password must be nonempty and contain no line breaks or comment delimiters."; exit 1; }

install -d -o root -g root -m 0755 "$INSTALL_DIR" "$BIN_DIR"
install -d -o slithy -g slithy /var/lib/slithy /var/lib/slithy/wallets /var/log/slithy
install -d -o root -g slithy -m 0750 "$CONFIG_DIR"

install -m 0755 "$SOURCE_BIN/slithyd" "$BIN_DIR/slithyd"
install -m 0755 "$SOURCE_BIN/slithy-cli" "$BIN_DIR/slithy-cli"
install -m 0755 "$SOURCE_BIN/slithy-wallet" "$BIN_DIR/slithy-wallet"
install -m 0755 "$SOURCE_BIN/slithy-tx" "$BIN_DIR/slithy-tx"
install -m 0755 "$SOURCE_BIN/slithy" "$BIN_DIR/slithy"
install -m 0644 "$SOURCE_BIN/slithy-terms.txt" "$BIN_DIR/slithy-terms.txt"
install -m 0755 "$SOURCE_BIN/slithy-node" "$BIN_DIR/slithy-node"
install -m 0755 "$SOURCE_BIN/slithy-update.sh" "$BIN_DIR/slithy-update.sh"
install -m 0755 "$SOURCE_BIN/slithy-common.sh" "$BIN_DIR/slithy-common.sh"
install -m 0755 "$SOURCE_BIN/slithy-seed-tool" "$BIN_DIR/slithy-seed-tool"
if [[ -d "$SOURCE_BIN/licenses" ]]; then
  install -d -o root -g root -m 0755 "$BIN_DIR/licenses"
  find "$SOURCE_BIN/licenses" -maxdepth 1 -type f -exec install -o root -g root -m 0644 -t "$BIN_DIR/licenses" -- {} +
fi
if [[ -f "$SOURCE_BIN/slithy-network.json" ]]; then
  install -m 0644 "$SOURCE_BIN/slithy-network.json" "$BIN_DIR/slithy-network.json"
fi

if [[ -f "$SOURCE_DIR/VERSION" ]]; then
  install -m 0644 "$SOURCE_DIR/VERSION" "$VERSION_FILE"
elif [[ -f "$(dirname "$SOURCE_BIN")/VERSION" ]]; then
  install -m 0644 "$(dirname "$SOURCE_BIN")/VERSION" "$VERSION_FILE"
else
  printf 'unknown\n' > "$VERSION_FILE"
fi

ln -sf "$BIN_DIR/slithy" /usr/local/bin/slithy
ln -sf "$BIN_DIR/slithy-node" /usr/local/bin/slithy-node

if [[ "$CHAIN" == "main" || "$CHAIN" == "mainnet" ]]; then
  CHAIN_ARG=""
  DATA_DIR="/var/lib/slithy/main"
else
  CHAIN_ARG="-testnet"
  DATA_DIR="/var/lib/slithy/beta-20260909"
fi

install -d -o slithy -g slithy "$DATA_DIR" "$DATA_DIR/wallets"

cat > "$NODE_CONFIG_FILE" <<CFG
rpcuser=$RPC_USER
rpcpassword=$RPC_PASSWORD
CFG

chown root:slithy "$NODE_CONFIG_FILE"
chmod 0640 "$NODE_CONFIG_FILE"

cat > "/etc/systemd/system/$SERVICE_NAME.service" <<SERVICE
[Unit]
Description=Slithy Tove node
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=slithy
Group=slithy
ExecStart=$BIN_DIR/slithyd \\
  -conf=$NODE_CONFIG_FILE \\
  $CHAIN_ARG \\
  -datadir=$DATA_DIR \\
  -walletdir=$DATA_DIR/wallets \\
  -server=1 \\
  -listen=1 \\
  -bind=0.0.0.0 \\
  -port=$P2P_PORT \\
  -addnode=borogove.slithy.io:53424 \\
  -addnode=mome.slithy.io:53424 \\
  -addnode=rath.slithy.io:53424 \\
  -rpcbind=127.0.0.1 \\
  -rpcallowip=127.0.0.1 \\
  -rpcport=$RPC_PORT \\
  -fallbackfee=0.0001 \\
  -debuglogfile=/var/log/slithy/slithyd.log
Restart=always
RestartSec=10
TimeoutStopSec=60

[Install]
WantedBy=multi-user.target
SERVICE

cat > "$CONFIG_FILE" <<CFG
CHAIN="$CHAIN"
RPC_CONNECT="127.0.0.1"
RPC_PORT="$RPC_PORT"
RPC_USER="$RPC_USER"
RPC_PASSWORD="$RPC_PASSWORD"
WALLET_DIR="$DATA_DIR/wallets"
USE_SUDO="auto"
SERVICE_NAME="$SERVICE_NAME"
LOG_FILE="/var/log/slithy/slithyd.log"
CFG

chown root:slithy "$CONFIG_FILE"
chmod 0640 "$CONFIG_FILE"

if [[ -n "${SUDO_USER:-}" && "$SUDO_USER" != "root" ]]; then
  usermod -aG slithy "$SUDO_USER" || true
fi

echo "Firewall rules were not changed. If you want inbound peers, allow TCP $P2P_PORT. RPC is bound to loopback."

systemctl daemon-reload
systemctl enable "$SERVICE_NAME"

echo "Installed Slithy to $BIN_DIR"
echo "Menu command: slithy"
echo "Node console: slithy-node"
echo "Start node: sudo systemctl start $SERVICE_NAME"
echo "Check node: slithy doctor"
echo "RPC settings were saved to $CONFIG_FILE"
if [[ -n "${SUDO_USER:-}" && "$SUDO_USER" != "root" ]]; then
  echo "Log out and back in if the slithy command cannot read $CONFIG_FILE yet."
fi
