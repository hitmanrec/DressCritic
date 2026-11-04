# Unified garment snapshot 1.0.0

Plugin v0.3.0 writes `garment-snapshot.json` whenever `critic-editor` or the export
button succeeds. The button capture directory contains four files. Existing
`diagnostics.json`, `editor-runtime.json` and `runtime-types.json` keep schema
0.2.0. This new file has its own schemaVersion 1.0.0.

The pure C# builder transforms the already collected editor graph, without further
reflection, game calls, mutations, network requests, or image capture. It reads
only the explicit `GameManager.activeDress.state` root; loaded preview copies are
never fallback garments. A missing root means unavailable; an explicit null root
means absent. Neither yields an invented empty garment.

The snapshot contains:

- Captured UTC timestamp, Unity scene, internal screen and source path.
- Components for bodice/sleeve/collar/skirt, raw definition GUID/name, variant and
  observed compatibility fields. Missing collar stays null, not a made-up asset.
- Logical panel/accessory/join-state counts, observed item arrays, and separate
  membership completeness states. `complete` means all declared members were
  exported, not that every property is known. A null count means unavailable.
- Per-panel cut/placement/stitch flags, raw grainQuality, and fabric identity.
- Accessory identity availability, placed flag, scale and position/quaternion.
- Independent sewingComplete and finished flags.
- Explicit unknown commission/unlocked inventory/color/visual appearance states.

Nullable booleans/numbers are real JSON types, not strings. Unknown booleans never
become false and unknown counts never become zero. A finite numeric value is
preserved; invalid/non-finite values remain null. Grain quality's interpretation
is not established. Coordinate w is null for Vector3 and may be present for a
quaternion; the original graph status describes availability. Asset prettyName
can be empty; do not fabricate a name or equate it with catalog identity.

Asset/coordinate `status` comes from the graph (`fields`, `null`, `depth-limit`,
`unavailable`, etc.). `fields` does not guarantee every nested scalar is available.
Graph identity references are resolved within the editor graph and are not emitted
as cross-capture IDs. Component GUIDs are asset identities; panel/accessory indices
are local to a capture and are not stable persistent object IDs.

`completeness` intentionally remains partial: normalized known construction state
is not a complete visual, semantic brief, or editor capability model. Compatibility
fields on chosen definitions do not prove unlocks or supported editing operations.
Do not generate recommendations requiring unresolved capabilities.

## Validation without game assemblies

From the repository root:

```bash
dotnet run --project tests/Introspection.Tests.csproj
# Optionally write the nine generated snapshots to a local directory:
dotnet run --project tests/Introspection.Tests.csproj -- /tmp/garment-snapshots
```

Tests use the nine committed real exports and the **same builder and JSON writer**
as the plugin. They verify absent designs, 14 panels despite photo duplicates,
shared fabric identities, 7/14 stitched panels, completion transitions, 7 placed
accessories with unknown identities, numeric/boolean JSON, truncated arrays and
dangling references. Existing reflection safety tests remain enabled.

[The example](examples/garment-snapshot.json) is generated from stage 9 of the
recorded session, not a new live-game run. [The JSON schema](garment-snapshot.schema.json)
defines the new contract. Actual game-assembly compilation and live export of
plugin v0.3.0 are still unverified in the cloud environment. No repeat of the
nine-stage session is needed for offline validation of this transformation.
