#!/usr/bin/env bash
set -euo pipefail

# Register this extracted GUI folder in the current user's app menu.
# No system service or existing wallet is replaced.
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
if [[ "$root" == *$'\n'* || "$root" == *'"'* || "$root" == *'`'* || "$root" == *'$'* || "$root" == *'\'* || "$root" == *'%'* ]]; then
  echo 'Extract Slithy to a directory without shell metacharacters.' >&2
  exit 1
fi
if [[ ! -f "$root/slithy-desktop" || ! -f "$root/Binaries/slithyd" || ! -f "$root/Assets/slithy-icon-256.png" ]]; then
  echo 'Extract the complete GUI archive before running this script.' >&2
  exit 1
fi
chmod u+x "$root/slithy-desktop" "$root/Binaries/slithyd"
data_home="${XDG_DATA_HOME:-$HOME/.local/share}"
[[ "$data_home" == /* ]] || data_home="$HOME/.local/share"
applications="$data_home/applications"
icons="$data_home/icons/hicolor/256x256/apps"
mkdir -p "$applications" "$icons"
target="$applications/io.slithy.Tove.desktop"
icon="$icons/io.slithy.Tove.png"
if [[ -L "$target" || -L "$icon" ]]; then
  echo 'Refusing to overwrite a symbolic link.' >&2
  exit 1
fi
temporary="$(mktemp "$applications/.slithy-launcher.XXXXXX")"
temporary_icon="$(mktemp "$icons/.slithy-icon.XXXXXX")"
trap 'rm -f -- "$temporary" "$temporary_icon"' EXIT
install -m 0644 "$root/Assets/slithy-icon-256.png" "$temporary_icon"
printf '%s\n' '[Desktop Entry]' 'Type=Application' 'Name=Slithy Tove' \
  'GenericName=Wallet and CPU miner' \
  'Comment=Linux wallet and CPU miner for the Slithy test network' \
  "Exec=\"$root/slithy-desktop\"" 'Icon=io.slithy.Tove' \
  'StartupWMClass=io.slithy.Tove' 'StartupNotify=true' \
  'Terminal=false' 'Categories=Office;Finance;' > "$temporary"
chmod 644 "$temporary"
mv -f -- "$temporary_icon" "$icon"
mv -f -- "$temporary" "$target"

# Remove the shortcut made by earlier GUI packages, not a custom launcher.
legacy="$applications/slithy-tove-gui.desktop"
if [[ -f "$legacy" && ! -L "$legacy" ]] &&
   grep -Fxq 'Name=Slithy Tove GUI Beta' "$legacy" &&
   grep -Fxq 'Comment=Linux wallet and CPU miner for the Slithy test network' "$legacy" &&
   grep -Eq '^Exec="/.*/slithy-desktop"$' "$legacy"; then
  rm -- "$legacy"
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache -q -t "$data_home/icons/hicolor" >/dev/null 2>&1 || true
fi
if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$applications" >/dev/null 2>&1 || true
fi
echo 'Added Slithy Tove to your app menu. Leave this extracted folder in place.'
