#!/usr/bin/env bash
# Shared terminal helpers. Configuration is data, never a shell script.
umask 077
SLITHY_TMP="$(mktemp -d "${TMPDIR:-/tmp}/slithy.XXXXXXXX")"
SLITHY_UNLOCKED_WALLET=""
SLITHY_BETA_GENESIS="000f888cdb70403cd5310d02d7983951ee146ac799485bca337a7b52c24643f2"

verify_beta_network() {
  case "$CHAIN" in
    regtest) return 0 ;;
    test|testnet) ;;
    *) echo "This release is for beta testing, not the live network." >&2; return 1 ;;
  esac
  local genesis
  genesis="$(call_cli_unchecked getblockhash 0)" || return 1
  if [[ "$genesis" != "$SLITHY_BETA_GENESIS" ]]; then
    echo "This node is on a different beta network. Follow the reset migration guide before opening a wallet or mining." >&2
    return 1
  fi
}

call_cli() {
  verify_beta_network || return 1
  call_cli_unchecked "$@"
}

relock_wallet() {
  if [[ -n "$SLITHY_UNLOCKED_WALLET" ]] && declare -F call_wallet_cli >/dev/null; then
    local selected="$WALLET_NAME"
    WALLET_NAME="$SLITHY_UNLOCKED_WALLET"
    if ! call_wallet_cli walletlock >/dev/null 2>&1; then
      echo "Warning: wallet lock could not be confirmed. Check the local node." >&2
      WALLET_NAME="$selected"
      return 1
    fi
    WALLET_NAME="$selected"
    SLITHY_UNLOCKED_WALLET=""
  fi
}

cleanup_terminal() {
  relock_wallet || true
  rm -rf -- "$SLITHY_TMP"
}
trap cleanup_terminal EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

read_menu_config() {
  local file="$1" key value first
  [[ -r "$file" ]] || return 0
  IFS= read -r first < "$file" || true
  while IFS='=' read -r key value; do
    case "$key" in
      CHAIN|RPC_CONNECT|RPC_PORT|RPC_USER|RPC_PASSWORD|WALLET_NAME|USE_SUDO|SERVICE_NAME|LOG_FILE|WALLET_DIR)
        value="${value%$'\r'}"
        # Earlier releases surrounded each value with quotes. Remove those
        # delimiters without evaluating substitutions or backslash escapes.
        if [[ "$first" != '# slithy-data-v2' && "$value" == \"*\" ]]; then
          value="${value:1:${#value}-2}"
        fi
        printf -v "$key" '%s' "$value"
        ;;
    esac
  done < "$file"
}

validate_wallet_name() {
  [[ "$1" =~ ^[A-Za-z0-9][A-Za-z0-9_.-]{0,63}$ ]] || {
    echo "Use 1 to 64 letters, numbers, dots, underscores or hyphens for the wallet name." >&2
    return 1
  }
}

confirm_wallet_password() {
  [[ -n "$1" ]] && return 0
  local answer
  read -r -p "This wallet will be unencrypted. Type unencrypted to accept: " answer
  [[ "$answer" == unencrypted ]]
}

# Core's -stdin reads RPC parameters after the method. -stdinrpcpass reads
# the first line as authentication. Neither secret appears in argv.
rpc_input() {
  local value
  for value in "$RPC_PASSWORD" "$@"; do
    if [[ "$value" == *$'\n'* || "$value" == *$'\r'* ]]; then
      echo "RPC input must not contain a line break." >&2
      return 1
    fi
  done
  printf '%s\n' "$RPC_PASSWORD"
  if [[ "$#" -gt 0 ]]; then printf '%s\n' "$@"; fi
}

require_stopped_service() {
  if ! command -v systemctl >/dev/null ||
     [[ "$(systemctl show "$SERVICE_NAME" -p LoadState --value 2>/dev/null)" != loaded ]]; then
    echo "Cannot verify a managed node shutdown. No wallet files were moved." >&2
    return 1
  fi
  sudo systemctl stop "$SERVICE_NAME" || return 1
  [[ "$(systemctl show "$SERVICE_NAME" -p ActiveState --value)" == inactive ]] || {
    echo "The node has not stopped. No wallet files were moved." >&2
    return 1
  }
}

create_report_file() {
  local reports="${XDG_STATE_HOME:-$HOME/.local/state}/slithy/reports"
  mkdir -p "$reports"
  mktemp "$reports/report-XXXXXXXX.txt"
}
