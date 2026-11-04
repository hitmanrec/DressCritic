> Исторический отчёт: выводы и команды относятся к описанным версиям.
> Актуальное состояние и следующий тест: [база знаний](knowledge/README.md).

# First real image capture and v0.6.2 evidence classification

Source commit 9c43ffa adds one PNG/metadata pair, two brief evidence captures and
one saved-dress inspection report. Original files are retained unchanged.

## What is verified

- image-20261104-141507-684-23ac5b7e/image.png is a valid 2048x2048 PNG and matches
  its metadata dimensions. Its source is OldDressView.renderImage texture.
- Direct visual inspection shows the whole garment silhouette, including hem,
  neckline and sleeves, on a plain background without visible menu/button overlays.
  The frame is useful as an initial single-view visual input. It is dark in places;
  colour/material recognition and small details need care. No back view is present.
- The inspection report selects #2 Flamboyant Funeral from saved history, with
  Cinched Keyhole Blouse, Butterfly Sleeve, no collar and Mermaid Skirt. It reports
  16 panels, 4 Hunter Velvet/12 Wine Red Silk assignments and one accessory.
- Pearl Shank Button is now named and all 16 fabric identities/1 accessory identity
  are available in that report. Accessory position/rotation remain unknown. An
  accessory definition being known does not establish complete transform recovery.
- The PNG visually agrees with the broad recorded composition (dark red garment,
  contrasting waistband, short flared sleeves and a small neckline fastening), but
  that is not proof of an exact programmatic saved-record or order association.

The metadata deliberately remains unpaired. Do not silently join a PNG and report
by nearby times or visual resemblance; no stable viewer-selection mapping has
been verified. The viewer texture path is now proven to work in this instance;
the temporary camera fallback, side views and other render pipelines are not
validated by this capture. The image is not an isolated transparent garment asset,
not calibrated colour evidence, and not enough to judge hidden construction.

## What did not work

Both brief captures (141325-378-de9f96f6 and 141420-253-a4e9fe2c) contain only
FrontDesk.nameText -> TMP_Text.m_text = Rose. There is no dialogue body or client
brief. A speaker name cannot substitute for a semantic commission requirement.
The current report says no order selected, so it does not validate rendering of
the bundled English reference. No diagnostic exception is visible in the log.

v0.6.2 classifies these observations as no-dialogue-text instead of treating any
found text as dialogue. Future UI feedback explicitly states that only a name or
service text was saved. Candidate dialogue remains candidate-text-only, never a
claim of full coverage; the text-limit status takes precedence when truncated.
The originals are not relabelled on disk. The bundled sourced English reference
remains the available route for its five covered quests; no dialogue replay or
repeat garment construction is needed for this diagnosis.

## Validation and remaining work

104 offline checks pass. New real-fixture checks validate PNG signature/dimensions,
source/unpaired status and both speaker-only classifications. The schema accepts
no-dialogue-text. A synthetic dialogue candidate still cannot claim complete brief.
This does not establish live correctness of the changed UI feedback or game
compilation of v0.6.2 in the cloud (game DLLs are absent).

The next evaluator input milestone is explicit image-to-inspected-snapshot pairing
with visible source/provenance, followed by the local multimodal model connection.
Do not derive a compliance score or artistic verdict from the current independent
files, and do not require another copy of this same PNG merely to repeat checks.
