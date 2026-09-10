#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET="${1:-linux}"
JOBS="${JOBS:-2}"
[[ "$TARGET" == linux || "$TARGET" == windows ]] || { echo 'Usage: bash tools/build-core.sh linux|windows'; exit 2; }
[[ "$JOBS" =~ ^[1-9][0-9]*$ ]] || { echo 'JOBS must be a positive integer'; exit 2; }
BUILD="${BUILD_DIR:-$ROOT/build/native/$TARGET}"
options=(-DCMAKE_BUILD_TYPE=Release -DBUILD_GUI=OFF -DBUILD_BENCH=OFF
  -DBUILD_TX=ON -DBUILD_WALLET_TOOL=ON -DBUILD_UTIL=OFF -DBUILD_BITCOIN_BIN=OFF
  -DENABLE_IPC=OFF -DWITH_USDT=OFF -DWITH_ZMQ=OFF)
if [[ "$TARGET" == windows ]]; then
  # The dependency recipes pin downloads and check their hashes.
  # An existing toolchain can be reused without changing the source checkout.
  toolchain="${SLITHY_TOOLCHAIN:-$ROOT/core/depends/x86_64-w64-mingw32/toolchain.cmake}"
  if [[ ! -f "$toolchain" ]]; then
    case "$ROOT" in /mnt/[a-z]/*) echo 'Build Windows dependencies from a checkout in the Linux filesystem, not /mnt/c. See docs/BUILDING.md.'; exit 1 ;; esac
    make -C "$ROOT/core/depends" HOST=x86_64-w64-mingw32 NO_QT=1 NO_ZMQ=1 NO_USDT=1 NO_IPC=1 -j"$JOBS"
  fi
  options+=(-DCMAKE_TOOLCHAIN_FILE="$toolchain" -DBUILD_TESTS=OFF)
else
  options+=(-DBUILD_TESTS=ON)
fi
cmake -S "$ROOT/core" -B "$BUILD" "${options[@]}"
cmake --build "$BUILD" -j"$JOBS"
echo "Native programs: $BUILD/bin"
if [[ "$TARGET" == linux ]]; then
  echo "Run native unit tests: ctest --test-dir '$BUILD' --output-on-failure -j$JOBS"
fi
