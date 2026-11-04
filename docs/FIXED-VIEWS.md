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
