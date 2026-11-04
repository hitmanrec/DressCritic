# Progress context discovery 1.0.0

Plugin v0.3.1 adds `context-runtime.json` to button captures and `critic-editor`.
It is a fifth export, separate from garment-snapshot schema 1.0.0 and the original
diagnostic schemas. No scores or recommendations are produced.

## Proven versus unverified

Real game metadata establishes `PlayerProgress._current`, `activeCommissionIndex`,
`activeCommissions`, `unlockedPatterns`, `unlockedFabrics`, `unlockedAccessories`
and `QuestState.questDefinition`. Existing captures contain no verified instance
path from a scene object to PlayerProgress. The older diagnostic's Current getter
is not an audited read-only access route and is not called.

The new access route is deliberately conditional: locate PlayerProgress only in
Assembly-CSharp, then read only its declared static _current field if its field
type matches and its declaring type has **no static initializer**. A static field
read can cause a .cctor to run, so the presence of one blocks the read entirely.
The plugin does not assume save-load events prove the initializer has completed,
force initialization, or invoke an arbitrary property getter. No game write or
setter occurs. The guard checks metadata; this does not audit constructor code.

If the game type has an initializer, the report will say
`blocked: static initializer has not been audited`, with no progress objects.
This is a real limitation, not a verified commission integration. Do not weaken
the guard simply because a save appears loaded. A supported observable API or
separately audited access strategy is needed in that case.

Other access statuses: type-unavailable, type-not-allowed,
backing-field-unavailable, no-current-progress, available, unavailable:<exception>.
An unavailable/blocked context is not an empty commission or empty unlock set.

## Exported observations when access succeeds

Only allowlisted fields are collected; player name, save paths, money, unrelated
story flags, and full progress object graphs are not requested.

- activeCommissionIndex and activeCommissions, using exact BCL List<T> layout.
- selectedCommission and selectedCommission.questDefinition from a bounds-checked
  activeCommissions[index], without invoking currentQuestState/CurrentQuestDefinition.
- unlockedPatterns, unlockedFabrics, unlockedAccessories (raw membership evidence).

Membership is not ownership, compatibility, affordability, or proof an edit is
supported. These remain unknown. Quest objects can contain localization/Yarn IDs,
not resolved client brief text; no semantic brief requirements are fabricated.
Instance fields are prioritized before static fields in type metadata exports so
large static asset registries no longer hide the instance layout.

Context graph limits: 4096 shared nodes, depth 4, 32 array items, 64 fields per
object, 512 characters per string. The selected index is read directly after
checking _size and _items length, even if it lies beyond the exported first 32
entries. A null selected observation can also mean invalid index or unsupported
layout; inspect the index/list graph, do not infer there is no commission.
Reference IDs are scoped to this file, never the editor graph. Original graph
statuses/limits apply; inspection/initialization failures are explicit.

## Verification and next in-game check

42 offline checks pass, including all nine garment fixtures, reflection safeguards,
and synthetic access-layout tests. The synthetic fixture has a throwing static
initializer to verify that the guard refuses to execute it; it does not emulate
proprietary game APIs or establish their compatibility. The new game-specific
access has not been compiled against game DLLs or validated in-game here.

After installing v0.3.1, make **one** capture with a save loaded and an accepted
commission. Check context-runtime.json first for progressAccessStatus. If available,
compare the selected commission and known unlocked items with the UI; preserve
unknowns/truncation. If blocked, the exact limitation is now recorded and no
repeat of the nine-stage garment session is useful. Review raw quest strings
before sharing. Commission context remains exploratory until a real capture
confirms the route and the meaning of its values.

## v0.3.2: real access confirmed and normalized output

The two real captures now confirm access before/after an accepted order. See
[the paired findings](order-pair-findings.md). Exports additionally contain
`progress-snapshot.json` (six total files per button capture), schema 1.0.0.
It distinguishes no order from inaccessible progress, preserves raw deterministic
requirements, and marks semantic brief data unknown. `context-runtime.json` gains
an optional unlockedInventory section for compact GUID/name membership, capped at
512 per category; old raw graphs remain truncated at 32 and are reported as such.
Focused tag roots precede quest recursion. New compact/focused live data is not
verified by the older captures; all 53 offline checks pass.

### Actual compact/focused export now verified

Capture 20261104-125434-693-79d9b746 confirms complete compact lists and Elegant,
Daywear, Simple requirement tags. See the latest section of the paired findings.
59 checks pass, including exact structural parity between the live normalized
file and offline reprocessing, plus schema validation of the real three snapshots.
This establishes data extraction, not semantic brief interpretation or an evaluator.
