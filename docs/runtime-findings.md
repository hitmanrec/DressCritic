# Runtime evidence and remaining questions

The committed v0.1 exports are real historical game evidence, not output from the
v0.2 implementation or a test run in this cloud environment. Preserve them.

Confirmed in those exports:

- Game 0.7.0, Unity 6000.3.4f1, BepInEx 5.4.23.5, loader 0.7.8.
- Plugin initialization, console registration and PatternCutting/save-load events.
- Selected events for Princess Bodice, Cap Sleeve, No Collar and Princess Skirt,
  all variant 0 and fabric `(none)` at those event times.
- Quest identifiers were available via the old AllQuestDefinitions read.
- An empty activeCommissionSummary and unavailable GameManager.Instance at snapshot time.
  Neither proves that no commission exists. The old reflection swallowed errors.
- The reference catalog contains 178 components, tags/weights, variants, panels,
  mesh data and compatibility metadata. This is not evidence of save unlocks.
- Loader log warns about a save-event Harmony binding and dependency discovery;
  the plugin subsequently loads. Do not assume all loader events work.

Unknown: authoritative current garment holder, panel-to-fabric/color assignments,
accessory placement, current quest/brief representation/localization, unlock state,
editor operations and constraint semantics. No new undocumented game member has
been assumed. Type-name matches identify candidates only.

## v0.2 strategy

Use event roots plus loaded-scene components to discover actual runtime field
paths. Names and exact declaring types are preserved; inherited private fields and
compiler-generated auto-property backing fields are included. Lists/dictionaries
appear as backing fields; only CLR arrays are indexed. Dictionary storage can
contain unused slots; it must not be treated as a semantic list of entries.
Properties are listed as metadata only, including static properties. Static values
are not read, avoiding static initializer execution. No methods are invoked via
reflection. The old speculative singleton/property reads have been removed:
legacy JSON keys remain, but questDefinitions is empty in v0.2 and the summaries
explain the unresolved state. This deliberate safety change means v0.1 quest
identifier extraction is not claimed to work in v0.2. The historical catalog and
quest export remain available as reference inputs.

Event history is capped at 32 and cleared on scene changes. It records observations,
not a complete design; fabric changes may refer to one panel, and missed events,
save changes or callbacks may make the last root stale. Save-load events should
not be used as proof of synchronization. Run editor discovery after loading each
save. Type scans happen only on explicit command, never Update/each frame.

Caps: 512 nodes shared across each graph capture, depth 4, 64 fields per object,
32 array elements, 512 characters per string, 8 inheritance levels. Repeated
references use identity IDs rather than recursion; depth-limited roots are still
recorded. Scene traversal visits at most 128 objects, 32 components per object and
64 children per transform; pending queue is capped at 128. Unity's root and
component APIs allocate native result arrays before these limits apply. Reflection
metadata arrays are also allocated by the runtime. These commands are bounded
in traversal/output, not a hard real-time or allocation guarantee. Do not run them
repeatedly on very large scenes. Type metadata is capped at 128 candidates per
assembly and 64 fields/properties per declaring type; omitted results may exist.
Export contains private runtime strings; review locally before sharing.

## Future evaluator boundary

Keep visual quality, semantic brief compliance and deterministic style/tag scores
as separate outputs with provenance and confidence. The catalog can supply known
pattern compatibility and tag weights; it cannot establish available edits or
unlocked inventory. Unknown unlocks/constraints must remain unknown and block
recommendations requiring them. Join future observations to catalog IDs only after
runtime identity mapping is verified. Raw field graphs are exploratory evidence,
not a stable normalized garment/commission contract.

## Five real v0.2 button captures (2026-11-04)

The five captures added in commit 9224471 span UTC 11:27:18–11:37:20.
All report PatternCutting, game 0.7.0 and Unity 6000.3.4f1. Different editor
screens therefore cannot be distinguished by scene name alone in this evidence.
The log confirms the button invoked all three exporters for each capture.

However, diagnostics.json omits `runtime`, editor-runtime.json omits `objects`
and `types`, and runtime-types.json omits `types` and `objects` in **all five**
captures. These are missing fields, not empty lists. Editor notes report graph
budget exhaustion, showing traversal happened before serialization. v0.2 native
Unity serialization failed to retain the custom nested DTOs in this runtime;
the underlying native serializer/type compatibility reason is not established.
The UI's file-existence success check consequently did not prove usable contents.

v0.2.1 replaces native Unity serialization with a bounded writer restricted to
our serializable diagnostic DTOs, strings, integers, booleans and exact List<T>.
It writes null and empty collections explicitly and is tested using a separate
JSON parser for nested graphs, metadata lists, reference IDs and escaped strings.
No live game objects or arbitrary getters are traversed by the JSON writer.
The schema stays 0.2.0 because this restores intended fields rather than changing
the contract. Actual Unity/Mono execution of the replacement still needs testing.

The log independently confirms:

- Selection changes to Cinched Keyhole Blouse, Long Sleeve, Peter Pan Collar and
  Pencil Skirt, each variant 0 at selection time.
- PanelCreated events for those components with variant -1.
- FabricChanged events with fabric LinenCoolGrey, also variant -1.

Treat -1 as an observed sentinel with unknown semantics, not a valid catalog
variant index. Event multiplicity does not establish panel identity/count: events
can repeat during reconstruction. No color assignments, accessory placement,
active commission or unlocked-pattern state can be recovered from these missing
JSON payloads. Preserve the captures as regression evidence; do not fabricate
replacement runtime values from the log.

