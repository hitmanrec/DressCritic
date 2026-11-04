# Before and after accepting a story order

Real v0.3.1 captures in commit 5d53199:

| Capture UTC suffix | User action | Progress access | Active order count | Selected index |
| --- | --- | --- | --- | --- |
| 124653-858-b665a5ea | No order accepted | available | 0 | -1 |
| 124817-488-cd75ebd1 | Story order accepted | available | 1 | 0 |

The initializer-free PlayerProgress._current field access succeeded in these
actual game captures. This verifies the route for this game/runtime version; the
initializer guard remains required for other versions. GameManager.proposedQuest
is null in both cases and is not used to infer accepted-order state.

The selected quest definition in the second capture has:

- prettyName: Undercover Servant.
- GUID: f205a54432cf7a4bda9bf1ab59049a6d.
- budget: 250 (raw game value; budget/affordability semantics are not evaluated).
- Four completionRequirements: QualityRequirement AtLeast 75, and three
  TagScoreRequirement entries AtLeast 50, 50, 25 respectively.
- Each observed requirement has gated=false. No gate expression is evaluated.
- requirements list count 0; this separate collection is not the completion list.

The three tag assets are depth-limited, so their names/identities cannot be
recovered from these captures. Quality and tag scores are deterministic game
requirements; they do not constitute visual quality or a resolved artistic brief.
The client dialogue/brief has not been extracted or semantically interpreted.

Both captures declare 72 unlocked patterns, 212 fabrics and 182 accessories.
Only 32 members of each are visible in the raw graph. Equal counts and equal
prefixes do not prove full membership is unchanged, and missing IDs in truncated
lists are never evidence that an item is locked. Unlock membership is not owned
stock, cost affordability, compatibility or support for a specific edit.

## Implementation follow-up: v0.3.2

`progress-snapshot.json` normalizes accepted-order identity, index/count, budget,
raw requirement observations and inventory availability, alongside the existing
five files (six total per button capture). It shares the same field graph reader
as garment snapshots; unavailable/blocked access never becomes an empty order.
Raw requirement type/comparison/threshold are retained; unknown tags remain unknown.
The existing garment schema keeps its explicit unknown context flags: consumers
join the two files by capture directory/time rather than inventing context fields.

Compact unlocked membership extraction reads only _guid and prettyName per entry
using known instance fields and exact BCL List storage. It is capped at 512 members
per category and 512 characters per identity string. Declared count, observed
members and completeness status are separate. Identical IDs are not fabricated
or names guessed. The real game's compact export is not yet verified; old captures
still correctly normalize as truncated. Raw graph limits remain unchanged.

TagScoreRequirement.tag is captured as a focused root before quest recursion, so
future captures can reveal its identity without increasing the general graph depth.
No game getter, setter, requirement evaluation method or initialization is invoked.

53 offline checks pass using the two real context captures, nine garment captures,
reflection guards, synthetic list caps and unknown/truncation behavior. Runtime
compilation and the new compact/focused exports still require game validation.
No repeat of the garment construction sequence is necessary. A subsequent ordinary
capture with an accepted order is sufficient to validate compact membership and
the focused tags when convenient.
