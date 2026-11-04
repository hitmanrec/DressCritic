# DressCritic product contract — jury MVP (2026-11-05)

This document fixes the product direction. It supersedes the single 0–100 visual
score and the temporary diagnostics/facts UI. No encoder-only scoring branch and
no multi-garment comparison are planned for this MVP.

## Always available jury

One local vision-language model plays four fictional fashion-house roles, not four
independent models or agents. House IDs are stable; final fictional brand names
can be chosen later without changing the contract.

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

1. Order compliance: only an explicitly selected order and a verified/key-matched
   sourced brief. Scope is the provided summary, never the complete unseen dialogue.
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

v0.9.0 adds the Evaluate/result flow to the existing real local pipeline. The
first real saved-garment evaluation succeeded in v0.8.5 (17.3 seconds, mean 6.25).
Rubric 1.1.0 limits Atelier to visual plausibility; physical construction,
comfort, functionality and intent cannot be established from the captured view.

The result panel shows a captured snapshot, its history record number/time,
four votes, app-owned mean/verdict, optional context and evidence limitations.
It never claims to follow the current live garment: changing the garment requires
a new evaluation. Starting another evaluation clears the previous display;
closing the panel does not cancel an ongoing request. No reflection is performed
by the panel or per frame. Snapshot/results remain available for diagnostics.

Manual Ollama is still required. Customer evidence, context runtime testing,
repeatability/calibration, active/URP capture outside the history viewer and
packaging/model lifecycle remain unfinished. Native result UI needs in-game
verification; no native build is claimed without the proprietary game references.

## v0.10.0 presentation

The Jury bookmark opens a mod-owned paper-styled overlay. At widths >=850px the
captured image/mean occupy the left page, houses/context tabs the right; smaller
screens use one text column. Preview is read from the same capture-local PNG
used for inference, loaded on the GUI thread once per path, and destroyed when
replaced/unloaded. It never follows a different current garment. Display names
and Russian mottos do not enter the model prompt or change scoring IDs.
Native notebook navigation integration, bespoke emblems, localized generated
remarks and fixed multi-view capture remain separate unfinished work.

## Current runtime gate: v0.11.0

Fixed-view candidate capture is implemented but awaits native compile/render and
coverage review. Inference is paused in this validation build until that evidence
exists. See [FIXED-VIEWS.md](FIXED-VIEWS.md). The prior single-angle results remain
research history and are not a calibrated final evaluator.
