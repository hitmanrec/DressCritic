# Three-dress runtime check — 2026-11-09

Capture timestamps: **2026-11-08 UTC**, plugin **0.15.12**. All three supplied
archives passed every manifest file size and SHA-256 check. They provide three
technically eligible four-view samples; this does not accept lighting/color fidelity
or supply human quality labels. [Machine-readable evidence](research/2026-11-09-history-context.json).

| Capture | Historical association | Displayed brief | Goals |
| --- | --- | --- | --- |
| `201120-546-6cf8f74f` | record 7, Chilly Overtime | Rose gets chilly while working overtime, and would love a warmer dress. | Quality ≥70; Professional ≥50; Warm ≥50; Cute ≤20; Romantic ≤20 |
| `201223-533-ef38beed` | record 10, Off rack dress | An off-the-rack dress. | 0; not admitted as an order |
| `201906-668-966b6e72` | record 5, Vegetable Empire | Fiona wants a cheerful gardening dress. | Quality ≥75; Cute ≥50; Playful ≥50; Workwear ≥40; minimum distinct colors 3 |

All have `record-state-verified`, `record-display-verified` and complete captured
goal fields. Exact historical definitions, TMP text and goal targets are now
confirmed for these cases. Off-rack did not acquire today's accepted commission.
Customer recipient is absent; giver identity is not a verified preference profile.
Full dialogue and client taste remain unknown.

Update: the later 0.15.14 capture `20261108-214839-334-cefd9aae` passed every stage,
including the context call on the user's Ollama. Studious Motivation fit and missing
client preferences were unknown/null. This confirms deployed format compatibility,
not semantic-score quality. See [native path evidence](research/2026-11-09-native-export-path.json).

## Context inference failed separately

Both commissioned calls returned valid JSON with the wrong structure:
`orderCompliance`/`customerCompatibility` were strings, plus an extra
`contextualJudgements` array using incorrect scope names. Strict validation rejected
them with `Unexpected assessment members`. This is not a missing-order failure.
The captured jury assessments were kept; off-rack correctly skipped context.

0.15.13 adds `response_format: json_schema` **only to the context call**, with
closed nested objects and exact scope/version constants. Missing customer evidence
limits that branch to unknown/null in the decoding schema. The independent jury
request retains its old format, evidence and instructions. Local validation still
enforces one decimal, unknown/null coupling, text bounds and evidence admission;
the portable server schema is an additional structural constraint, not proof of
truth. Unsupported servers fail explicitly; there is no silent fallback or guessed
conversion of the old replies. Raw HTTP errors/responses remain captured; `context-response-format.json` records
the exact decoding schema used.

The prompt explicitly separates visible brief fit from source eligibility: checked
provenance is not a design achievement. Client and order judgments are independent;
missing client preferences do not invalidate an otherwise verified order brief.
Physical warmth/insulation cannot be established from an image.

## Native dress exports and accessories

`.dress` files are JSON garment states: component GUID/name references, variants,
panels/fabrics/cut transforms, joins, accessory definitions/transforms/path nodes,
measurements and completion flags. These supplied exports contain no quest
definition, brief, goals, meshes or textures. A standalone renderer/generator still
needs the game's asset resolution/geometry; having this format does not establish
that safe reconstruction is implemented.

The file labelled **«по заказу экспорт.dress» is not its captured garment**:

- `.dress`: Mandarin Collar / Waistcoat Bodice / Long Cuffed Sleeve / Bias Cut
  Skirt, 25 panels, 10 accessories. Shape/variants match history record **9,
  Pigeon Adoption** in all supplied history candidate lists.
- Paired capture: record **7, Chilly Overtime**, Coat Dress components, 24 panels,
  11 accessories.

The mismatch does not prove whether a different dress was exported or a game export
selection issue occurred. Do not automatically pair these for training or transfer
one garment's order to the other. No new export is required to confirm the already
captured historical order.

The off-rack and many-accessories exports agree with records 10 and 5 on component
GUIDs/variants and counts. This supports compatibility, not exact complete state
identity or uniqueness. The decorated dress has **20** captured placed accessory
states, two copied batched accessory meshes and 26 batch entries. Front PNG
inspection shows buttons, bows, flowers and piping; full all-view decoration and
color/light fidelity are not declared accepted. Batch entries are not a one-to-one
accessory count.

Observed `ColorVarietyRequirement.minimumDistinctColors=3` is now normalized in
0.15.13 as its own nullable target field and shown as “Different colours · at least
3”. The game's definition/grouping of distinct colors and current completion value
are not reverse-engineered or recomputed. Raw fields remain available.

## Reproducibility and next check

Two real development probes used the captured Chilly Overtime images with
Qwen3.5-2B Q4_K_M/F16 projector and owned llama.cpp b12110 on CPU. Both passed the
server schema and production context validator; each owned process exited. Requests
took about 59 and 61 seconds, outside Unity on a shared cloud CPU. This establishes
format transport with this backend, not latency on the user's PC or compatibility
with their Ollama version.

The first reply gave 9.5 for verified provenance, an unacceptable justification.
After explicitly separating provenance from garment fit, the second reply cited
visible sleeves/coverage, but also made a weak collar-to-brief link and contradicted
its known status in limitations. **Semantic scoring is not accepted as reliable.**
Both complete contents and manual findings are retained, rather than presenting
schema validity as a model-quality improvement. The game's default model remains
unchanged. This is two exploratory calls on one dress, not a comparison or calibration.

Compressed original text fixtures, with original SHA-256 values in the research
report, cover positive historical extraction, AtMost targets, color goal parsing,
rejection of both malformed replies and the mismatched `.dress` pair. No proprietary
assemblies, meshes or textures are committed. Full supplied captures/images are
retained in the research workspace; the fixture folders are not complete captures.

The production C# harness now accepts `request-context INPUT MODEL` and
`validate-context INPUT CONTENT` for exact offline replay. These do not require
Unity or duplicate request/scoring logic.

After installing 0.15.13, rate **one commissioned history dress** and send its
capture. The extraction check need not be repeated for off-rack. Inspect the
context stage, nested response shape and whether remarks actually cite visible
garment evidence. Unknown/null is legitimate when the brief cannot be judged from
the supplied appearance. A valid schema alone never establishes useful critique
or calibrated scores. Native compilation and the new request with the user's
Ollama version still require the installed game/backend.