Next test: rebuild/install v0.2.1, confirm its version in the log, capture once
before and after a garment/fabric change, and check that diagnostics.json contains
`runtime.recentEvents` and graph roots, runtime-types.json contains `types`, and
editor-runtime.json contains `objects`. Check limit statuses before making another
long test sequence. The native array allocations and graph caps remain unchanged.

## Three v0.2.1 captures: nested JSON is present

Commit 0b9ead5 adds captures 20261104-115432-827-8abff4cd,
20261104-115440-451-1846c2cc and 20261104-115447-867-202aafdc.
All three contain runtime graphs, scene objects and 227 candidate type descriptions
(99 game types plus 128 loader candidates). The serialization fix is confirmed
for these actual captures. diagnostics.json is identical across them apart from
timeUtc, as is runtime-types.json. Each event history still contains only four
initial Selected events; fabricObservation is null. This does not establish that
the player's fabric changes failed: callbacks are not an authoritative current
state source. Editor JSON does differ, but does not reach activeDress.

The shared 512-node scene graph budget is exhausted while reading GameManager's
campaign/scene references after four utility components. This masks the state of
interest. GameManager **is** present as a scene component; the earlier failed
singleton lookup did not prove it was absent.

New confirmed metadata (field existence/type, not verified current values):

- GameManager.activeDress: Dress; GameManager.proposedQuest: QuestDefinition.
- GameManager.<CurrentScene>k__BackingField and <LastScene>k__BackingField hold
  GameManager.Scene values: potential sub-screen identity within PatternCutting.
- Dress.state: DressState, containing four component definitions/variant indices,
  panelStates, joinStates, accessoryStates, measurements, sewingComplete, finished.
- PanelState.fabric: Fabric; flags include hasBeenCut, placedOnMannequin,
  hasBeenStitched and grainQuality.
- PatternPanel.definition, state, garmentComponent and dress link scene panels.
- PlayerProgress.unlockedPatterns, unlockedFabrics, unlockedAccessories,
  activeCommissions and activeCommissionIndex exist as instance fields.
- QuestState.questDefinition and dress link commission state to its definition
  and dress. Metadata alone does not identify the selected commission instance.

### v0.2.2 targeted capture

Before generic scene graphs, export the real GameManager screen backing fields,
up to 32 panel fabric roots via activeDress.state.panelStates, activeDress.state
itself and proposedQuest. Paths are resolved as instance fields only; no new game
getters/static reads are introduced. Unknown/missing fields yield null. Exact BCL
List<T> storage is read using its actual _size and _items fields, capped at 32,
without custom enumeration or indexing. This runtime layout remains to be verified
in-game; unsupported layouts yield no focused list items.

The editor now collects at most 256 candidate components during bounded traversal,
prioritizes GameManager/relevant game types, uses 4096 shared graph nodes and reads
large campaign fields after activeDress. Normal event graphs retain 512 nodes.
Depth/array/string/field caps are unchanged; exhaustion and traversal caps still
mean that output can be incomplete. Reference IDs remain shared within each graph
capture. Focus paths are recorded in ValueNode.member. These are exploratory paths,
not verified normalized state adapters. PlayerProgress access/values and semantic
commission requirements remain unresolved; no singleton getter is invoked.

Next test: install v0.2.2 and compare one panel before/after fabric change, then
capture at assembly. Check editor-runtime.objects for member labels identifying
CurrentScene, activeDress.state, and panelStates[i].fabric. These should reveal
actual state even if the event history stays unchanged. Do not infer constraints
or unlocks until their live values and meaning are independently verified.

## Three v0.2.2 captures: sub-screen identity confirmed

Commit 9bfe595 contains three captures ending 121053, 121058 and 121102.
The first two CurrentScene backing values are CuttingRoom; the third is Mannequin.
LastScene is FrontDesk in all three, while the Unity scene remains PatternCutting.
GameManager.activeDress is an actual null field in the generic graph, not merely a
failed path lookup. Focused state and proposedQuest are null. No panel fabric roots
were produced, and the four initial selection events remain unchanged. Therefore
these captures do not establish which fabric was selected or which commission is
active. A null activeDress does not prove there are no other dress/panel objects.
The generic graph again exhausts its budget in unrelated scene references.

v0.2.3 adds on-demand Unity loaded-object searches for the confirmed component
types Dress and PatternPanel (including inactive loaded objects, excluding assets),
inspecting at most 8 dresses and 32 panels. It exports their actual state fields,
panel definition/fabric and Unity scene identity. Native search result allocations
are not bounded; this occurs only during explicit export. No getter/property of
a game type is invoked. Generic component fallback now reads only one field level
so it cannot recursively consume the remaining budget in unrelated UI references.
Metadata discovery also includes CuttingRoom, FrontDesk and Mannequin types.
Loaded object labels/indices are local to a capture, not stable identities, and
can include inactive or historical objects; no object is claimed to be the current
design until state/UI correspondence is verified. Static PlayerProgress fields
remain unread, avoiding possible type-initializer side effects.

Next verification: a single capture at the mannequin should first show whether
any loaded.Dress or loaded.PatternPanel objects exist and have populated state.
If no live objects appear, use the expanded type metadata and shallow scene roots
to choose the next evidence-based access path. v0.2.3 is not yet game-validated.
