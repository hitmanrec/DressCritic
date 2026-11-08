> Текущая версия/блокеры: [CURRENT](knowledge/CURRENT.md). Разделы с номерами
> старых версий ниже описывают историю реализации, не текущий следующий тест.

# DressCritic product contract — 2026-11-07

This is the current product contract. The primary execution plan is
[EVALUATION-PLAN](EVALUATION-PLAN.md); the source audit is
[REPOSITORY-AUDIT](REPOSITORY-AUDIT.md). They supersede historical UI queues and
the prototype's free-form generation of four scores. Offline comparison is a
development tool, not a player-facing multi-garment feature. An encoder-only
release is not the target; a learned lightweight scoring component is conditional
on evidence, not prohibited or preselected.

## Product goal and release acceptance

Deliver a working, self-contained, fully local mod that quickly reviews saved
dresses on demand from the game's history screen and integrates seamlessly with
the existing notebook interface. External setup is limited to downloading the
supported computer-vision model. The player must not need to install, configure
or manually start a separate inference service, edit endpoint settings, or use
a terminal to obtain a review.

The mod distribution must supply and manage its compatible inference runtime:
model discovery, startup, readiness, shutdown of owned processes, and useful
in-game failure states. Capture, analysis and results remain on the player's
computer; no hosted API or network dependency is allowed during evaluation.
The existing manually configured Ollama endpoint is a development adapter, not
completion of this requirement. The production runtime is not yet selected or
packaged; automatic launching of a user's unrelated Ollama installation alone
would not satisfy self-contained delivery.

Release acceptance requires:

- A downloaded supported model and installed mod suffice to evaluate a history
  dress without other software setup or a manually running server.
- Rate dress, progress, four house reviews and failures appear within the native
  history interface; controls remain readable, scrollable and correctly bound to
  the selected dress across switching and leaving history.
- Canonical images include visible accessories and give consistent framing and
  lighting independent of the viewer's rotation.
- Reviews keep the game responsive. Cold startup and warm review latency are
  measured separately on the documented target machine; previous timings alone
  do not establish a production latency guarantee.
- Fully offline evaluation and runtime cleanup are verified on Linux/Proton.

## Primary evaluation decision

Separate game facts, visual observations, calibrated criterion/house scoring,
and grounded review wording. A successful JSON response is not evidence of
aesthetic quality. Neither numeric range stretching nor added randomness can
replace recognition and calibration. Current direct-score inference remains a
baseline until the controlled evaluation set and candidate comparison justify a
replacement; no new model is selected yet.

Start with input validity, a roughly 30–50-dress pilot with controlled variants
and human reasons/preferences, then compare compact model candidates on held-out
garment families and repeatability. Choose thresholds before the final test.
Evaluate factual hallucinations before tuning score scales. Design quality is
not rendering quality, number of accessories, or novelty alone.

The full protocol, candidate shortlist, hardware measurements and research
sources are maintained in EVALUATION-PLAN. A separate trained encoder/head is an
optional later optimization; do not build speculative parallel pipelines.

## Always available jury

The prototype uses one local vision-language model for all four roles. The
target keeps four fictional fashion houses while separating their scoring from
review generation; it does not require four independent models or processes.
House IDs are stable; display names are independent of the model contract.

| ID | Display name | Preference |
| --- | --- | --- |
| harmony | Maison Orvelle | Composition, colour/material balance, coherent details |
| character | Bellombre | Expressive mood, a legible idea, distinctive visual identity |
| avant_garde | Virelai | Boldness, experimentation, unexpected combinations |
| atelier | Maison Aveline | Elegance, proportions and plausibility as real clothing |

Every critic judges the whole garment using common scoring anchors, with extra
weight on their stated taste. No random bonuses, fixed personality offsets or
forced disagreement. Novelty means perceived unconventional combinations, not
proof of uniqueness across all dresses ever made. Do not penalize unavailable
editor operations or demand functionality unsupported by the game.

Each vote: score 0–10 (at most one decimal), or unknown/null when evidence is
insufficient; one or two short remark sentences. Remarks explain observed design
choices, not edits to perform. All four IDs must appear exactly once.

