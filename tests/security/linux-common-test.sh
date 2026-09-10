#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
source "$ROOT/site/install/linux/slithy-common.sh"
CONFIG_FILE="$SLITHY_TMP/config"
printf '%s\n' '# slithy-data-v2' 'WALLET_NAME=$(touch /tmp/slithy-should-not-execute)' 'RPC_PORT=12345' > "$CONFIG_FILE"
read_menu_config "$CONFIG_FILE"
[[ "$WALLET_NAME" == '$(touch /tmp/slithy-should-not-execute)' ]]
[[ "$RPC_PORT" == 12345 ]]
if validate_wallet_name "$WALLET_NAME"; then exit 1; fi
validate_wallet_name test42
RPC_PASSWORD='dummy-password'
actual="$(rpc_input 'dummy secret' '{"fixture":true}')"
expected=$'dummy-password\ndummy secret\n{"fixture":true}'
[[ "$actual" == "$expected" ]]
[[ "$(rpc_input)" == dummy-password ]]
RPC_PASSWORD=$'bad\npassword'
if rpc_input >/dev/null; then exit 1; fi
echo 'Linux configuration and secret-input checks passed.'
CHAIN=testnet
call_cli_unchecked() { printf '%s\n' old-genesis; }
if verify_beta_network; then echo 'Old beta was accepted'; exit 1; fi
call_cli_unchecked() { printf '%s\n' "$SLITHY_BETA_GENESIS"; }
verify_beta_network
CHAIN=regtest
verify_beta_network
echo 'Beta identity guard passed.'
