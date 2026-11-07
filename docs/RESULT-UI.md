> История реализации. Текущий history-only UX и следующий тест:
> [CURRENT](knowledge/CURRENT.md). Закладка, overlay и snapshot label ниже относятся
> к старым версиям и не должны восстанавливаться.

## 0.15.6 screenshot-driven native layout correction

The reported history spread places Jury on the facing leaf. Use the unchanged
native Photo Mode position (original brief fallback) to resolve the horizontal
leaf, retaining the history holder as parent so page turning is preserved.
Brief, complete historical requirements card, Jury and Photo Mode occupy separate
vertical regions. Text reserves the upper-right clip area. The exact history
wrapper is the only resized score target; shared inventory/content is excluded.

Photo Mode Image sprites, decorative Image children, font and selectable states
are borrowed read-only. Only owned visual objects are created; native scripts,
listeners and animation controllers are not cloned. Existing house crest assets
are preserved. Native rectangle snapshots restore anchors, pivot, rotation,
scale and size, plus text margins/fonts.

WIP tags receive an owned native-sprite frame and expanded rectangular clipping
allowance without row/value/slider mutations. Existing clipping and stencil state
are restored on exit. Diagnostic UI probes include tag bindings, clipping padding
and sprite names. Pure page-selection tests cover left/right and centered origins,
degenerate geometry and conflicting markers; native appearance/input still need
the in-game check in CURRENT.

# v0.9.0 evaluation result flow

The launcher now says Оценить платье. It uses the existing verified pipeline and
opens a scrollable result panel, with close/reopen during inference. All four
houses show 0–10 votes or explicit unknown, their English model remarks, an
application-owned arithmetic mean/verdict and divided flag. Order/customer
scores appear separately and never enter the mean. Unknown context and
not-evaluated game scoring remain explicit. No extra inspection/facts controls.

Result identity is deliberately a snapshot: history record number and local
capture time plus an always-visible notice. Selecting or editing a garment does
not relabel a prior result as current. New evaluation clears the old panel result.
A mod-owned transparent raycast surface shields the panel rectangle from game UI clicks.
It is hidden when the panel closes and destroyed on plugin unload.
The UI does not poll the game, reflect fields per frame, or alter any game state.
Diagnostics remain exported for failed/successful requests, including critic-export.

Rubric 1.1.0 clarifies visible plausibility versus unknown physical construction,
comfort, weight, functionality and intent. Old captures/rubric 1.0.0 remain valid
historical results; the assessment schema accepts both rubric versions.
A prompt restriction is not proof the model will obey it: next output needs review.

Validation: existing real-response, HTTP/protocol, isolation and aggregation tests;
core net472 compilation and native-source syntax check. Full native compile and
GUI interaction require authorized game assemblies and runtime, unavailable here.

## One in-game check

Update/install with the game closed; no model or cfg change. Open an existing
saved dress, click Оценить платье. Confirm readable wrapped remarks/scrolling,
mean, separate unknown context, close/reopen, and the snapshot label. Select a
different dress after evaluation and confirm the old result stays explicitly
labeled as a prior snapshot; start Evaluate again for that dress. Check clicks
and scrolling within the panel do not activate the game's controls behind it.
If inference fails, import the diagnostic capture. Upload the next completed
capture to review whether the new rubric reduced unsupported physical claims.
No new garment needs to be made.

## v0.9.1 readability correction

User screenshots confirmed v0.9.0 inherited a translucent skin with small text.
The garment behind the window obscured the assessment. The panel now draws its
own fully opaque dark surface, sets and restores GUI tint explicitly, uses 18px
body text, 21px bold house headings, a 28px mean and 26px title. Text wraps, remarks
cannot inject rich text, and the scrollable viewport fits the current screen.
Evidence limitations are collapsed by default with a clear expand button.
No change to the scoring/model contract or English remarks.

The two v0.9.0 user packets both passed all artifact hash/size checks and local
jury inference: history dress 8 mean 6.25 in 18.6 seconds, history dress 1 mean
6.625 in 12.0 seconds (display rounded to 6.63). This confirms two different
saved garments can reach the UI; it is not a calibration/repeatability benchmark.
The updated native source passes syntax verification; full Unity build and
readability/input behavior at the user's display scale need an in-game check.

## v0.10.0 salon layout

Paper-colored opaque surface with dark ink text and burgundy headings replaces
the dark panel. The Jury bookmark toggles it; Evaluate is inside the header.
On wide screens a left-page photo is the exact captured image, not a live viewer
or a screenshot of another garment. Missing photos do not prevent reading scores.
House names/mottos replace technical taste labels; tabs separate order context.
No game UI/assets are copied or modified. Existing machine captures are retained.

Verification: production core tests and native syntax check pass. A real native
build/layout test requires installed game DLLs and runtime. Update/install with
the game closed, keep the same Ollama model/config, open Жюри and evaluate one
saved dress. Check tabs, snapshot photo identity, close/reopen, scroll and smaller
window sizes. UI presentation does not resolve the known single-angle bias.

## v0.15.4 continuation of the inline notebook UI

Keeps the user's history-only UI, house crests, optional locale, native TMP styling
and Houses/Order tabs. Measures `Sketchbook.leftPage.GetWorldCorners()` in the
holder's local space; no assumption that oldDressViewHolder spans two pages.
Only the mod root is positioned from that rectangle. Unavailable/degenerate page
geometry hides the root and records a reason instead of stacking controls at zero
size. Existing reversible brief/stats compaction is retained pending geometry
review; it is not yet visually accepted. Native controls are restored on history
exit and binding changes. No guessed fallback Photo button is resized.

Scroll viewport has a transparent raycast graphic; rows use zero horizontal
sizeDelta, top anchors and TMP preferred text height. Rebuild happens on changed
geometry/content, not unconditionally per frame. A selection revision and exact
state reference prevent a late assessment from appearing on another history dress.
Request failure is visible as short inline status; no extra debug popup.

Capture adds ui-layout-original.json (before native UI adjustments), ui-layout.json
(start), ui-layout-after.json (completion). Explicit field bindings, instance IDs,
parents, rects/anchors/scale/screen corners, masks/raycast/layout sizes; no UI text.
192 nodes, depth 12, 16 component names per node; truncation explicit. This is an
on-demand probe, with the original snapshot taken once when applying the layout.
Core and syntax checks pass; full Unity build, notebook placement and input need
the installed game. See CURRENT for the consolidated next test.
