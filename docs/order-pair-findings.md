> Исторический отчёт: выводы и команды относятся к описанным версиям.
> Актуальное состояние и следующий тест: [база знаний](knowledge/README.md).

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

## v0.3.2 follow-up: tags and full compact membership verified

Capture 20261104-125434-693-79d9b746 confirms the same selected Undercover Servant
order, budget 250, and the following completion requirements:

| Requirement | Comparison | Value |
| --- | --- | --- |
| QualityRequirement | AtLeast | 75 |
| TagScoreRequirement: Elegant | AtLeast | 50 |
| TagScoreRequirement: Daywear | AtLeast | 50 |
| TagScoreRequirement: Simple | AtLeast | 25 |

Tag GUIDs, in that order: e3723cd3ed274c344b83ea2199a13ef7,
f28078b1b4d790c4b85bf48f2ce7b426, 3455b7ea5b6fbdc42bd1cf5014e9f04c.
All four requirements have gated=false. These are deterministic game constraints,
not an artistic brief or measurements of visual elegance, simplicity or quality.

Compact inventory is complete for the declared lists: 72 patterns, 212 fabrics,
182 accessories. Each category has exactly that many observed members, non-null
GUIDs and no duplicate GUIDs. This is complete unlocked membership in this save,
not proof of owned stock or executable/affordable editing operations. Matching
runtime GUIDs to the static name-based catalog still needs verified mapping.

The garment snapshot explicitly says absent in this capture. Therefore no
order-compliance result can be produced from it. The earlier nine-stage garment
is a different capture/session; it must not be silently paired with this order.
Capture directory/timestamp is the context join boundary.

The actual live progress-snapshot.json is structurally identical to the offline
builder output from its accompanying context-runtime.json. All three new schema
contracts validate the corresponding real files. The suite now has 59 passing
checks, including regression coverage of complete membership and resolved tags.
This confirms v0.3.2 compact and focused output in the real game. No repeat capture
is required for these findings. Outstanding work: resolved brief/dialogue text,
actual deterministic score observations, accessory detail/visual appearance,
verified inventory-to-catalog mapping, and editor operation feasibility.
