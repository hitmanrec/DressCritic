# Automatic native garment state — 0.15.16

The normal Rate dress / critic-export pipeline now obtains the selected garment's
JSON **in memory using the reviewed game serializer**. No manual .dress export,
file dialog, FileSystem save helper or progress/quest operation is called. The
JSON is then saved only as a diagnostic artifact in the existing capture directory.

## Source and API

The reviewed game/framework binaries are identified by SHA-256 in
[the review record](research/2026-11-09-native-serializer-review.json). Neither DLL,
decompiled code nor private test stand-ins are committed. Only game 0.7.0 with
those exact assembly fingerprints is enabled; unknown versions/binaries fail
before invocation and retain the existing field-reader fallback.

The in-memory sequence uses a fresh Framework.JSONSerializer and Config,
SerializeObjectReferences=false, BeginSerialization with LogLevel.None (0),
Serialize on an owned copy, EndSerialization in finally, and GetJSONString(false)
on the resulting JSONObject. It reproduces native state serialization without
FileSystem's optional FILE_FORMAT wrapper. Whitespace differs from manual exports;
field values/structure are validated against the selected snapshot.

The notebook's native Sketchbook.ExportDress reads GameManager.activeDress.state;
PhotoScene.ExportDress reads its own _displayDress.state. Neither UI handler is
used. Our argument comes from the existing exact selected history record/display
binding. This avoids exporting the active dress while viewing another history item.

## Read-only boundary and bounds

- The source DressState, panel/join/accessory state and BCL lists are deeply copied
  without invoking game instance constructors. Writes target only detached copies.
  Reviewed ScriptableEnum asset leaves are retained as references; serialization
  reads their stored base GUID and the known Unity Object.name getter. The lazy
  ScriptableEnum.Index/asset-loading path is not used.
- A closed field/type contract rejects unexpected fields, derived state types,
  custom object overrides, type initializers on plain game data, custom JSON
  interfaces and unreviewed attributes before serialization. Native reflection
  traverses fields; arbitrary game properties/cost/fit methods are not called.
- The serializer's custom-handler registry is checked for reachable plain payload
  types. Custom comparers, non-runtime Type keys, unknown registry layouts and
  relevant registered handlers block invocation. Registry delegates/enumerators
  are not executed to perform this check.
- Input bounds: 8192 nodes, depth 10, 128 panels/accessories, 512 joins, 256 nodes
  per accessory path; finite float magnitude at most 1,000,000; asset names at
  most 512 characters and a 32-hex GUID. The native formatter otherwise replaces
  invalid floats; preflight rejects those instead of accepting invented zeros.
- Output is at most 2 MiB, JSON depth 16. Every field, collection count, asset
  reference and primitive/vector value is matched to the detached snapshot.
  Duplicated/extra/missing fields fail. Floating comparisons permit the native
  formatter's roundoff (relative/absolute tolerance 1e-6).
- Full state fingerprints before/after serialization and after image rendering,
  plus the existing record/state reference and display checks, protect pairing.
  A mutation during capture blocks acceptance/inference. This is additional
  validation, not permission to mutate and restore game state.

## Artifacts and model input

native-export.json is the versioned status/provenance report
([schema](native-garment-report.schema.json)). Accepted capture reports contain
status=accepted, detachedCopy=true, isNativeGarmentPayload=true, file/hash/byte
count, source fingerprint and approved assembly hashes.

native-garment.json is the native .dress-shaped state: components/variants,
panels and fabric references/cut transforms, joins, accessories including paths
and offsets, measurements and finish flags. It has no invented schemaVersion or
quest fields; the separate report versions and identifies its contract.

Accepted native state is the primary source for garment.json and evaluation-input
state facts. Friendly asset names and definition facts absent from native JSON
are enriched only on matching GUIDs from existing metadata. All native fields
remain in the raw artifact; they are not all dumped into the VLM prompt. Current
model evidence keeps at most the first 32 material/accessory entries, with explicit
coverage limitations for larger designs. Mannequin color is excluded from visual
asset-name facts. Orders/client data remain separate from the independent jury.

Unsupported profile, callbacks, graph limits, payload mismatch or output failure
produce an explicit blocked report and retain the previous reader. Successful
native captures skip the heavy export-API investigation report; failed ones keep
it for compatibility diagnostics. No UI buttons were added.

## Verification and next game check

Core tests cover three real user .dress files, detached lists/path state, asset
leaves, constructor/getter traps, callback/comparer rejection, failure cleanup,
wrong payloads, changed source, duplicate fields, limits and VLM evidence bounds.
The new bridge also builds for net472 without game DLLs.

A private smoke test executed the **actual supplied Framework.dll** on owned test
DTOs and the three garment states, with minimal Unity API stand-ins outside Git.
All three returned native JSON accepted by the full field/value matcher. This
verifies the API sequence/serialization on that setup; it does not run the actual
Assembly-CSharp game state or establish Unity engine behavior. The installed
plugin still needs runtime acceptance.

Update/install 0.15.16, restart, open one existing history dress with accessories,
and run Rate dress or critic-export as usual. No new garment or manual .dress is
needed. Send the full capture. Acceptance: native-export.json is accepted with
detachedCopy=true, native-garment.json is listed/hashed in the manifest, normalized
counts/IDs correspond to that history item, and fixed views remain paired. If
native is blocked, the reason and fallback data should be present. Model access
is not required to produce native-state artifacts; inference is a later stage.

Prior metadata-only findings and the earlier truncation are recorded in
[the path report](research/2026-11-09-native-export-path.json). Those blockers
were resolved by private inspection of the user-supplied binaries.
