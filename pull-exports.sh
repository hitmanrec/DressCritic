#!/usr/bin/env bash
# Copy Dressmaker Critic capture folders from the game into this repo,
# then remove those folders from the game captures directory so the next
# pull only sees fresh exports.
#
# Usage:
#   ./pull-exports.sh                 # copy NEW captures, clean game folder
#   ./pull-exports.sh --all           # refresh every capture from the game
#   ./pull-exports.sh --commit        # after copy, git-add exports and commit
#   ./pull-exports.sh --keep-source   # do not delete game capture folders
#   ./pull-exports.sh /path/to/Dressmaker
#   DRESSMAKER_GAME_ROOT=/path ./pull-exports.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
EXPORTS_DIR="$ROOT/DressmakerCritic-Diagnostics/exports"
DEST_CAPTURES="$EXPORTS_DIR/captures"
MODE="new"          # new | all
DO_COMMIT=0
NO_LOG=0
KEEP_SOURCE=0
GAME_ARG=""

usage() {
  cat <<'EOF'
Usage:
  ./pull-exports.sh                 # copy NEW captures + LogOutput.log, then
                                    # delete synced folders from the game
  ./pull-exports.sh --all           # overwrite/refresh all captures from game
  ./pull-exports.sh --commit        # git add + commit pulled exports
  ./pull-exports.sh --keep-source   # leave captures in the game folder
  ./pull-exports.sh --no-log        # skip LogOutput.log
  ./pull-exports.sh [/path/to/Dressmaker]
  DRESSMAKER_GAME_ROOT=/path ./pull-exports.sh
EOF
  exit 2
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -h|--help) usage ;;
    --all) MODE="all"; shift ;;
    --commit) DO_COMMIT=1; shift ;;
    --keep-source) KEEP_SOURCE=1; shift ;;
    --no-log) NO_LOG=1; shift ;;
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

  echo "Could not find Dressmaker. Pass the game folder or set DRESSMAKER_GAME_ROOT." >&2
  exit 1
}

GAME_DIR="$(resolve_game_dir "$GAME_ARG")"
SRC_CAPTURES="$GAME_DIR/BepInEx/config/DressmakerCritic/captures"
SRC_LOG="$GAME_DIR/BepInEx/LogOutput.log"

if [[ ! -d "$SRC_CAPTURES" ]]; then
  echo "No captures directory yet: $SRC_CAPTURES" >&2
  echo "Export once in-game (Export diagnostics button), then re-run." >&2
  exit 1
fi

# Safety: never delete outside the expected game captures path.
case "$SRC_CAPTURES" in
  */BepInEx/config/DressmakerCritic/captures) ;;
  *)
    echo "Refusing unexpected captures path: $SRC_CAPTURES" >&2
    exit 1
    ;;
esac

mkdir -p "$DEST_CAPTURES"

copied=0
skipped=0
cleaned=0
declare -a synced_names=()

shopt -s nullglob
for src in "$SRC_CAPTURES"/*/; do
  name="$(basename "$src")"
  dest="$DEST_CAPTURES/$name"

  if [[ "$MODE" == "new" && -d "$dest" ]]; then
    echo "skip    $name (already in repo)"
    skipped=$((skipped + 1))
    synced_names+=("$name")
    continue
  fi

  mkdir -p "$dest"
  cp -a "$src." "$dest/"
  echo "pulled  $name"
  copied=$((copied + 1))
  synced_names+=("$name")
done
shopt -u nullglob

if [[ "$NO_LOG" -eq 0 ]]; then
  if [[ -f "$SRC_LOG" ]]; then
    cp -f "$SRC_LOG" "$EXPORTS_DIR/LogOutput.log"
    echo "pulled  LogOutput.log"
  else
    echo "warn: LogOutput.log not found at $SRC_LOG" >&2
  fi
fi

if [[ "$KEEP_SOURCE" -eq 0 ]]; then
  clean_failed=0
  for name in "${synced_names[@]:-}"; do
    dest="$DEST_CAPTURES/$name"
    src="$SRC_CAPTURES/$name"
    if [[ ! -d "$dest" ]]; then
      echo "warn: skip clean $name (missing in repo after copy)" >&2
      continue
    fi
    if [[ ! -d "$src" ]]; then
      continue
    fi
    # Proton/Wine captures are often not writable; make them removable first.
    if chmod -R u+w -- "$src" 2>/dev/null && rm -rf -- "$src"; then
      echo "cleaned $name (removed from game captures)"
      cleaned=$((cleaned + 1))
    else
      echo "warn: could not remove $src (close the game / check permissions)" >&2
      clean_failed=$((clean_failed + 1))
    fi
  done
  if [[ "$clean_failed" -gt 0 ]]; then
    echo "warn: $clean_failed capture folder(s) left in the game directory" >&2
  fi
else
  echo "keep-source: left game captures untouched"
fi

echo "Done. new/updated=$copied skipped_existing=$skipped cleaned_game=$cleaned mode=$MODE"
echo "Dest: $DEST_CAPTURES"
echo "Game: $SRC_CAPTURES"

if [[ "$DO_COMMIT" -eq 1 ]]; then
  cd "$ROOT"
  if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "Not a git repo; skipped --commit." >&2
    exit 0
  fi
  git add "$EXPORTS_DIR/captures" "$EXPORTS_DIR/LogOutput.log"
  if git diff --cached --quiet; then
    echo "Nothing new to commit."
    exit 0
  fi
  git commit -m "$(cat <<'EOF'
chore: sync in-game diagnostic captures

Pull latest BepInEx DressmakerCritic captures and LogOutput into exports/.
EOF
)"
  git status -sb
fi
