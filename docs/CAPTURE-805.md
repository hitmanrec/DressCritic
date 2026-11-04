# First genuine four-house result: v0.8.5

Capture `20261104-201159-031-73a8b9c2` from the user-uploaded archive of the same
name. Every artifact hash/size verifies. Both jury and final assessment JSON
validate against the committed schemas. Local jury stage succeeds in 17312 ms:
8803 prompt tokens, 361 completion tokens, finish_reason=stop.

| House | Score |
| --- | --- |
| harmony | 6.5 |
| character | 7.0 |
| avant_garde | 6.0 |
| atelier | 5.5 |

Application-owned mean 6.25, spread 1.5, divided=false, verdict competent.
No order/customer context was selected/verified; those channels remain unknown
and the separate context request was skipped. Deterministic game scoring remains
not-evaluated. The exact final jury content is a regression fixture; it contains
no proprietary game assets and is not a score calibration target.

## Confirmed milestone

Read-only saved-history selection, image identity, garment normalization, real
local multimodal HTTP inference, four-vote validation and app aggregation now
work together in one runtime capture. The previous model/template blockers are
resolved for this example. This is one successful example, not proof of quality
or repeatability across garments or screens.

## Quality concerns

Remarks refer to red/striped panels, central zigzag, buttons, feather and brooch,
which correspond to the inspected image. However the response calls these layers,
infers intentionality and a non-functional aesthetic, and speculates about bulk
in real wear and construction suitability. The image/state do not establish
actual layering, comfort, sewing feasibility or the designer's intent. These
remarks must not be presented as verified physical facts. The rubric needs a
clearer distinction between visual plausibility and physical construction.
No unsupported prose is silently rewritten or accepted as factual ground truth.

## Next implementation priorities

1. Constrain Atelier and the common rubric to visible proportions and visual
   plausibility; state uncertainty about construction/comfort and never penalize
   game-rendered seams as proof of poor real sewing.
2. Build the final Evaluate/result flow using this existing pipeline and contract,
   keeping the diagnostic export for failures. Associate results with the captured
   garment; prevent stale results after changing the displayed design.
3. Test different existing garments and repeated evaluation for stability, then
   verified order context. Do not ask for another identical capture just to
   reconfirm this successful transport path. No model/config change needed now.

Automatic model process management and deployment, active/URP capture outside the
saved viewer, verified customer preference coverage and production performance
remain unfinished. Manual Ollama is still a development dependency.
