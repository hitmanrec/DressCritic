# DressCritic

Read-only local garment evaluator for Dressmaker / Linux-Proton, BepInEx
5.4.23.5, Yuuma ModLoader 0.7.8. Current plugin: **0.15.11**.

The existing history-only inline Jury, house crests, English UI and optional
locale overlay are retained. v0.15.4 fixes the latest model response rejection,
late-result binding and scroll input, and measures the notebook page instead of
assuming the history holder has page-sized bounds. **Native layout needs an
in-game test.** See [current findings and remaining blockers](docs/knowledge/CURRENT.md).

## Knowledge and evidence

Primary direction: [grounded, calibrated local evaluation](docs/EVALUATION-PLAN.md).
The current direct VLM ratings are a prototype baseline, not a validated quality
scale. [Repository audit](docs/REPOSITORY-AUDIT.md) records the next blockers;
input validity and controlled model evaluation take priority over UI cosmetics.

[Offline tools and owned-runtime probe](docs/OFFLINE-EVALUATION.md) now support
capture inventory, repeated baseline/observation requests and local CPU startup/
cleanup without game assemblies. [Recorded CPU probe](docs/research/2026-11-07-cpu-probe.json)
is a one-dress engineering experiment, not a calibrated model comparison.
The current in-game model is unchanged. Fixed-view failure now blocks inference;
the original viewer image remains diagnostic evidence only.

[Structured knowledge base](docs/knowledge/README.md) is the current entry point:

- [Status, blockers and next test](docs/knowledge/CURRENT.md)
- [Runtime/game data](docs/knowledge/RUNTIME.md)
- [Image identity, geometry and lighting](docs/knowledge/RENDERING.md)
- [Model, jury, briefs and UI](docs/knowledge/EVALUATOR.md)
- [Evidence registry](docs/knowledge/EVIDENCE.md) and [machine facts](docs/knowledge/facts.json)
- [Product contract](docs/PRODUCT.md)

Historical reports preserve evidence but may contain superseded installation or
next-test instructions. Some chat-uploaded source archives are not stored in Git;
the registry identifies that distinction explicitly.

## Build and install

Requires .NET SDK and an authorized local game installation with the loader.
Close the game, then from the repository:

```bash
git pull --ff-only
bash install.sh
# Or: bash install.sh "/actual/path/to/Dressmaker"
```

No proprietary DLLs are included. Game saves, quests and garments are never
written. Unity API changes must be verified against installed game assemblies;
core tests and syntax checks cannot establish native compilation/rendering.

## Current in-game check

Open a saved dress with accessories in history, click **Rate dress**, then switch
to another dress while inference is running: the old result must not appear on
that page. Evaluate the selected dress and check four house crests/remarks and
scrolling. Leave history: Jury disappears and native controls are restored.
If Rate is inaccessible, use console `critic-export`. Import with
`bash pull-exports.sh`; the packet includes bounded `ui-layout*.json` geometry.
Detailed next test and remaining render checks: [CURRENT](docs/knowledge/CURRENT.md).

Optional locale: copy `DressmakerCritic-Diagnostics/locale.example.ru.json` to
`BepInEx/config/DressmakerCritic/locale.json`.

## Local model configuration

Current recipe: **qwen3-vl:4b-instruct-q4_K_M**, alias dresscritic-vision.
Ollama >=0.12.7, context16384, temperature0.2. For a first installation or migration
from the old Thinking variant, with Ollama running:

```bash
ollama pull qwen3-vl:4b-instruct-q4_K_M
ollama create dresscritic-vision -f model/Modelfile
```

The game's BepInEx/config/community.dressmakercritic.diagnostics.cfg:

```ini
[Inference]
Model = dresscritic-vision
Endpoint = http://127.0.0.1:11434/v1/chat/completions
TimeoutSeconds = 300
```

Existing configured installations need no model change now. A browser GET to
/chat/completions returning405 is expected; that API requires POST. /api/tags
lists models. Manual Ollama is a development dependency, not final packaging.

Selection.SavedDressIndex=-2 uses exact display/history identity then active
fallback; -1 explicitly selects active; >=0 is a zero-based history record.
History dresses now use their own saved quest record automatically. Known
commission/off-rack origins are classified by version-scoped IDs; unseen IDs stay
unknown. `history-order.json` records origin, historical requirements, recipient
and quest giver separately. Sourced brief lookup uses the historical definition;
off-rack dresses receive the independent jury without a commission request.
Selection.OrderIndex only applies to active dresses (-1: no associated order;
>=0: explicit accepted-order index); history ignores it. Do not derive either
index from viewer siblingIndex. Full dialogue/customer preferences remain unknown.

## Validation

```bash
dotnet run --project tests/Introspection.Tests.csproj
python3 tests/test_capture_importer.py
python3 tests/test_offline_evaluation.py
python3 tools/review_capture.py /path/to/capture
```

Schemas and real-response fixtures test protocol/unknowns/isolation/aggregation;
HTTP tests use an injected handler. Real local inference was separately observed,
but score calibration, repeatability, full context evaluation and final deployment
remain unfinished. UI is a mod-owned overlay, not a native notebook tab.
