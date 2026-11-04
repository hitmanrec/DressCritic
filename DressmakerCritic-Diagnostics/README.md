# Dressmaker Critic — Diagnostics 0.2 (experimental source)

A **read-only diagnostic scaffold** for Dressmaker + Yuuma's Dressmaker ModLoader 0.7.8.
No AI and no changes to dresses, commissions, or save files yet.

## Prerequisites (Linux / Proton)

1. Install Dressmaker through Steam and make a manual backup of your save.
2. Install the appropriate latest **Windows/Proton or Steam Deck** release of Yuuma's Dressmaker ModLoader into the game folder. Follow the loader author's current instructions; don't install native Linux BepInEx blindly into the Proton directory. Confirm `BepInEx/plugins/DressmakerMods.dll` exists and the game has **Options → Mod Manager**.
3. Install a .NET SDK. On Arch/CachyOS: `sudo pacman -S dotnet-sdk`.
4. Building uses a Microsoft .NET Framework reference-assemblies NuGet package. `dotnet restore` needs Internet on its first run.

## Build and install

Find the game folder from Steam → Dressmaker → Properties → Installed Files → Browse.
The folder must contain `Dressmaker.exe`.

From the extracted source folder:

```bash
chmod +x build_and_install.sh
./build_and_install.sh "$HOME/.local/share/Steam/steamapps/common/Dressmaker"
```

Replace the path with the **real** installation directory, including capitalization.
The script does NOT install the ModLoader itself. It copies only our compiled plugin DLL to `BepInEx/plugins/DressmakerCritic/`.

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

Build without installing: `bash build.sh "/actual/path/to/Dressmaker"`.
Build and install: `bash build_and_install.sh "/actual/path/to/Dressmaker"`.
Both work with paths containing spaces on Linux/Proton; use an authorized game
installation and the appropriate Windows/Proton loader. Do not commit game DLLs.
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
