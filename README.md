# DressCritic

Read-only local garment evaluator in development for Dressmaker / Linux-Proton,
BepInEx 5.4.23.5 and Yuuma's ModLoader 0.7.8. Current plugin: **0.7.1**.

The current objective is one real local multimodal assessment, not a diagnostics
viewer. See [the audit, concrete blockers and next work](docs/AUDIT.md).

## Install

Install the game and appropriate Proton/Steam Deck ModLoader, then from this repo:

```bash
./install.sh
# or: ./install.sh "/actual/path/to/Dressmaker"
```

.NET SDK and authorized game assemblies are required. Restart after installation.
No proprietary DLLs are included. Runtime state/saves are never edited.

## One diagnostic iteration

Open the garment in the game's saved viewer, or the active garment on mannequin /
photo. Click **Диагностика оценщика** (console alias: `critic-export`), wait for
completion, then run `./pull-exports.sh`. A single folder under
BepInEx/config/DressmakerCritic/captures contains JSON, image(s), readiness codes,
stage failures/timings, identity evidence, hardware and hashes. No separate text,
image or inspection buttons; no human reports are generated.

Local inference runs only if image identity is verified, facts are present and a
model is explicitly configured. The loopback OpenAI-compatible client and strict
assessment validation are implemented; actual model quality/in-game integration
remain unverified. Without a model, the same capture records the blockers and local
server metadata so the next step is concrete.

Configuration: [audit](docs/AUDIT.md#configuration-and-exactly-one-next-in-game-test).
English brief data is partial and source-backed; missing semantic context remains
unknown. Recommendations are disabled until real editor operations are mapped.

## Validation

```bash
dotnet run --project tests/Introspection.Tests.csproj
python3 tests/test_capture_importer.py
python3 tools/review_capture.py /path/to/capture
```

Tests cover production data/inference code on saved fixtures and an injected HTTP
handler, not a real local model or the game's Unity rendering. Old versioned docs
and committed captures remain research history; their removed UI instructions are
not the current workflow.

Latest real packet findings and model setup: [capture-700 analysis](docs/CAPTURE-700.md). The viewer uses shared URP render output; 0.7.1 collects it even while its garment identity is unresolved.
