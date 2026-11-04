#!/usr/bin/env bash
set -euo pipefail
if [[ $# -ne 1 ]]; then
  echo 'Usage: bash build.sh "/path/to/Dressmaker"' >&2
  exit 2
fi
ROOT="$(cd "$(dirname "$0")" && pwd)"
GAME_DIR="$(realpath "$1")"
for relative in Dressmaker.exe BepInEx/core/BepInEx.dll BepInEx/plugins/DressmakerMods.dll Dressmaker_Data/Managed/UnityEngine.CoreModule.dll Dressmaker_Data/Managed/UnityEngine.ImageConversionModule.dll Dressmaker_Data/Managed/UnityEngine.IMGUIModule.dll Dressmaker_Data/Managed/UnityEngine.TextRenderingModule.dll Dressmaker_Data/Managed/UnityEngine.UI.dll Dressmaker_Data/Managed/UnityEngine.UIModule.dll Dressmaker_Data/Managed/UnityEngine.JSONSerializeModule.dll Dressmaker_Data/Managed/UnityEngine.dll Dressmaker_Data/Managed/Framework.dll Dressmaker_Data/Managed/Assembly-CSharp.dll; do
  if [[ ! -f "$GAME_DIR/$relative" ]]; then
    echo "Required game/loader file missing: $relative" >&2
    exit 1
  fi
done
command -v dotnet >/dev/null || { echo '.NET SDK required' >&2; exit 1; }
dotnet build "$ROOT/DressmakerCritic.Diagnostics.csproj" -c Release "-p:DressmakerGameRoot=$GAME_DIR" -o "$ROOT/out"
