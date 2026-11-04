#!/usr/bin/env bash
# Copy Dressmaker Critic exports from the game into this repo, then remove the
# synced files/folders from the game config so the next pull only sees fresh ones.
#
# Syncs:
#   BepInEx/config/DressmakerCritic/captures/<id>/   -> exports/captures/
#   BepInEx/config/DressmakerCritic/reports/*.txt    -> exports/reports/   (v0.5+)
#   BepInEx/LogOutput.log                            -> exports/LogOutput.log
#
# Usage:
#   ./pull-exports.sh                 # copy NEW items, clean game folders
#   ./pull-exports.sh --all           # refresh every item from the game
#   ./pull-exports.sh --commit        # after copy, git-add exports and commit
#   ./pull-exports.sh --keep-source   # do not delete game export files
#   ./pull-exports.sh /path/to/Dressmaker
#   DRESSMAKER_GAME_ROOT=/path ./pull-exports.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
EXPORTS_DIR="$ROOT/DressmakerCritic-Diagnostics/exports"
DEST_CAPTURES="$EXPORTS_DIR/captures"
DEST_REPORTS="$EXPORTS_DIR/reports"
MODE="new"          # new | all
DO_COMMIT=0
NO_LOG=0
KEEP_SOURCE=0
GAME_ARG=""

