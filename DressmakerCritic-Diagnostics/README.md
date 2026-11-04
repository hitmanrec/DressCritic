# Dressmaker Critic — Diagnostics 0.2 (experimental source)

A **read-only diagnostic scaffold** for Dressmaker + Yuuma's Dressmaker ModLoader 0.7.8.
No AI and no changes to dresses, commissions, or save files yet.

## Prerequisites (Linux / Proton)

1. Install Dressmaker through Steam and make a manual backup of your save.
2. Install the appropriate latest **Windows/Proton or Steam Deck** release of Yuuma's Dressmaker ModLoader into the game folder. Follow the loader author's current instructions; don't install native Linux BepInEx blindly into the Proton directory. Confirm `BepInEx/plugins/DressmakerMods.dll` exists and the game has **Options → Mod Manager**.
3. Install a .NET SDK. On Arch/CachyOS: `sudo pacman -S dotnet-sdk`.
4. Building uses a Microsoft .NET Framework reference-assemblies NuGet package. `dotnet restore` needs Internet on its first run.

## Build and install

Preferred: from the **repository root**, one command builds and installs:

```bash
chmod +x ../install.sh   # once
../install.sh
# or: ../install.sh "/path/to/steamapps/common/Dressmaker"
# or: ../install.sh --build-only
```

`install.sh` auto-detects common Steam/Proton game paths (or uses
`DRESSMAKER_GAME_ROOT`). It does **not** install the ModLoader; it only copies
`DressmakerCritic.Diagnostics.dll` to `BepInEx/plugins/DressmakerCritic/`.

Lower-level scripts in this folder (same result if you already know the path):

```bash
bash build_and_install.sh "$HOME/.local/share/Steam/steamapps/common/Dressmaker"
bash build.sh "$HOME/.local/share/Steam/steamapps/common/Dressmaker"   # compile only
```

Find the game folder from Steam → Dressmaker → Properties → Installed Files → Browse.
The folder must contain `Dressmaker.exe`.

## Verify in game

