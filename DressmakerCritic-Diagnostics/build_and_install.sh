#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo 'Usage: ./build_and_install.sh "/path/to/steamapps/common/Dressmaker"'
  exit 2
fi

ROOT="$(cd "$(dirname "$0")" && pwd)"
GAME_DIR="$(realpath "$1")"
if [[ ! -f "$GAME_DIR/Dressmaker.exe" ]]; then
  echo "Dressmaker.exe not found in: $GAME_DIR"
  exit 1
fi
if [[ ! -f "$GAME_DIR/BepInEx/plugins/DressmakerMods.dll" ]]; then
  echo "DressmakerMods.dll not found. Install Yuuma's Dressmaker ModLoader first."
  exit 1
fi
if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK not installed. On Arch/CachyOS: sudo pacman -S dotnet-sdk"
  exit 1
fi

dotnet build "$ROOT/DressmakerCritic.Diagnostics.csproj" -c Release \
  "-p:DressmakerGameRoot=$GAME_DIR" -o "$ROOT/out"

DEST="$GAME_DIR/BepInEx/plugins/DressmakerCritic"
mkdir -p "$DEST"
cp -f "$ROOT/out/DressmakerCritic.Diagnostics.dll" "$DEST/"
echo "Installed: $DEST/DressmakerCritic.Diagnostics.dll"
echo 'Restart Dressmaker, open console (Ctrl+`), and run: critic-status / critic-export'