usage() {
  cat <<'EOF'
Usage:
  ./pull-exports.sh                 # copy NEW captures/reports + log, then
                                    # delete synced items from the game
  ./pull-exports.sh --all           # overwrite/refresh all from game
  ./pull-exports.sh --commit        # git add + commit pulled exports
  ./pull-exports.sh --keep-source   # leave game export folders untouched
  ./pull-exports.sh --no-log        # skip LogOutput.log
  ./pull-exports.sh [/path/to/Dressmaker]
  DRESSMAKER_GAME_ROOT=/path ./pull-exports.sh

Looks under BepInEx/config/DressmakerCritic/ for:
  captures/<id>/   (diagnostic JSON folders)
  reports/*.txt    (v0.5+ inspection text reports)
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

assert_under_critic() {
  case "$1" in
    */BepInEx/config/DressmakerCritic/*) ;;
    *)
      echo "Refusing unexpected path: $1" >&2
      exit 1
      ;;
  esac
}

remove_path() {
  local path="$1"
  local label="$2"
  if [[ ! -e "$path" ]]; then
    return 0
  fi
  if chmod -R u+w -- "$path" 2>/dev/null && rm -rf -- "$path"; then
    echo "cleaned $label"
    return 0
  fi
  echo "warn: could not remove $path (close the game / check permissions)" >&2
  return 1
}

GAME_DIR="$(resolve_game_dir "$GAME_ARG")"
CRITIC_CFG="$GAME_DIR/BepInEx/config/DressmakerCritic"
SRC_CAPTURES="$CRITIC_CFG/captures"
SRC_REPORTS="$CRITIC_CFG/reports"
SRC_LOG="$GAME_DIR/BepInEx/LogOutput.log"

if [[ ! -d "$CRITIC_CFG" ]]; then
  echo "No DressmakerCritic config yet: $CRITIC_CFG" >&2
  echo "Install/run the plugin and export once in-game, then re-run." >&2
  exit 1
fi

has_captures=0
has_reports=0
[[ -d "$SRC_CAPTURES" ]] && has_captures=1
[[ -d "$SRC_REPORTS" ]] && has_reports=1

if [[ "$has_captures" -eq 0 && "$has_reports" -eq 0 ]]; then
  echo "No captures/ or reports/ under $CRITIC_CFG yet." >&2
  echo "Export diagnostics or save an inspection report in-game, then re-run." >&2
  exit 1
fi

mkdir -p "$DEST_CAPTURES" "$DEST_REPORTS"

copied=0
skipped=0
cleaned=0
clean_failed=0
declare -a synced_capture_names=()
declare -a synced_report_names=()

# --- capture folders ---
if [[ "$has_captures" -eq 1 ]]; then
  assert_under_critic "$SRC_CAPTURES/x"
  shopt -s nullglob
  for src in "$SRC_CAPTURES"/*/; do
    name="$(basename "$src")"
    dest="$DEST_CAPTURES/$name"
    in_progress=0
    if [[ -f "$src.capture-in-progress" ]]; then
      in_progress=1
      if [[ "$MODE" == "new" ]]; then
        echo "skip    captures/$name (capture still running; --all copies partial evidence without cleaning it)"
        continue
      fi
    fi

    if [[ "$MODE" == "new" && -d "$dest" && ! -f "$dest/.capture-in-progress" ]]; then
      echo "skip    captures/$name (already in repo)"
      skipped=$((skipped + 1))
      if [[ "$in_progress" -eq 0 ]]; then synced_capture_names+=("$name"); fi
      continue
    fi

    mkdir -p "$dest"
    cp -a "$src." "$dest/"
    if [[ "$in_progress" -eq 0 ]]; then rm -f -- "$dest/.capture-in-progress"; fi
    echo "pulled  captures/$name"
    copied=$((copied + 1))
    if [[ "$in_progress" -eq 0 ]]; then synced_capture_names+=("$name"); fi
  done
  shopt -u nullglob
fi

# --- inspection report text files (v0.5+) ---
if [[ "$has_reports" -eq 1 ]]; then
  assert_under_critic "$SRC_REPORTS/x"
  shopt -s nullglob
  for src in "$SRC_REPORTS"/*; do
    [[ -f "$src" ]] || continue
    name="$(basename "$src")"
    dest="$DEST_REPORTS/$name"

    if [[ "$MODE" == "new" && -f "$dest" ]]; then
      echo "skip    reports/$name (already in repo)"
      skipped=$((skipped + 1))
      synced_report_names+=("$name")
      continue
    fi

    cp -a "$src" "$dest"
    echo "pulled  reports/$name"
    copied=$((copied + 1))
    synced_report_names+=("$name")
  done
  shopt -u nullglob
fi

if [[ "$NO_LOG" -eq 0 ]]; then
  if [[ -f "$SRC_LOG" ]]; then
    cp -f "$SRC_LOG" "$EXPORTS_DIR/LogOutput.log"
    echo "pulled  LogOutput.log"
  else
    echo "warn: LogOutput.log not found at $SRC_LOG" >&2
  fi
fi

if [[ "$KEEP_SOURCE" -eq 0 ]]; then
  for name in "${synced_capture_names[@]}"; do
    [[ -n "$name" ]] || continue
    dest="$DEST_CAPTURES/$name"
    src="$SRC_CAPTURES/$name"
    if [[ ! -d "$dest" ]]; then
      echo "warn: skip clean captures/$name (missing in repo after copy)" >&2
      continue
    fi
    if remove_path "$src" "captures/$name"; then
      cleaned=$((cleaned + 1))
    else
      clean_failed=$((clean_failed + 1))
    fi
  done
  for name in "${synced_report_names[@]}"; do
    [[ -n "$name" ]] || continue
    dest="$DEST_REPORTS/$name"
    src="$SRC_REPORTS/$name"
    if [[ ! -f "$dest" ]]; then
      echo "warn: skip clean reports/$name (missing in repo after copy)" >&2
      continue
    fi
    if remove_path "$src" "reports/$name"; then
      cleaned=$((cleaned + 1))
    else
      clean_failed=$((clean_failed + 1))
    fi
  done
  if [[ "$clean_failed" -gt 0 ]]; then
    echo "warn: $clean_failed item(s) left in the game directory" >&2
  fi
else
  echo "keep-source: left game exports untouched"
fi

echo "Done. new/updated=$copied skipped_existing=$skipped cleaned_game=$cleaned mode=$MODE"
echo "Dest captures: $DEST_CAPTURES"
echo "Dest reports:  $DEST_REPORTS"
echo "Game config:   $CRITIC_CFG"

if [[ "$DO_COMMIT" -eq 1 ]]; then
  cd "$ROOT"
  if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "Not a git repo; skipped --commit." >&2
    exit 0
  fi
  git add "$EXPORTS_DIR/captures" "$EXPORTS_DIR/reports" "$EXPORTS_DIR/LogOutput.log"
  if git diff --cached --quiet; then
    echo "Nothing new to commit."
    exit 0
  fi
  git commit -m "$(cat <<'EOF'
chore: sync in-game diagnostic captures

Pull latest DressmakerCritic captures/reports and LogOutput into exports/.
EOF
)"
  git status -sb
fi
