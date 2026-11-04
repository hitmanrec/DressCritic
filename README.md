# DressCritic

Experimental **read-only** diagnostics for [Dressmaker](https://store.steampowered.com/app/2498800/Dressmaker/) on Linux/Proton with [Yuuma's Dressmaker ModLoader](https://www.nexusmods.com/dressmaker/mods/1) 0.7.8.

Current plugin: **Diagnostics 0.6.0** (`DressmakerCritic-Diagnostics/`).
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
2. Log should show `Dressmaker Critic Diagnostics 0.6.0`.
3. Console (**Ctrl + \`**): `critic-status`, `critic-export`, `critic-types`, `critic-editor`.
4. Or use the in-game export/inspection UI. v0.5+ writes readable reports to
   `BepInEx/config/DressmakerCritic/reports/inspection-*.txt`; full diagnostic
   folders still go under `…/captures/<timestamp>-<id>/` when that export is used.

### Pull captures into the repo

After exporting in-game (game can stay open or closed):

```bash
./pull-exports.sh              # NEW captures + reports + log, then clean game copies
./pull-exports.sh --commit     # same, then git commit
./pull-exports.sh --all        # refresh every item from the game folder
./pull-exports.sh --keep-source  # copy without cleaning the game export dirs
```

## Repository layout

| Path | Purpose |
| --- | --- |
| `install.sh` | **Unified** build + install (preferred entry point) |
| `pull-exports.sh` | Copy new in-game `captures/` + `reports/` into `exports/` (optional `--commit`) |
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

## Garment facts card (v0.4.0)

Click **Проверить платье** in the upper-right corner, then **Обновить** after changes.
The scrollable card shows the open active garment, construction progress, materials,
accessories and current-order requirements as separate reference information.
It does not compute compliance, game scores or visual quality. Finished active
garments are supported; history/viewer compatibility depends on whether the game
exposes that garment as active. See [card usage and limits](docs/garment-card.md).

### Saved garment selection (v0.4.1)

In **Проверить платье**, choose **Сохранённые**, then an entry from completed-order
history. The card shows its source explicitly. **Текущее** switches back to the
current garment. Export after inspecting to include `inspected-garment.json`;
`garment-snapshot.json` continues to describe activeDress, which may be a different
object. Saved-history access still requires live verification.

## Русская карточка и отчёт (v0.5.0)

Нажмите **Проверить платье**. Доступны **Текущее**, **Сохранённые**, **Обновить**,
**Заказ**, **Сохранить отчёт** и **Закрыть**. Заказ выбирается явно из принятых;
при смене платья привязка сбрасывается. **Без заказа** убирает выбранный заказ.
Карточка показывает каждую панель, ткани, готовность и известные детали аксессуаров,
а также отдельно отмечает неизвестные и неполные данные. Сравнения платьев нет.

Читаемые UTF-8 отчёты сохраняются в `BepInEx/config/DressmakerCritic/reports/`.
Диагностический захват после проверки также содержит `inspection-report.txt`.
Игровые названия предметов и заказов сохраняются как в данных игры;
оценка красоты, игровые очки и вердикт о выполнении заказа пока не вычисляются.

## Три входа оценщика (v0.6.0)

Добавлены кнопки **Текст задания** и **Изображение**, а также более глубокое чтение
аксессуаров без увеличения общей глубины. Текст диалога сохраняется как отдельное
свидетельство; привязка к заказу и полнота задания не предполагаются. Изображение
берётся из открытого игрового просмотра или временной камеры манекена/фотоэкрана;
неподдерживаемые случаи сообщаются явно. Новые пути ещё требуют проверки в игре.
Файлы сохраняются в captures/brief-* и captures/image-* и копируются pull-exports.sh.
См. [подтверждённые результаты, ограничения и отдельные проверки](docs/phase-three-inputs.md).
