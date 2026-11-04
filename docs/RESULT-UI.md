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
