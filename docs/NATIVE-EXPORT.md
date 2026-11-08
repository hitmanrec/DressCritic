# Native garment JSON without manual export

Goal: use the game's own garment serialization as the automatic structural source
for the exact selected historical dress, supplement it with its historical order
and four views. Replacing the extraction source must not change jury independence
or invoke progress/quest/garment writes.

## Current implementation: 0.15.14

**Native in-memory serialization is not yet enabled.** The supplied `.dress` files
prove the output format, not a safe callable API. The existing garment pipeline
remains active; no handwritten surrogate is labelled as a native export.

Available `targeted-api.json` records confirm only:

- `System.Void Sketchbook.ExportDress()`
- `System.Void PhotoScene.ExportDress()`
- `PlayerProgress.MigrateSaveData(Framework.JSONObject, System.Int32)` establishes
  the JSONObject type name; the migration method is not called or probed as an
  export entrypoint.

Void UI handlers may select another garment, display a save dialog, write files
or call other game logic. Proprietary assemblies are unavailable in this workspace;
their implementations, serializer configuration, custom converters and callback
behaviour cannot be verified offline. Unity JsonUtility/third-party reflection
serialization is not substituted: it may execute callbacks/getters and may not
match native asset-reference conversion.

## Automatic discovery in the next capture

Normal Rate dress / `critic-export` now records `native-export-api.json`, before
image capture and inference. No extra button, configuration or manual `.dress`
export is required. Successful type discovery is cached for the state type during this process;
missing type metadata is retried on a later capture, never on a frame tick;
reflection is not performed per frame. This stage does not affect visual-jury
readiness if metadata is unavailable.

The probe accepts types only, never the live state as a serialization argument.
It reads:

- Root UI export method metadata and referenced methods/fields/types.
- DressState and observed `panelStates`, `accessoryStates`, `joinStates` and
  `measurements` field types, interfaces, method signatures and serialization
  attribute metadata. Attribute constructors and property getters are not invoked.
- Up to eight serialization/JSON-named methods on state/JSONObject, for discovery
  only; naming is not evidence of safety and never authorizes invocation.
- Bounded method-reference traversal within the root game/framework assemblies,
  including function-pointer delegate targets and async MoveNext metadata.
- Field read/write operation names, referenced external file/serializer calls,
  indirect write/calli operation markers, body hashes and assembly MVIDs. Runtime
  virtual dispatch, reflection and native callbacks remain unresolved possibilities.

No game static field values, string literals, raw IL, proprietary DLLs, meshes or
textures are written to the report. A reference to File.WriteAllText is metadata,
not an executed file write. This probe writes only its diagnostic report.

Limits: 64 method bodies, depth 4, 16 KiB per body, 128 KiB inspected IL total,
8192 instructions total, 128 unique operation/member references per body (occurrences counted separately), 12 type descriptions.
Type field/property descriptions retain FieldSnapshot's bounds (eight declaring
levels, up to 64 fields/properties per level). Method lists are capped at 128;
attribute lists at 16 and attribute arguments/named values at eight. Failures and
truncation are explicit. Literal attribute strings are not exported; type arguments,
enum values and booleans retain useful converter/settings identity.

The report's `nativeSerializationStatus=not-invoked-unverified` and
`isNativeGarmentPayload=false` are intentional. A bounded call graph or absence
of observed writes does not prove a method is pure. Hashes identify observed
bodies, not semantic safety of uninspected callees.

[Report schema](native-export-api.schema.json).

## Required next check

Install 0.15.14 and capture one existing commissioned history dress as usual.
Send the complete capture, especially `native-export-api.json` and `runtime.json`.
The model need not be started for these files to be produced. If inference is
running, the API metadata is still written before that request. No manual export,
new garment or progress change is needed.

Then inspect the actual helper, its argument/source selection and serializer
settings/callbacks. Only after a read-only path is verified should an adapter call
it on the independently selected historical state, obtain JSON in memory, bound
its size/collections, and check state/binding before and after. Native JSON must
stay paired with that same state; a filename or matching component shape is not
an identity guarantee. If the report is insufficient, request targeted metadata or
privately inspect the necessary assemblies without committing them; do not guess
a callable serializer signature.

Tests use trap constructors, attributes and getters, file-write references,
delegate/async bodies and an oversized synthetic method. They verify discovery
without execution and bounded output. They cannot replace the game's API report
or prove native serialization readiness.

## Received 0.15.14 API report — 2026-11-09

Capture `20261108-214839-334-cefd9aae` verified the native chain:

`Sketchbook.ExportDress` → `GameManager.activeDress.state` →
`ExportUtility.ExportDress` → native save dialog →
`FileSystem.SaveObjectToJSONFile<DressState>` →
new `Framework.JSONSerializer` / `Config`, `BeginSerialization`, `Serialize(object)`,
`EndSerialization`, `JSONValue.GetJSONString` → filesystem write.

The photo scene handler instead reads `PhotoScene._displayDress.state`. The notebook
handler therefore cannot be substituted for serializing a selected history state.
This gives a concrete explanation for the earlier mismatched .dress/capture pair,
without establishing which UI action originally produced that upload.

An in-memory adapter should bypass dialog/path/write code and pass the verified
historical state directly to a fresh serializer. However, the main `GetJSONValue`
method was cut at 64 total references before later type/converter branches. The
exact SerializeObjectReferences flag value and enum arguments are also absent;
we must not infer settings or getter/callback safety from the prefix.

0.15.15 fixes that diagnostic limitation by counting repeated operation/member
references rather than spending slots on every repeated primitive type check. The
per-body unique-reference cap is 128; global byte/instruction/method/depth limits
stay bounded. Existing 1.0.0 reports remain readable; occurrences is additive.
**Native invocation remains disabled.**

To finish without another game-only metadata iteration, privately inspect
`Dressmaker_Data/Managed/Framework.dll` and `Assembly-CSharp.dll`. Neither assembly
will be committed. This is the current implementation blocker; no new manual
.dress export is needed. [Captured evidence](research/2026-11-09-native-export-path.json).

The same capture confirms context JSON-schema transport on the user's actual
Ollama: every stage passed; order fit for Studious Motivation and unknown client
preferences remained unknown/null. This is valid abstention, not proof of aesthetic
score quality.
