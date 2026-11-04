# Dressmaker Critic — Diagnostics 0.1 (experimental source)

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