The application computes the arithmetic mean of all four scores. No order/client
score enters it. If any critic cannot assess, the aggregate remains unknown rather
than averaging a smaller jury or assigning zero. Application-owned verdict bands:
[0,3) weak, [3,5) uneven, [5,7) competent, [7,9) strong, [9,10] exceptional.
Spread >=3 is separately marked as divided. These are versioned presentation rules,
not empirical ground truth; real-model repeatability/calibration is still required.

## Independent context channels

1. Order compliance: the selected history dress's own saved quest record and a
   verified/key-matched sourced brief. Explicit active-order selection is only a
   development option for an active dress and cannot override historical origin.
   Known off-rack records have no commission context; unknown origin stays unknown.
   Scope is the provided summary, never the complete unseen dialogue.
2. Customer compatibility: only documented preferences with an explicit source.
   Unknown until such a profile is actually available; never inferred from a name,
   stereotype or one order.
3. Deterministic game requirements: not evaluated until actual scoring is verified.

Order/customer can have their own 0–10 score and remarks when supported; missing
context has unknown/null status. They never alter jury scores or their mean.
Sale/free-design garments therefore retain the full jury without an order.

## Enforced isolation

The first model request contains only garment/image facts and the four rubrics:
no order, brief, client profile, saved quest name or completion-history index.
A separate stateless request evaluates context only when verified context exists.
It receives no jury votes. This prevents the prompt from quietly turning four
fashion tastes back into client/task compliance. No context call is made when both
order and customer evidence are unavailable.

The application validates IDs, score ranges, missing evidence and response shape,
computes aggregation itself and preserves raw replies separately. It never asks
the model to invent an average. No advice/action-recommendation field is accepted.

## Current implementation boundary

Plugin 0.15.16 implements history capture, structured garment facts, historical
order association, direct four-vote VLM inference, strict response validation and
native notebook review cards. The current model is Qwen3-VL-4B-Instruct Q4_K_M,
rubric 1.1.0. There is no calibrated aesthetic scorer or validated model benchmark.

Existing schemas describe this prototype; the separate observation schema 0.1.0
is experimental/offline only. Production calibrated scoring contracts remain
future work. Manual Ollama remains a development dependency. The source audit
identified a single-view fallback; v0.15.11 blocks it. Offline provenance tools
and an owned CPU runtime probe now exist. Model selection, human calibration,
in-game hardware measurements and end-user packaging remain unfinished. Context
failures reported by the user still require their raw capture for diagnosis.

Current UI is history-only inline, using native assets, four crests, fixed-height
cards and optional locale.json. Old bookmarks, overlays and render-gate
instructions are historical; see RESULT-UI and FIXED-VIEWS for their evidence.
UI cosmetics are not the main development queue unless they block use.

Game/save state remains read-only. Native build/render acceptance requires
authorized game assemblies/runtime; core tests cannot substitute for it.

History context completion currently precedes dataset generation: v0.15.12 adds a
verified displayed-card path and focused goal fields. Runtime extraction passed
the commissioned/off-rack checks in [results](HISTORY-CONTEXT-RESULTS.md); extraction must remain
visible/exported when model inference fails. This does not claim recalculated game
compliance or complete customer preferences.

Native serializer integration is now being investigated. 0.15.14 collects bounded
export API metadata only; it does not invoke the UI export handlers or claim a
native JSON payload. See [NATIVE-EXPORT](NATIVE-EXPORT.md).

The 0.15.14 API report identifies the actual in-memory JSONSerializer path and
confirms that notebook ExportDress reads the active state. Core serializer branches
and config values remain unreviewed; private assembly inspection is required before
calling it on history. 0.15.15 improves metadata reference counting, not native
serialization readiness.

0.15.16 enables the reviewed in-memory serializer on a bounded detached history
state, with profile/callback/layout guards and legacy fallback. Native JSON is the
primary normalized garment-state source; full raw remains local and model evidence
stays compact. Private real-Framework smoke and net472 core build passed; installed
Unity/Mono acceptance is pending. The DLL-inspection blocker has been resolved.
