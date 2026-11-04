# Export contract 0.2.0

All three exports use Unity JsonUtility public-field DTOs, with `schemaVersion`
`0.2.0`. Existing v0.1 files lack that field. Consumers must reject unsupported
major/minor versions and handle missing optional fields. This is an exploratory
schema, not a claim that the complete garment or commission has been resolved.

`diagnostics.json`: original timeUtc, scene, gameVersion, unityVersion,
garmentReferenceFound and three legacy summary lists, plus `runtime`:

| Field | Meaning |
| --- | --- |
| status | Explicitly exploratory completeness notice |
| lastEventUtc, lastEvent, lastVariant | Latest observation; meaningful only with timestamp |
| recentEvents | Up to 32 event records: timeUtc, kind, componentName, componentType, variant, fabricName |
| garmentObservation, fabricObservation | ValueNode graph roots; shared identity/node budget |
| commissionStatus, unlockedPatternsStatus, editorConstraintsStatus | Explicit unknown state until verified adapters exist |

`runtime-types.json` and `editor-runtime.json`: schemaVersion, timeUtc, scene,
note, objects (ValueNode array), types (type descriptions). Only one of objects or
types is populated by its command. Type descriptions contain assembly-qualified
`type`, `fields`, `properties` and a `status` explaining metadata limits. Member
strings contain declaring type and field/property type; they are evidence, not
paths that this plugin will execute.

ValueNode: `type` is an assembly-qualified runtime type; `member` identifies the
parent field or array index; `value` is a scalar encoded as an invariant string;
`children` contains field/array nodes. `id` identifies a reference object in this
export's capture, and `referenceId` points to an earlier occurrence. IDs are not
stable across captures. Default 0 means no identity. `status` is one of `null`,
`scalar`, `fields`, `reference`, `depth-limit`, `node-limit`, `field-limit`,
`collection-limit`, `string-limit`, `unsupported-array-rank`, `opaque`,
`opaque-pointer`, `unavailable`. Unavailable values report the exception type only.
Null is not an empty collection; truncated output is not complete. A destroyed
Unity object's managed fields may still exist; a graph root does not prove liveness.

A machine-readable structural schema is in `runtime-export.schema.json`.
Graph and type metadata strings are not semantic evidence of unlocked patterns,
client requirements or supported editor actions. Do not train a normalized
compliance model from unresolved candidate fields without verified mapping.
