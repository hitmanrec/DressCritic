# Fixed-view capture: v0.11.0 runtime verification boundary

## Problem

The shared viewer texture reflects the player's camera rotation. A three-quarter
view was interpreted as asymmetric garment construction. Calibrating jury scores
before controlling views would conflate design with camera position.

## Implemented

One bounded capture takes four orthographic 768x768 images from mannequin-local
+Z, -Z, +X, -X axes. Pose comes from mannequin orientation and combined visible
renderer bounds, never the current viewer camera pose. A sphere-based extent plus
12% margin gives the same framing size for all directions. The original preview
camera supplies its culling mask and other camera defaults, not its position.

Only a temporary mod camera, target texture and readback texture are created.
No cloning game objects/scripts, turning the live dress, changing renderer layers,
invoking game methods/getters or altering progress. Temporary resources are
cleaned up on failure too. The prior active render target is restored. A normal
Camera.Render is used on built-in; on URP, RenderPipeline.StandardRequest with
SupportsRenderRequest is used. Unity's official API/source was checked:
https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/RenderPipeline/RenderPipeline.cs
https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRenderPipeline.cs
These are public Unity APIs, not invented game members. Installed Unity version
and actual pipeline support still require compilation/runtime confirmation.

Require the existing exact history/display/mannequin/image identity chain.
Collect active enabled renderers underneath preview mannequin and display dress,
deduplicate, enforce 1–256 and finite bounds radius 0.01–100 world units. The same
state reference/preview chain is checked after capture; the caller compares
normalized garment fields again. This is not proof of renderer coverage.

Files: fixed-axis-plus-z.png, fixed-axis-minus-z.png, fixed-axis-plus-x.png,
fixed-axis-minus-x.png and fixed-views.json. Metadata records bounds, renderer
names/types/layers, culling mask, camera positions, failures and review status.
The four images are not yet labeled front/back/left/right because the actual
local-axis semantics have not been verified in the game.

## Real blocker

No proprietary game DLLs/Unity runtime are available in the cloud. We cannot
verify native compilation or render these images here. Preview garment meshes
may not all be parented under the observed mannequin/dress, copied camera masks
may include unrelated geometry, pipeline/shader defaults or bounds may be wrong,
and stored mannequin axes may not match anatomical directions. These are actual
unknowns that the new image/metadata packet resolves.

The validation build blocks inference with fixed-views-runtime-review-required.
Do not silently fall back to the old single view or send uninspected camera output
to the model. Existing snapshot collection, model metadata and critic-export
remain available. No model/config change is required.

## One useful in-game iteration

Close game, git pull --ff-only, bash install.sh. Open an existing saved dress
with recognizable front/back details. Let it settle, open Жюри and press Evaluate.
It should complete collection quickly without model generation, displaying the
review-required message. Import the packet using bash pull-exports.sh.

For the same dress, rotate the normal viewer to a very different angle, let it
settle and capture again. Send both packets. We will check all four views for
coverage, background/lighting, canonical directions and independence from viewer
rotation. No new garment or order is needed. If compilation fails, provide the
compiler output; if a render API fails the bounded packet preserves its cause.

After this verification: admit only identity/coverage-validated fixed views to
inference, attach explicit orientation labels, extend the image contract from
three to four views with unchanged byte bounds, and compare behaviour on these
existing dresses. Four views by themselves do not prove construction/comfort or
resolve obscured details; those must still remain uncertain.

## First runtime pair and v0.11.1

User captures 20261104-205543-739-ca5c6923 and 20261104-205645-784-b610e7da
both pass all artifact checks and complete four StandardRequest renders. Both
select history record 0. The returned reference images show the same green dress
at different viewer rotations. Fixed views are unusably small, near the bottom.
Metadata gives world center Y=-10.45 and extent Y=10.45 (max Y=0), framing radius
about 11 and orthographic size about 12.3. The renderer list includes 15 ordinary
panel meshes and 15 AnimationMesh entries plus mannequin body/pole. Which entry
contributed bad bounds was not recorded individually, so attributing it to a
specific animation renderer is still a hypothesis.

