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
