# Nine-stage garment workflow: confirmed runtime evidence

Sources: nine v0.2.3 captures uploaded in commit 04a94ed, ordered by capture UTC
and mapped to the user's nine stage descriptions. All use Unity scene
PatternCutting, game 0.7.0, Unity 6000.3.4f1 and export schema 0.2.0.
No additional game run was performed for this analysis.

| Stage | Capture UTC suffix | Internal screen | Panels cut | On mannequin | Stitched | Accessories | sewingComplete | finished |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 Main screen | 121624-528-9eb760a5 | FrontDesk | unavailable | unavailable | unavailable | unavailable | unavailable | unavailable |
| 2 Design | 121657-374-a582b085 | Sketchbook | unavailable | unavailable | unavailable | unavailable | unavailable | unavailable |
| 3 Before cutting | 121720-867-7446d013 | CuttingRoom | 0/14 | 0/14 | 0/14 | 0 | false | false |
| 4 Shop | 121813-979-aec1a71f | Store | 0/14 | 0/14 | 0/14 | 0 | false | false |
| 5 Fabric assigned, panels cut | 122000-894-6f5474fd | CuttingRoom | 14/14 | 0/14 | 0/14 | 0 | false | false |
| 6 Mannequin | 122006-727-a30a745d | Mannequin | 14/14 | 0/14 | 0/14 | 0 | false | false |
| 7 Partial sewing | 122108-178-1e6bac1a | Mannequin | 14/14 | 14/14 | 7/14 | 0 | false | false |
| 8 Sewn dress, accessories | 122413-989-5c2e58b6 | Mannequin | 14/14 | 14/14 | 14/14 | 7 | true | false |
| 9 Photo | 122434-315-f7e1a777 | Photo | 14/14 | 14/14 | 14/14 | 7 | true | true |

Unavailable is not zero. The active dress state is null in stages 1–2 and is
populated from stage 3 onward. Each state has 14 panel entries and 22 join-state
entries; join count is not an assertion that all joins were sewn. The focused
editor graph budget is available in all nine captures. Depth/field limits still
exist within individual graphs.

## Verified useful access paths

- GameManager.<CurrentScene>k__BackingField distinguishes editor sub-screens.
- GameManager.activeDress.state exposes actual design/progress in stages 3–9.
- DressState.panelStates -> PanelState fields distinguish cutting, placement,
  stitching, fabric assignment and grain-quality values.
- DressState.accessoryStates contains 7 entries with placed=true in stages 8–9.
- DressState.sewingComplete and finished represent distinct observed transitions.
  Sewing is complete before the finished flag becomes true. This sequence shows
  the flag change between mannequin and photo; it does not prove which exact game
  action sets it or that photo is the only way to finalize a dress.

All panels' fabric fields are null before cutting (stages 3–4), then all 14 refer
to Fabric.prettyName=Burlap, GUID 3906de95a21587e498bb922ba7058101 (stages 5–9).
This confirms final panel assignments, not an old-to-new fabric substitution:
the earlier selected fabric is not represented as a PanelState.fabric assignment
in these before-cut snapshots. The player's selected cutting-table material can
be separate from an already-cut panel's persisted fabric.

Observed nonzero grainQuality values range from 71.72221 to 99.98385 after cutting.
They are game-provided values; a normalized scale, thresholds, and aesthetic
meaning have not been established. Before cutting they are zero.

The three non-null component definition GUIDs remain stable in stages 3–9:

| Component | Runtime GUID | Other evidence |
| --- | --- | --- |
| Bodice | 15bc11a6dfca0254594baa860a406627 | prettyName Heart Princess Bodice; event object name Princess Heart Bodice |
| Sleeve | 9de461db2f93fff4e82e83ce70720403 | prettyName empty; event object name Butterfly Sleeve |
| Skirt | dbd4e04582eb61f478df9a5f3a980ef7 | prettyName empty; event object name Pencil Skirt |

All four variant fields are 0. collarDefinition is null; do not invent a collar
GUID from the earlier No Collar selection event. Names differ or may be empty,
so name-only joins to the static reference catalog need explicit verification.
Bodice metadata contains collarType=None, skirtType=NaturalWaist, usesSleeves=true;
skirt metadata also contains NaturalWaist. These are evidence for future
compatibility mappings, not a complete editor-operation or unlock contract.

## Important limits for the evaluator

The photo screen reports 2 loaded Dress components and 28 PatternPanel components
while authoritative activeDress.state still contains 14 panels. Loaded searches
can expose preview/copy objects. Prefer the active design state when present;
never concatenate every loaded dress/panel as one garment.

Seven accessory entries are confirmed, but their definitions and transforms are
depth-limited in this graph. Their exact identities, types, placement coordinates,
colors and material appearance have not been recovered. Counts and placed flags
do not prove seven different accessory designs. No screenshots are exported.
The future visual evaluator still needs an explicit, separate image capture path.

GameManager.proposedQuest remains null; this neither proves nor disproves an
active accepted commission. PlayerProgress.activeCommissions/activeCommissionIndex
and unlockedPatterns were identified in earlier type metadata, but their live
values and semantic requirements are still unresolved. Static progress fields
and arbitrary getters remain unread. Catalog presence is never treated as proof
of unlocks. Visual quality, semantic brief compliance and deterministic game
scores must remain separate outputs.

## Reproduce this analysis without another game session

Run from the repository root:

```bash
python3 tools/summarize_captures.py > /tmp/nine-stage-analysis.json
```

`nine-stage-captures.json` preserves the user-supplied stage mapping.
`nine-stage-analysis.json` is the generated report. The reader resolves reference
IDs within each editor graph, reads BCL List backing arrays only up to their
exported logical count, and preserves unavailable/depth-limited scalars as null.
It reports observed item counts separately from declared collection counts.
This is offline evidence extraction, not a new live-game schema or evaluator.

The existing nine captures are sufficient to confirm the basic construction
state sequence. No repeat of this full sequence is needed for these findings.
Next targeted development areas are accessory-detail roots and a verified,
read-only commission/progress access path; they should be validated separately.