1. Launch Dressmaker through Steam. Load a save, open the dress editor or go to the desk.
2. Open the ModLoader console with **Ctrl + backtick (`)**; try the `help` command if your keyboard layout differs.
3. Run `critic-status`.
4. Run `critic-export` to write:
   - `BepInEx/config/DressmakerCritic/diagnostics.json`
   - `BepInEx/LogOutput.log` (the loader writes this)
5. Also obtain the loader's automatically generated catalog:
   - `BepInEx/config/DressmakerMods/garment-reference.json`

Bring these **three files** back to the conversation, or just `diagnostics.json` and error lines from the log if you prefer. Logs may contain system paths or other mod information; review them before sharing.

## Important limits

- This is source code, **not a verified precompiled mod**. It is based on the loader's public API and must be compiled and tested against your installed game version.
- The initial snapshot exports visible quest identifiers and limited status, not the full garment or commission text. Some active-commission fields may be absent until we inspect actual game classes.
- Does not query, change, or save the game's progress.
- Does not read private data, screenshots, or images.
- If ModLoader's API changes, update the dependency version/API calls as required.

## Remove

Exit the game and delete `BepInEx/plugins/DressmakerCritic/` and (optionally) `BepInEx/config/DressmakerCritic/`.

## Relevant docs

- https://threadbox.cc/modloader/index.php/Mod_Loader/CSharp_APIs
- https://threadbox.cc/modloader/index.php/Mod_Loader/Plugin_lifecycle
- https://threadbox.cc/modloader/index.php/Mod_Loader/Reference/API_index
- https://www.nexusmods.com/dressmaker/mods/1

## Diagnostics v0.2 — exploratory runtime exports

Version 0.2 keeps `critic-status` and `critic-export` and adds:

- `critic-types`: candidate game/loader type names, fields and property metadata,
  written to `BepInEx/config/DressmakerCritic/runtime-types.json`.
- `critic-editor`: bounded field graphs for game components in the active scene,
  written to `BepInEx/config/DressmakerCritic/editor-runtime.json`.
- `critic-export` now writes schema 0.2.0, recent event observations and field graphs
  for the most recent garment/fabric event roots to the original diagnostics.json.

All commands are read-only with respect to game state; only diagnostic files are
written. They never invoke arbitrary getters or enumerate game collections.
Legacy quest extraction used speculative property reads; v0.2 disables those
reads and retains the legacy keys with explicitly unresolved summaries. See
[findings and limits](../docs/runtime-findings.md) and [schema](../docs/export-schema.md).
No evaluator, AI requests, screenshots or game asset copying are included.

From the repo root: `./install.sh` (build+install) or `./install.sh --build-only`.
From this folder: `bash build_and_install.sh "/actual/path/to/Dressmaker"` or
`bash build.sh "/actual/path/to/Dressmaker"`. Paths with spaces work on
Linux/Proton; use an authorized game install and the Windows/Proton loader.
Do not commit game DLLs.
Run independent reflection safety tests from the repository root with
`dotnet run --project tests/Introspection.Tests.csproj` (.NET 8 SDK).
The plugin itself still targets net472. Safety tests do not verify Unity or loader APIs.

### Next in-game test

1. Exit the game, back up the save, build/install v0.2 and restart. Verify the log
   identifies plugin 0.2.0 and registers all four commands without exceptions.
2. At the title screen run all four commands. Confirm absent roots produce null
   nodes and commands return gracefully. Copy outputs elsewhere before repeating:
   filenames are overwritten on each successful export.
3. Load a save with a known commission, enter PatternCutting, select known
   components, variants, panel fabrics/colours and accessories through normal UI.
   Run `critic-types`, `critic-editor`, `critic-export`. Record the UI choices
   alongside the files and scene/time. Confirm observations correspond to choices;
   do not interpret the last selection event as the whole garment.
4. Change one UI choice at a time, repeat exports and compare field paths. Open the
   commission/desk UI and repeat to locate brief text, identifiers and requirements.
5. Repeat with saves having different known unlocks and inspect inventory/editor
   fields versus the reference catalog. Do not unlock anything through diagnostics.
6. Check references/limit statuses, responsiveness, scene transition clearing,
   absent objects, and output sizes. Verify normal play and saving behave as before.
   Review exports for private strings before sharing diagnostics.json,
   runtime-types.json, editor-runtime.json and relevant log excerpts.

The cloud environment lacks proprietary game assemblies: v0.2 has not been
compiled against the real game or tested in-game. The steps above are required
before treating it as verified. Never substitute fake game APIs for that check.

### Export button on every screen

The plugin draws an **Export diagnostics** button in the upper-right corner using
Unity IMGUI, independent of the game's scene UI. Click it at the title screen,
in the editor, at the desk, or in another scene. Each click writes all three JSON
exports to its own directory:

`BepInEx/config/DressmakerCritic/captures/<UTC timestamp>-<capture id>/`

Previous captures are retained for comparisons. The overlay reports complete,
partial, or failed export; the BepInEx log records the full path and individual
results. Exports are synchronous and may briefly pause the game. A two-second
cooldown prevents rapid repeated clicks; idle drawing performs no reflection scans.
The diagnostic overlay does not pause the game or change cursor/quest/garment state.
If the game captures the mouse, use its normal menu to release the pointer.

Set `[Diagnostics] ShowExportButton = false` in
`BepInEx/config/community.dressmakercritic.diagnostics.cfg` and restart to hide it.
Console commands still work and retain their original output locations.
Building now also requires `Dressmaker_Data/Managed/UnityEngine.IMGUIModule.dll`
from the authorized game installation.

In-game verification still required: confirm the button survives scene changes,
is legible on Linux/Proton at the actual resolution, is clickable without changing
a garment underneath it, and produces three valid JSON files per capture.
Test title screen, PatternCutting and commission/desk screens, a second capture,
a read-only output directory (error feedback), and the hidden-button setting.
IMGUI does not guarantee blocking game-specific input handlers underneath the
button; if click-through occurs, hide the overlay and use console commands until
that integration is addressed. Screen-space placement and input behavior have not
been validated against the proprietary game runtime in this environment.

### v0.2.1 export-content fix

The first five real button captures revealed that Unity's native serializer
omitted nested diagnostic DTOs and lists. Version 0.2.1 uses a dedicated bounded
DTO JSON writer; schema version remains 0.2.0. Existing uploaded captures remain
untouched as evidence. After updating, first make one capture and check that
`runtime`, `objects` and `types` are present in their respective files before
recording a longer session. See the findings document for the exact limitations.

### v0.2.2 targeted current-state capture

Real v0.2.1 files confirm nested JSON works, but scene traversal exhausted its
budget before reaching activeDress. v0.2.2 captures known GameManager screen fields,
current dress state and up to 32 panel fabric roots first, then generic graphs.
Editor graphs have a 4096-node shared budget; event graphs remain at 512 nodes.
All focused paths are instance fields proven in the real type metadata. Missing
paths remain null; no singleton/property getter is called. Focused List<T> reading
accepts only the exact BCL type and bounds reads by logical count and array length.
After updating, compare before/after a single panel fabric change and at assembly;
inspect labelled roots in editor-runtime.json. In-game validation remains required.

### v0.2.3 loaded-object discovery

The v0.2.2 captures confirm internal screens CuttingRoom/Mannequin, but activeDress
is null. Export now searches loaded Dress and PatternPanel components directly,
including inactive objects, and reads their proven state fields. Inspection is
capped at 8 dresses/32 panels; native search allocates its results before caps.
Fallback component graphs are shallow. No game getters or static values are read.
After updating, one mannequin capture is sufficient to check whether these objects
exist. Inactive/historical objects are candidates, not authoritative current state.

### v0.3.0 unified garment snapshot

The export button now creates **four** files: the existing diagnostics plus
`garment-snapshot.json`. `critic-editor` writes both the raw editor graph and this
new snapshot to its normal directory. It contains typed selected components,
panels, fabric identities, accessory observations and construction flags, with
explicit unknown/absent/truncated states. It uses the existing graph and adds no
runtime scans. See [the contract and offline validation](../docs/garment-snapshot.md).

### v0.3.1 guarded commission/unlock context

Button and `critic-editor` exports now also write `context-runtime.json` (five files
per button capture). It attempts only the known PlayerProgress._current backing
field and refuses to read it if a static initializer exists. Available data is
limited to current-commission fields and unlocked pattern/fabric/accessory lists.
Blocked or absent progress is reported explicitly. Live access and semantic brief
mapping remain unverified; [details and one-capture test](../docs/progress-context.md).

### v0.3.2 normalized order and unlock snapshot

Export now writes `progress-snapshot.json` as a sixth file. Real captured progress
is normalized into no-order/selected/unknown states, raw completion requirements,
and inventory membership with explicit completeness. Compact unlock IDs/names
are capped at 512 per category; raw graphs keep their earlier limits. Focused
requirement tag roots avoid deep recursion. Existing garment schemas are unchanged.
See [the paired order findings](../docs/order-pair-findings.md).

### v0.4.0 Inspect garment card

**Inspect garment** opens a cached, scrollable facts card without writing files.
Refresh reads the open active garment and progress once, with a two-second cooldown.
Finished active garments work; unverified history/preview objects are never chosen
automatically. Current-order requirements are reference information only; no
compliance/visual score is computed. [Usage and live-test limits](../docs/garment-card.md).

### v0.4.1 saved-history selection

Use Inspect garment -> Saved dresses -> select an entry, then Refresh if needed.
This reads completed-order dress records explicitly, independently of activeDress
or the game's thumbnail selection. Active switches back to the current design.
Export after inspecting includes inspected-garment.json with the selected source;
normal garment-snapshot.json remains the live active design. Free-design history
outside completedQuests is not yet supported. See the card documentation for limits.

### v0.5.0 русский интерфейс и подробный отчёт

Карточка переведена на русский: панели и аксессуары перечислены отдельно,
неизвестные значения и неполные списки отмечены явно. Заказ выбирается вручную
кнопкой «Заказ»; при смене платья выбор сбрасывается. «Сохранить отчёт» пишет
читаемый UTF-8 .txt в BepInEx/config/DressmakerCritic/reports/; диагностический
экспорт также сохраняет inspection-report.txt после проверки. Сравнения платьев
нет. Схемы исходных снимков и их обычный выбор заказа не изменены.
