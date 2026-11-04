# v0.6.0: three independent input tracks

## 1. Saved garment state: real usage evidence

The four newly uploaded text reports confirm actual completed-history access:

| History entry | Panels | Stitched flags | Sewing complete | Finished | Accessories |
| --- | --- | --- | --- | --- | --- |
| #1 Librarian Workwear | 15 | 14/15 | yes | yes | 0 |
| #2 Flamboyant Funeral | 16 | 14/16 | yes | yes | 1 |
| #3 Fierce Funeral | 18 | 17/18 | yes | yes | 3 |
| #10 Pigeon Adoption | 25 | 24/25 | yes | yes | 10 |

These are real v0.5.0 report observations, not new synthetic runtime output.
They verify saved selection, different compositions/materials, Russian formatting
and report files. They do **not** prove that all panel stitch flags must be true
for a finished dress. No quality/compliance heuristic may impose that assumption.
Accessory counts are known, but identities/positions were depth-limited.

v0.6.0 seeds accessory definitions and position/rotation/normal fields before
recursing through dress state, using the known AccessoryItemState fields. The same
focus applies to active and manually selected saved dresses and diagnostic exports.
All existing graph limits still apply. New focused data needs a real validation
capture; no old report is overwritten or given fabricated accessory values.

## 2. Client text: bounded evidence, not a complete resolved brief

The new **Текст задания** button reads only string instance fields whose names
contain text on the existing FrontDesk.dialogueText and nameText references.
It never invokes .text or arbitrary game getters, evaluates Yarn, advances a
conversation, or changes quests. Fields are capped at 128 per declaring type,
8 inheritance levels, 32 text observations and 4096 characters per observation.

Results are saved under captures/brief-<UTC>-<id>/ as brief-evidence.json and .txt.
They include exact declaring field paths and truncation flags. Empty text reports
an actionable status rather than pretending the client brief was resolved. UI
text can be stale, inactive, an unrelated dialogue, a name or only the current
page. The candidate-text-only status deliberately prevents treating it as the
entire artistic brief or automatically attaching it to the chosen order.

Remaining work: verify which fields actually contain presented client text,
collect required pages, map localized narrative to the correct order, and then
separately interpret semantic requirements. The current input does not block
image capture or structural work, but that narrative mapping is not yet complete.

## 3. Images: independent local evidence

The **Изображение** button writes captures/image-<UTC>-<id>/image.png and
image-evidence.json. The existing pull-exports.sh copies these capture directories,
including PNGs, without a new importer. Metadata records source, dimensions, files,
UI limitations and an explicitly **unpaired** status.

Priority is the texture of exactly one active OldDressView.renderImage. Its texture
is read from Unity UI's m_Texture instance field; unsupported layouts fail safely.
This uses the already rendered viewer texture rather than a screenshot with UI.
It does not establish that the viewer is displaying the saved entry selected in
our separate list. No image/garment/order pairing is guessed. A saved selection
with no viewer texture is refused instead of substituting the current dress.

Otherwise, active garments on Mannequin/Photo can use a temporary camera copied
from GameManager.mainCamera in the built-in render pipeline. Its transform/culling
changes affect only our temporary object, not the game's camera or garment. It
excludes the UI layer, renders offscreen, restores RenderTexture.active and frees
owned resources. Screen-space overlay UI is not drawn by this camera; UI rendered
on other layers, scene backgrounds, framing and post-processing are not guaranteed
clean. SRP render pipelines are explicitly unsupported by this fallback, rather
than bypassed with an unreliable screenshot.

The initial view keeps viewport aspect, capped at 1024 px on its longest side.
If active garment child renderers supply bounds, two temporary camera angles
(+/-35 degrees around their centre) are saved as angle-left/right.png. This can
include background objects and is not proven to be an isolated garment render.
Viewer textures keep aspect and are capped at 2048 px; they provide one available
view. Native searches allocate result arrays before inspection caps. Images are
not sent anywhere and do not trigger model evaluation or automatic Git pushes.

The rendering module UnityEngine.ImageConversionModule.dll is now a required
build reference from the user's authorized game installation. No game DLL or
texture/model asset is included in this change.

## Validation and next short checks

87 offline checks pass, including nine garment/three order regressions, saved and
order selection, text field-only access/limits, image dimensions/unpaired metadata,
and focused accessory identity/coordinate preservation on synthetic layouts.
Those tests do not compile or exercise Unity rendering or verify a real TMP field
layout. The cloud has no proprietary game DLLs. A successfully rendered GPU image,
clean UI-free framing, multiview correctness and full client brief are **not** yet
claimed. Existing schema contracts remain unchanged; new evidence uses separate
1.0.0 metadata contracts.

Independent checks can use existing garments:

1. Choose a saved dress with accessories, refresh and export: check known identity
   and coordinate fields in inspected-garment.json (or explicit unknowns/limits).
2. While viewing the relevant client dialogue, press Текст задания; review whether
   brief-evidence.txt actually contains that page. No garment creation is needed.
3. Open the game's saved viewer and press Изображение; inspect the PNG. Separately,
   an active dress on Mannequin/Photo can check the camera fallback and side views.
   Check source/metadata, framing, colours and UI contamination before using images
   as ML input. Unsupported cases should show an explicit message.

These are three separate checks, not one blocking nine-stage capture sequence.
