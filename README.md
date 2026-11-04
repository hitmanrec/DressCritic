# DressCritic

Read-only local garment evaluator in development for Dressmaker / Linux-Proton,
BepInEx 5.4.23.5 and Yuuma's ModLoader 0.7.8. Current plugin: **0.8.3**.

The current objective is one real local multimodal assessment, not a diagnostics
viewer. See [the audit, concrete blockers and next work](docs/AUDIT.md).

The target is [four independent fashion houses](docs/PRODUCT.md): harmony, character,
avant-garde and atelier. Each returns 0–10 and a short remark; the mod computes
the mean and verdict. Order compliance and customer compatibility are separate,
optional channels. The jury receives neither the brief nor customer preferences.
This contract is implemented; real-model calibration and the final score UI remain pending.

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

Latest iteration: [two v0.8.0 captures and the preview identity fix](docs/CAPTURE-800.md).

The first verified saved-garment request reached local inference but hit its old
90-second deadline. v0.8.2 uses `[Inference] TimeoutSeconds = 300` (30–600 allowed);
see [capture findings and next test](docs/CAPTURE-801.md).

The first model reply exhausted its output budget without final JSON. v0.8.3
requests `reasoning_effort=none`, rejects truncated/empty replies explicitly, and
positions the JSON reader before parsing. See [the findings](docs/CAPTURE-802.md).
