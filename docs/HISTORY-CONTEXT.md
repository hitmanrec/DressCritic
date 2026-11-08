# Historical dress → order → brief and goals

Current priority before dataset generation/training. Implementation: **0.15.12**.
The three supplied **0.15.12** game packets now confirm history binding, displayed
brief and bounded goals for two commissions and an off-rack dress. See
[results](HISTORY-CONTEXT-RESULTS.md). The implementation/test description below
records the original 0.15.12 checkpoint; the new 0.15.13 context format needs
confirmation with the installed Ollama backend.

## What was missing

`completedQuests[i].dress` and `.questDefinition` already establish a historical
association. Ten commission GUIDs and the shared off-rack GUID were observed in
`20261104-230842-469-86722d39/identity-evidence.json`. Today's active commission
must never replace that definition. Other GUIDs/versions remain origin=unknown.

Previous integration used a five-entry sourced brief catalog, which cannot supply
every history title. Generic goal normalization kept quality/tag thresholds but
dropped parameters of other goal types. The game's displayed card was never used
as semantic brief evidence. A failed context inference is distinct from failed
extraction; model refusal is not evidence that the order is absent.

## Extraction now implemented

- `history-order.json` schema **1.1.0** retains the selected record's definition,
  giver and recipient separately, budget, goals and displayed brief evidence.
- `Sketchbook.oldQuestTitle` / `oldQuestText` come from observed game metadata.
  The reader checks their TMP `m_text` backing fields; unavailable fields are an
  explicit failure, with no getter, dialogue method or localization invocation.
- Runtime brief admission requires verified display binding before/after, the
  exact record/dress state reference, independently resolved history index, and a title equal to
  that record's definition title (outer whitespace ignored). Multiple books,
  hidden/wrong display, changed binding, stale title and empty/oversized text
  cannot create trusted brief evidence. Limits: title 512, body 4096 characters.
- `history-brief-binding-before.json` and `...-after.json` retain display checks.
  `displayedBrief.status` explains rejection. The TMP field path and real UI title
  format still require verification in the installed game. UI page numbering is
  retained as displayedIndex; its ordering is not assumed to equal completedQuests.
- Accepted text becomes `brief.status=runtime-history-verified` with a local
  `game://history/.../quest/...` source identifier. It is the displayed card in the
  current game language, not reconstructed dialogue. English game text stays
  English. Inference responses still request English.
- If runtime admission fails, the original sourced catalog rules apply. An
  unverified title match is never enough. Off-rack records provide no order brief
  to inference, even if their generic card has text.
- Each goal gets a separate bounded field graph, outside the nested giver/gift
  quest graph. At most 32 goals, 128 nodes per goal, normal FieldSnapshot depth,
  field, string and collection limits. `requirementCount` is the declared list
  size; `requirementsStatus` is complete, partial-fields, truncated or unavailable.
  Complete means bounded field capture, not understood semantics or compliance.
- `requirementFields[i]` contains focused tag/goal roots. Reference IDs are local
  to each wrapper, not shared across goals. Unknown requirement types, nested
  asset identities, enums and scalar parameters survive without invented member
  mappings. The older bounded definition graph remains in `objects`.
- `evaluation-input.order` includes count/status/raw goal fields alongside existing
  normalized `deterministicRequirements`. These additive fields are optional in
  the 2.0.0 input schema to retain old packet compatibility. Neither goals nor brief
  leak into the independent jury request. Semantic context uses only the verified
  brief; raw deterministic rules are not passed to the VLM to guess completion.
- The existing Order tab displays the extracted brief and known target values
  even if inference fails. Unknown goals show their type/scalar parameters; full
  parameter graphs remain in the export. No new diagnostic popup/button is added.

No game scoring/quest methods are invoked. Actual completion, gating state,
color-rule semantics, sale outcome and client taste are not inferred. A future
compliance reader must verify these separately; the native game target card still
provides its own displayed results.

## Original 0.15.12 acceptance test (completed with the supplied captures)

1. `git pull --ff-only`, `bash install.sh`, restart the game.
2. Open an existing **commissioned** dress in history (a quest outside the five
   bundled entries is useful). Wait until its native card is visible, then use
   Rate dress or `critic-export`. Check the Order tab for the brief and goals.
3. Open an existing **Off rack dress**, capture again. Today's active commission
   must not attach to it. No new garment or progress change is needed.
4. Send both complete capture directories (including manifest), via archive or
   the existing export importer. Model availability is not required for saving
   history-order/binding data; inference can fail after extraction is saved.

Acceptance: commissioned packet has record-state-verified, matching historical
definition/index, record-display-verified text and the expected goal count/fields;
off-rack has origin=off-rack and no admitted order context. Unknown GUID stays
unknown, not automatically off-rack. Compare extracted goals with the native card.
The supplied runtime results have now been inspected: extraction passed; semantic
inference failed on response shape. A single new commissioned capture is the next
check for the 0.15.13 schema-constrained context request.