v0.11.1 derives geometry bounds from MeshFilter.sharedMesh.bounds (or a skinned
renderer's localBounds), skips missing/empty/degenerate meshes, transforms all
corners into mannequin-local coordinates and forms a common bound there. The
center is transformed back to world space; framing radius is computed from the
same local box transformed to world, avoiding world-AABB rotation dependence.
No vertex arrays, mesh baking or game object mutation. Renderer diagnostics now
include mesh vertex count, local geometry bounds and world transform position.
The extents in fixed-views.json are mannequin-local from this version onward.

The native pipeline/API is now confirmed on this runtime. Framing and coverage
remain unverified until another image pair. Repeat the same two-rotation test on
the existing green dress after updating/installing. Model generation stays blocked.

## First correctly framed capture / v0.11.2

Capture 20261104-210119-876-6a56b304: all artifact sizes/hashes pass. All four
images inspected: +Z is front, -Z back, +X/-X opposite profile views. Full garment
is visible; empty animation geometry is excluded (15 skipped), orthographic size
1.1315 rather than 12.3. Framing center Y=-20.0417, local extents approximately
(0.5113,0.8583,0.1499). No scene UI or unrelated dress appears in these views.
This establishes the corrected mesh framing on one undecorated green garment.
It does not establish rotation invariance or accessory capture.

v0.11.2 prepares four image evidence entries and adjacent orientation labels in
the model body, extends the three-image cap to four, retains the 4 MiB total byte
budget and hash/dimension validation. Profiles use side A/B to avoid asserting
anatomical left/right without verification. The panel photo now uses fixed +Z.
Candidate bindings remain unverified and the explicit readiness gate stays on;
no new capture is submitted to the model yet. Tests verify four actual image
parts plus orientation labels and refusal of a fifth image.

Next useful runtime check: use an existing dress with prominent accessories,
capture at two very different normal-viewer rotations. These two packets jointly
check rotation invariance and accessory coverage; repeating an undecorated dress
alone would leave the latter unknown. No model/config changes, no new garment.
Compare actual fixed images before approving inference; pipeline success flags
alone are not sufficient.

## Lighting regression: three v0.11.2 captures / v0.12.0

Captures 20261104-210534-044-69572427, 20261104-210541-604-4ddb08c3 and
20261104-210557-306-229632f0 all pass artifact hashes/sizes and select the same
20-accessory dress. Mannequin-local extents agree within about 1e-7; size is
1.135005. Front geometry/framing visually agree while illumination changes
strongly. Front/back/profile views contain decorative vines/trim. Exact coverage
of every accessory is not established just by its presence in structured data.
The source mannequin rotates relative to world lighting. Fixed camera directions
alone did not fix lighting invariance. Do not open inference on this evidence.

v0.12.0 creates a temporary additive scene and a mod-owned hierarchy. Each nonempty
static mesh renderer is copied as a MeshFilter/MeshRenderer pair using read-only
shared meshes/materials and copied global/per-material property blocks. No game
scripts or GameObject hierarchies are cloned. Relative matrix is mannequin inverse
world matrix multiplied by renderer world matrix. Corner checks reject transforms
that cannot be represented faithfully (e.g. shear/mirrored decomposition).
Skinned meshes are explicitly unsupported pending pose baking rather than copied
incorrectly. Source meshes/materials/lights/renderers are never modified.

Camera.scene selects the preview scene; layer 30 and matching light culling masks
restrict mod geometry. New camera settings do not inherit source camera exposure
or transform. Fixed white key/fill/back directional lights use intensities
0.9/0.5/0.7; shadows, light probes and reflection probes are disabled on copies.
No game RenderSettings, active-scene switches, material setters or game light
mutations. Preview root is disabled immediately on cleanup, objects destroyed and
scene unloaded asynchronously; render target is restored. This is the intended
isolation mechanism, not a claim that URP honors it correctly before testing.

The native scene/camera isolation, shader/material fidelity, accessory batches
and property blocks need runtime review. Some shaders can depend on world space
or custom runtime data. If those are not reproduced, block rather than silently
fall back or change source garment appearance. Camera mask metadata now separates
sourceCameraMask from actual cameraMask; copiedMeshes identifies snapshot coverage.

Next test: update/install with game closed; same decorated dress, two substantially
different normal-viewer rotations, import/send both captures. Check neutral
appearance, intact patterns/accessories and pixel/visual consistency of each
canonical view. No model/config change. Native APIs use existing Unity core
references, but compilation must still be checked with the installed assemblies.
