#!/usr/bin/env bash
# Unified build (+ optional install) for Dressmaker Critic Diagnostics.
#
# Usage:
#   ./install.sh                         # build + install (auto-find game)
#   ./install.sh /path/to/Dressmaker     # build + install into that folder
#   ./install.sh --build-only /path/...  # compile only; still needs game refs
#   DRESSMAKER_GAME_ROOT=/path ./install.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
PLUGIN_DIR="$ROOT/DressmakerCritic-Diagnostics"
BUILD_ONLY=0
GAME_ARG=""

usage() {
  cat <<'EOF'
Usage:
  ./install.sh                         # build + install (auto-find game)
  ./install.sh /path/to/Dressmaker     # build + install into that folder
  ./install.sh --build-only [/path]    # compile only; still needs game refs
  DRESSMAKER_GAME_ROOT=/path ./install.sh
EOF
  exit 2
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -h|--help) usage ;;
    --build-only) BUILD_ONLY=1; shift ;;
    --) shift; break ;;
    -*)
      echo "Unknown option: $1" >&2
      usage
      ;;
    *)
      GAME_ARG="$1"
      shift
      break
      ;;
  esac
done
if [[ $# -gt 0 ]]; then
  echo "Unexpected extra arguments: $*" >&2
  usage
fi

resolve_game_dir() {
  if [[ -n "${1:-}" ]]; then
    realpath "$1"
    return
  fi

  local candidates=(
    "${DRESSMAKER_GAME_ROOT:-}"
    "$HOME/.local/share/Steam/steamapps/common/Dressmaker"
    "$HOME/.steam/steam/steamapps/common/Dressmaker"
    "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Dressmaker"
  )

  local path
  for path in "${candidates[@]}"; do
    [[ -z "$path" ]] && continue
    if [[ -f "$path/Dressmaker.exe" ]]; then
      realpath "$path"
      return
    fi
  done

  echo "Could not find Dressmaker. Pass the game folder explicitly:" >&2
  echo "  ./install.sh \"/path/to/steamapps/common/Dressmaker\"" >&2
  echo "Or set DRESSMAKER_GAME_ROOT." >&2
  exit 1
}

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK not installed. On Arch/CachyOS: sudo pacman -S dotnet-sdk" >&2
  exit 1
fi

GAME_DIR="$(resolve_game_dir "$GAME_ARG")"
echo "Game: $GAME_DIR"

chmod +x "$PLUGIN_DIR/build.sh" "$PLUGIN_DIR/build_and_install.sh"

if [[ "$BUILD_ONLY" -eq 1 ]]; then
  bash "$PLUGIN_DIR/build.sh" "$GAME_DIR"
  echo "Built: $PLUGIN_DIR/out/DressmakerCritic.Diagnostics.dll"
  exit 0
fi

exec "$PLUGIN_DIR/build_and_install.sh" "$GAME_DIR"
