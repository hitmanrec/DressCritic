# Product audit and execution plan — 2026-11-04 / v0.7.0

## Target, not a diagnostics product

Open a current or saved garment in the game's normal UI -> optionally select an
order -> evaluate locally -> receive separate visual-quality and brief observations.
No save/garment mutation. Game tag requirements are a separate channel, not beauty.
No recommendation may require unsupported editor actions or unavailable resources.

## Findings from source and actual captures

- Structural state and unlocked membership work. Real saved reports show distinct
  garments, fabrics and accessory counts. Whole-dress completion and individual
  panel flags can differ; 100% stitched flags are not a validated finish criterion.
- An actual 2048x2048 historical viewer image works, but was explicitly unpaired.
  A nearby report timestamp or matching appearance does not establish identity.
- We had spent successive versions on cards, readable reports, manual selection
  widgets and repeated exports while **no inference transport existed**.
- Export pathways could collect different objects at different times, silently
  omit values at reflection depth limits and equate file existence with success.
- Focused Vector3/Quaternion observations were collected but not used: value types
  have no graph reference ID. This explains why coordinates stayed unknown.
- Speaker-only `Rose` text is not a client brief. The existing reference has five
  independently written, cited English summaries, not complete game dialogue.
- Many old tests exercised formatting rather than the end-to-end assessment path.

## Essential blockers and their current state

| Priority | Blocker | This change | Remaining proof |
| --- | --- | --- | --- |
| P0 | Image and facts must describe the same garment | Shared capture ID and file hashes; active-state verification; historical binding requires exactly one clean thumbnail **object reference** match | Validate actual viewer-to-history reference mapping in game; if absent, inspect emitted identity/API evidence, not guessed indices |
| P0 | No local model integration | OpenAI-compatible loopback client, real image payload, bounded async HTTP, response preservation, strict result validation | Hardware/model IDs from next packet; configure a vision model and run one genuine assessment |
| P0 | No stable model input/result boundary | Compact evaluation-input + explicit unknowns; separate visual/brief/game channels; image hashes checked before request | Validate response quality against the actual chosen model, not only injected test replies |
| P1 | Unknown editor operations make advice unsafe | Action recommendations disabled; extra recommendation result members rejected | Map operations/ownership only when adding actionable recommendations |
| P1 | Brief coverage incomplete | Exact/version-scoped English reference retained; missing brief forces unknown semantic compliance | Expand verified English sources when needed; does not block standalone visual assessment |
| P1 | Runtime code never compiled/tested here against game assemblies | Pure production core exercised on real captures and injected HTTP server | User game build + single machine packet; do not claim native camera/runtime integration verified |

Precise deterministic score reproduction, perfect accessory detail, all dialogue
history, multi-dress comparison, text report presentation and a polished facts UI
are **not prerequisites** for the first honest local visual assessment. No score
is fabricated while these are unknown.

## Removed, not hidden behind flags

Deleted the facts card, Russian report formatter, UI lists, text-report writing,
per-feature text/image buttons, speaker-text collector, general scene dump,
per-frame/event chatter, legacy empty quest summary placeholders and their
formatting-only tests. Existing committed captures/reports are evidence and remain
untouched. Kept source-backed reference data, read-only field readers, normalized
snapshots and image rendering because the evaluator consumes them directly.

One temporary launcher remains: **Диагностика оценщика** (also critic-export).
It runs the same data preparation and inference boundary intended for final Evaluate.
It is not another product workflow; no new card/pop-up/report feature is planned.

## One game iteration, one complete machine packet

All Unity reads/rendering execute before the first await. Model/network work is
async; idle UI renders no reflection. A packet includes:

- runtime.json: game/Unity/pipeline, GPU/VRAM and RAM/CPU (no environment dumps).
- identity-evidence.json: all available historical record thumbnails (max 512),
  dirty flags, exact texture instance IDs/names, viewer objects and siblingIndex
  as **uninterpreted evidence**, so one capture can diagnose identity.
- selection.json: requested/resolved record indices and source, without game writes.
- garment.json and garment-raw.json: the chosen design and bounded focused graph.
  Focused value-type accessory coordinates now survive normalization. State is re-read after rendering; changes invalidate image binding and emit garment-after-render.json.
