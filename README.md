# DressCritic

Read-only local garment evaluator in development for Dressmaker / Linux-Proton,
BepInEx 5.4.23.5 and Yuuma's ModLoader 0.7.8. Current plugin: **0.12.0**.

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

## Evaluate a garment

Open the garment in the game's saved viewer, or the active garment on mannequin /
photo. Click **Оценить платье** (console alias: `critic-export`), wait for
completion, then run `./pull-exports.sh`. A single folder under
BepInEx/config/DressmakerCritic/captures contains JSON, image(s), readiness codes,
stage failures/timings, identity evidence, hardware and hashes. The same command preserves machine diagnostics; the result panel shows the actual jury assessment.

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

The current model recipe explicitly selects **qwen3-vl:4b-instruct-q4_K_M**.
The old `qwen3-vl:4b` tag resolves to Thinking weights; the no-thinking request
option alone did not yield a final reply in the user's runtime. After updating,
rebuild the existing alias (the config name stays unchanged):

```bash
ollama pull qwen3-vl:4b-instruct-q4_K_M
ollama stop dresscritic-vision
ollama create dresscritic-vision -f model/Modelfile
ollama show --modelfile dresscritic-vision
```

Verify its base is the Instruct model. See [the variant findings](docs/CAPTURE-803.md).

The first Instruct reply completed in 10.9 seconds but copied the old prompt
template verbatim. v0.8.5 removes answer examples, explicitly describes assessed
votes, and rejects the observed placeholder strings. See [capture findings](docs/CAPTURE-804.md).

First real four-house evaluation confirmed on a saved garment: mean **6.25/10**,
17.3 seconds. This validates the end-to-end path on one example; quality and
repeatability remain to be tested. See [findings and next priorities](docs/CAPTURE-805.md).

In v0.9.0 **Оценить платье** opens the jury result panel. **Закрыть** hides it
without cancelling inference; **Открыть результат** brings it back. Results are
explicitly labeled as a snapshot with record number/time, not a live evaluation
of a subsequently selected dress. English model remarks are preserved verbatim.
See [v0.9.0 validation and in-game check](docs/RESULT-UI.md).

v0.9.1 improves result readability with an opaque dark surface, explicit 18px
body text, larger score/house headings and collapsible limitations, independent
of the game's translucent IMGUI skin. No model/config changes required.

In v0.10.0 the **Жюри** bookmark opens a paper-styled overlay with an internal
**Оценить платье** button. Wide layouts show the captured photo and mean beside
house remarks. **Дома мод / Заказ** tabs separate jury and context. Display names:
Maison Orvelle, Bellombre, Virelai, Maison Aveline; model IDs/rubrics are unchanged.
This is the mod's overlay, not a patch to the game's actual notebook tabs.

**v0.11.0 is a fixed-view validation build.** The Evaluate action captures four
candidate orthographic views and diagnostics; inference is deliberately blocked
until their native rendering/coverage has been reviewed. The existing single-angle
viewer image is retained only as a reference. See [the required runtime check](docs/FIXED-VIEWS.md).

v0.11.1 corrects fixed-view framing: mesh-derived mannequin-local bounds replace
renderer world bounds, skipping empty/degenerate geometry. Runtime review is still required.

v0.11.2 prepares four labeled model image parts (same 4 MiB byte budget) and
uses the fixed front image for the snapshot preview. The runtime review gate
remains until camera-rotation invariance and accessory coverage are checked.

v0.12.0 snapshots static meshes/property blocks into a temporary scene with a
fixed neutral light rig and canonical object orientation. It no longer renders
the rotated live mannequin for evaluation views. Native lighting/material fidelity
is awaiting review; inference remains gated. See FIXED-VIEWS.md for the next test.
