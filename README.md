# DressCritic

Experimental **read-only** diagnostics for [Dressmaker](https://store.steampowered.com/app/2498800/Dressmaker/) on Linux/Proton with [Yuuma's Dressmaker ModLoader](https://www.nexusmods.com/dressmaker/mods/1) 0.7.8.

Current plugin: **Diagnostics 0.3.2** (`DressmakerCritic-Diagnostics/`).
It does **not** change dresses, commissions, or saves. It only writes diagnostic JSON under `BepInEx/config/DressmakerCritic/`.

## Quick start (build + install)

1. Install Dressmaker via Steam and back up your save.
2. Install the **Steam Deck / Proton** ModLoader into the game folder (not native Linux BepInEx). Confirm **Options → Mod Manager**.
3. Install a .NET SDK (`sudo pacman -S dotnet-sdk` on Arch/CachyOS).
4. From this repository root:

```bash
chmod +x install.sh
./install.sh
```

The script finds the game under common Steam paths, or you can pass it / set an env var:

```bash
./install.sh "$HOME/.local/share/Steam/steamapps/common/Dressmaker"
# or
DRESSMAKER_GAME_ROOT="/path/to/Dressmaker" ./install.sh
```

Compile only (still needs game assemblies for references):

```bash
./install.sh --build-only
```

Installed DLL path:

`…/Dressmaker/BepInEx/plugins/DressmakerCritic/DressmakerCritic.Diagnostics.dll`

Restart the game after installing.

## In-game check

1. Launch Dressmaker through Steam (Proton).
2. Log should show `Dressmaker Critic Diagnostics 0.3.2`.
3. Console (**Ctrl + \`**): `critic-status`, `critic-export`, `critic-types`, `critic-editor`.
4. Or use the **Export diagnostics** button (upper right) — each click creates  
   `BepInEx/config/DressmakerCritic/captures/<timestamp>-<id>/`.

### Pull captures into the repo

After exporting in-game (game can stay open or closed):

```bash
./pull-exports.sh              # only NEW capture folders + LogOutput.log
./pull-exports.sh --commit     # same, then git commit
./pull-exports.sh --all        # refresh every capture from the game folder
```

## Repository layout

| Path | Purpose |
| --- | --- |
| `install.sh` | **Unified** build + install (preferred entry point) |
| `pull-exports.sh` | Copy new in-game captures into `exports/` (optional `--commit`) |
| `DressmakerCritic-Diagnostics/` | Plugin source, lower-level `build.sh` / `build_and_install.sh` |
| `DressmakerCritic-Diagnostics/exports/` | Captured JSON / logs kept for analysis |
| `docs/` | Export schema and runtime findings |
| `tests/` | Bounded reflection safety tests (no game required) |

Do not commit the ModLoader ZIP/folder or game DLLs (see `.gitignore`).

## Docs

- Plugin details and test plan: [DressmakerCritic-Diagnostics/README.md](DressmakerCritic-Diagnostics/README.md)
- Export schema notes: [docs/export-schema.md](docs/export-schema.md)
- Runtime findings / limits: [docs/runtime-findings.md](docs/runtime-findings.md)
- ModLoader APIs: https://threadbox.cc/modloader/index.php/Mod_Loader/CSharp_APIs

## Remove

Exit the game, then delete `BepInEx/plugins/DressmakerCritic/` (and optionally `BepInEx/config/DressmakerCritic/`).

## Unified garment snapshot (v0.3.2)

The export button now also saves `garment-snapshot.json` beside the three diagnostic
files. `critic-editor` produces it too. This read-only normalized snapshot selects
only `GameManager.activeDress.state`, resolves graph references, uses typed JSON
booleans/numbers, and preserves unknowns and collection limits. It does not evaluate
style or recommend changes. See [the snapshot contract](docs/garment-snapshot.md).

## Commission and unlock discovery (v0.3.2)

A fifth file, `context-runtime.json`, attempts narrowly guarded progress discovery.
It reports why access was unavailable or blocked; it does not claim commission or
unlock support is verified. See [the access contract](docs/progress-context.md).

Version 0.3.2 also writes `progress-snapshot.json`: normalized accepted-order and unlocked-membership evidence. Six files are now produced per button capture. See [paired findings](docs/order-pair-findings.md).