- order.json/order-raw.json and inventory.json: deterministic requirements and
  current membership; evaluation association is explicit, never automatic.
- targeted-api.json: field/property metadata plus method signatures for the exact
  types needed to resolve identity/selection gaps; no method execution or IL dumps.
- image PNG(s)/image-evidence.json (render metadata v2; evaluation-input is the sole binding authority): automatic same-command attempt, no extra clicks.
- evaluation-input.json: model facts, references/hashes and explicit unknowns;
  full inventory/private runtime graphs are not dumped into the model prompt.
- readiness.json: hard blocker codes and softer limitations, not a vague ready flag.
- backend.json/local-models.json: explicit local configuration and a 3-second
  metadata probe. Model IDs are not evidence of vision support and not auto-chosen.
- request-audit.json, model-response.json, assessment.json when applicable: actual
  request metadata/system instruction (no duplicate base64 file), bounded raw
  response even when validation rejects it, and only a validated assessment.
- manifest.json: independent stage results/durations and every artifact's SHA-256.
  `complete` means collection finished, not that all stages passed or mod is ready.

Stage failures do not erase other useful evidence. A progress marker prevents
pull-exports.sh from copying/deleting a packet mid-inference. --all permits a
partial forensic copy but leaves its source intact; a later completed copy refreshes
that partial destination. No repetitive request for a different button is needed.

Review offline: `python3 tools/review_capture.py <capture-directory>`.

## Configuration and exactly one next in-game test

BepInEx/config/community.dressmakercritic.diagnostics.cfg:

- Selection.SavedDressIndex: -2 auto (default): choose history only by exact clean
  thumbnail reference, otherwise active state. -1 explicitly active; >=0 selects
  that zero-based completedQuests record. No game selection/setter is invoked.
- Selection.OrderIndex: -1 means visual-only, >=0 explicitly chooses an accepted
  order for this assessment. Current game order is still available as diagnostics.
- Inference.Model: blank prevents requests; exact local vision-model ID when known.
- Inference.Endpoint: default http://127.0.0.1:11434/v1/chat/completions. Only
  loopback HTTP(S), no embedded credentials/query, no redirects/hosted service.

For the next iteration, keep the model blank if not installed/configured, open the
saved garment viewer (or current mannequin/photo), click the **one** diagnostic
launcher and wait until it finishes. Run pull-exports.sh once. That packet must
answer identity, accessory fields, native rendering, hardware, model server presence
and what blocks the first real call. No new dress or repeated construction run.

## Verification limits

Core tests include real nine-stage garment fixtures, order fixtures, image bytes,
value-type focused roots, conflict/unknown/version rules, unique/stale thumbnail
identity, request-image hashes, actual multimodal HTTP payload, valid/invalid server
responses, forbidden remote endpoints and invented game/advice output, and stage
failure preservation. HTTP tests use an injected handler, **not a real vision model**.
Importer filesystem behavior is tested separately in a temporary fixture tree.

The production core also compiles against net472 reference assemblies; native Unity source passed Roslyn syntax validation only. The optional NuGet advisory feed was unreachable during that cached-reference build (warning).

Game DLLs are absent in this cloud workspace. The new native capture coordinator,
metadata access and System.Net.Http availability must be verified by the next game
build. A large passing unit-test count is not evidence those work or that the
assessment is useful. No final-mod readiness is claimed.

## Work queue after that packet

1. Resolve/verify exact image identity using the emitted evidence, or implement the
   minimal source-specific binding correction without adding diagnostic UI.
2. Select a locally runnable vision model based on actual VRAM/RAM, configure it,
   run the existing transport and inspect its structured result/latency.
3. Only then replace the diagnostic launcher with Evaluate + the final assessment
   result view. Add supported recommendations only with verified capability rules.

## Follow-up: real 0.7.0 packet

[CAPTURE-700.md](CAPTURE-700.md) records the shared RenderTexture mismatch, the premature image-stage guard corrected in 0.7.1, confirmed RTX 5070/12 GB hardware and URP, and concrete local model setup. Native compilation/load of 0.7.0 is now confirmed by the user packet; the new 0.7.1 selector probe is not yet game-verified.
