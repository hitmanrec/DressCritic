# Garment facts card — v0.4.0

After installing and restarting, click **Inspect garment** in the upper-right
corner. The card opens in the centre with **Refresh**, **Close**, and a scrollable
body. UI text is currently English. Existing Export diagnostics and console
commands remain available, with their existing files and schemas unchanged.

The card is a timestamped snapshot. Press Refresh after changing garments, fabric,
accessories or orders. There is a two-second refresh cooldown. Rendering uses
cached text only; reflection and progress reads occur only on explicit inspect or
refresh. The card does not write diagnostic files, pause the game, alter mouse
capture, contact a model, mutate a save or change garment state. Scene/save events
invalidate its cached text. Intra-scene changes require Refresh.

## What it shows

- The active garment's selected bodice, sleeve, collar and skirt, variant indices,
  construction counts, observed fabrics and accessories.
- Separate sewing-complete and finished flags; unknown values remain unknown.
- Current accepted order, budget and raw completion requirements as **reference**.
- Unlock-list counts/completeness with an explicit distinction from owned stock,
  affordable materials, compatibility and allowed editor operations.
- Missing/incomplete data. No deterministic game score, order-compliance verdict,
  artistic brief judgment or visual assessment is calculated.

When definition prettyName is empty, the live card uses Unity's native asset name
as a display fallback. This does not change exported snapshots or turn a name into
a stable identity. The card disables rich-text interpretation of game-provided
strings. Unknown accessory details are not fabricated.

## Already finished and older garments

A finished garment works if the game exposes it as the active dress; the card does
not require making a new one. Open it in the game's normal viewer/editor and
refresh. Library/history viewers are not verified and may expose no active dress.
In that case the card reports unavailable/absent, rather than guessing from loaded
preview copies. Exactly one loaded GameManager must be identified; ambiguous state
is refused. No history-entry activation, garment loading or save mutation is
performed by the mod.

The current order is always shown in a separate reference section. No relationship
to an older garment is inferred, even if a current order exists. No old capture is
silently substituted for the live garment. The card does not yet offer an explicit
"evaluate against this order" action.

## Verification

66 offline checks cover the existing safety/snapshot behavior and card formatting
on real fixtures, including 7/14 stitched panels, 14 Burlap panels, known order
tags, complete unlocks, finished garments, unknown accessories and absent designs.
They do not compile the Unity UI or prove live layout/input behavior. Game-assembly
compilation and live IMGUI interaction remain unverified in this cloud environment.
The new inspection path uses the same known field readers and guards as exports.
It searches the loaded GameManager type only; Unity allocates its native result
array before the unique-object check. Graphs remain capped; no every-frame scans.

A short in-game check after installation is enough; no full garment-making session
is needed: inspect an existing active/finished garment, verify the counts and
order reference against the UI, refresh once after changing screens, close/reopen,
scroll on the actual resolution, and confirm diagnostic export still works.
Check pointer capture/click-through on Linux/Proton: IMGUI cannot guarantee that
game-specific input handlers beneath the overlay will ignore the same click.
Use the game's normal menu to release the pointer if needed. No guarantee about
history viewers or input integration is claimed until that check is performed.

To hide only the inspection launcher, set `[Interface] ShowInspectButton = false`
in `BepInEx/config/community.dressmakercritic.diagnostics.cfg` and restart.
`[Diagnostics] ShowExportButton` independently controls the export launcher.

## v0.4.1: explicitly choose completed-order history

The two uploaded v0.4.0 captures (131215-489-505a411d and 131655-199-cf5a2f31)
contain a current 25-panel garment, first CuttingRoom/unfinished, then Photo/finished.
They do not identify a garment selected in the game's saved viewer. No diagnostic
exceptions appear in the supplied log. OldDressView metadata contains image/view
fields, not a verified dress-state selection field. Reading activeDress was not
sufficient for the user's historical inspection workflow.

The card now offers **Saved dresses** beside **Active** and **Refresh**. It reads
only the previously established PlayerProgress.completedQuests -> QuestState.dress
instance fields through the existing guarded progress access. Records without a
dress state are skipped, and original record indices are retained. The list is
capped at 512 source records and labels this limitation if applicable. Labels use
questDefinition.prettyName, falling back to Saved garment, plus original index.
These labels are not unique persistent dress IDs. Free-design collections outside
completedQuests remain unsupported until their access path is verified.

Choose a list entry explicitly: this is independent of which thumbnail is selected
in the game's own viewer. It reads the record's dress state without loading the
garment into the game, invoking a method, selecting a game item or mutating progress.
The card labels saved versus active source. It refuses stale selection after a
save-object change or history-record replacement. Scene/save events clear selection.
The current order stays reference-only and is never assigned to the historical
record. A unique GameManager is still required for the live screen context.

After inspecting, **Export diagnostics** includes two optional files:

- inspected-garment.json: the exact cached garment last shown by the card, with its
  original capturedAtUtc and source. This can be a historical dress.
- inspection-order-reference.json: current-order reference captured alongside the
  card, with no implied association with that historical dress.

The original six files still describe live active state at export time. Therefore
inspected-garment and garment-snapshot may differ deliberately. Refresh the card
before exporting if a fresh inspection is wanted; cached inspection time can be
earlier than directory export time. Opening the saved list, failed refresh, and
scene/save changes clear the cached optional files' source; no stale inspected
object is silently exported. Existing captures are preserved unchanged.

The snapshot schema accepts the explicit historical source
PlayerProgress.completedQuests[index].dress as well as GameManager.activeDress.state.
Other snapshot fields and schemas are unchanged. 73 offline checks pass, including
saved-choice provenance, empty records, stale save/record refusal and the existing
real fixtures. History-list layout and real historical record availability are not
yet game-validated. No game DLLs are available in this environment.

Next short check: open Inspect garment -> Saved dresses, select an old entry,
confirm the saved-source label and expected component/material facts, then export.
Inspect inspected-garment.json, not the live garment-snapshot.json, for that choice.
If the list is empty/unavailable, record its displayed status rather than creating
a new garment. No repetition of the nine-stage session is needed.
