#!/usr/bin/env bash
set -euo pipefail
umask 022

if [[ "$#" -ne 2 ]]; then
  echo "Usage: bash tools/build-linux-gui.sh /path/to/slithyd /new/output/folder"
  exit 1
fi

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
native="$(realpath "$1")"
output="$(realpath -m "$2")"
[[ -f "$native" ]] || { echo "The native daemon was not found."; exit 1; }
[[ ! -e "$output" ]] || { echo "Choose a new output folder."; exit 1; }

dotnet publish "$root/Desktop/Slithy.Linux/Slithy.Linux.csproj" \
  -c Release -r linux-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o "$output"
install -d "$output/Binaries" "$output/Assets"
install -m 0755 "$native" "$output/Binaries/slithyd"
install -m 0755 "$root/Desktop/Slithy.Linux/install-desktop.sh" "$output/install-desktop.sh"
install -m 0644 "$root/Desktop/Slithy Tove/Slithy Tove/Assets/slithy-mark.png" "$output/Assets/slithy-mark.png"
install -m 0644 "$root/Desktop/Slithy Tove/Slithy Tove/Assets/slithy-icon-256.png" "$output/Assets/slithy-icon-256.png"
install -m 0644 "$root/docs/LINUX-GUI-PREVIEW.md" "$output/README.md"
chmod 0755 "$output/slithy-desktop"
printf 'Linux GUI package folder: %s\n' "$output"
printf 'Test this folder before making a release archive.\n'
