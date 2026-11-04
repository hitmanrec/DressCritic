# DressCritic

Read-only local garment evaluator for Dressmaker / Linux-Proton, BepInEx
5.4.23.5, Yuuma ModLoader 0.7.8. Current plugin: **0.15.3**.

Fixed-view capture and local jury inference work when a model is configured.
History-only inline Jury UI is **in progress and currently broken in-game**
(controls land on the book spine; house crests/remarks do not layout). See
[docs/knowledge/CURRENT.md](docs/knowledge/CURRENT.md).

## Knowledge and evidence

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

Until the left-page Jury layout is fixed, treat UI checks as blocked; inference
captures via console `critic-export` / history Rate (if reachable) still produce
packets for `bash pull-exports.sh`. Locale overlay sample:
`DressmakerCritic-Diagnostics/locale.example.ru.json` →
`BepInEx/config/DressmakerCritic/locale.json`.

## Local model configuration (for inference after the render gate is resolved)

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
Selection.OrderIndex=-1 means no associated order; >=0 explicitly selects an
accepted order. Do not derive either index from viewer siblingIndex.

## Validation

```bash
dotnet run --project tests/Introspection.Tests.csproj
python3 tests/test_capture_importer.py
python3 tools/review_capture.py /path/to/capture
```

Schemas and real-response fixtures test protocol/unknowns/isolation/aggregation;
HTTP tests use an injected handler. Real local inference was separately observed,
but score calibration, repeatability, full context evaluation and final deployment
remain unfinished. UI is a mod-owned overlay, not a native notebook tab.
