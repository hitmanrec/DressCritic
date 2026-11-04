> Исторический отчёт: выводы и команды относятся к описанным версиям.
> Актуальное состояние и следующий тест: [база знаний](knowledge/README.md).

# v0.8.0 history/main-screen captures

Source: user-uploaded `20261104-194captures.tar.xz`, captures
`20261104-194051-125-28428ddd` and `20261104-194102-761-4a069d5d`.
Both manifests are complete and every listed hash/size verifies.

## Confirmed

- v0.8.0 loads. Ollama responds to the metadata probe and lists
  `dresscritic-vision:latest` and `qwen3-vl:4b`; config selects `dresscritic-vision`.
  No generation was attempted, so inference quality and vision support are not
  established by this metadata alone.
- History capture contains a clean, full garment preview PNG, 2048 x 2048.
- `Sketchbook._displayDress` is a Dress whose state is the exact same object as
  `completedQuests[7].dress`; the active Dress also matches history record 7.
  Record 7 has 24 panels and 11 accessories. This is independent of viewer
  `siblingIndex=5`, which is not a history index.
- Main screen has no active viewer and no active garment state. Refusing an
  assessment there is expected; hidden viewer data must not be reused.
- Both requests were blocked before generation; there is no jury result to assess.

## v0.8.1 change and remaining verification

Resolve saved state from the unique exact display-state reference rather than
requiring the shared RenderTexture to equal a saved Texture2D thumbnail.
For inference also require the live preview chain: active unique Sketchbook and
OldDressView, active display dress/preview mannequin, dress assigned to that
mannequin, preview camera target exactly equal to the visible viewer texture.
Check before and after image collection plus the existing normalized-state check.
Missing or conflicting links preserve the data/image but block inference.

The new links use observed field names; actual values of camera target and
mannequin assignment were not recorded in v0.8.0 and need runtime verification.
New `display-binding.json` and `display-binding-after-image.json` expose these
links directly instead of requiring another general reflection scan. No game
methods or game property getters are invoked. This does not prove texture
freshness during a page transition; wait until the dress is fully displayed.

## Next test

Close game, update repository, run `bash install.sh`, keep Ollama running and the
existing model configuration. Open the same saved dress in the notebook/history,
wait until it is fully visible, then make one capture and keep the page open until
completion (model generation can take up to 90 seconds). Import with
`bash pull-exports.sh`. No new garment and no main-screen capture are needed.
Expected: record 7 resolved; verified preview chain; jury request/response and
assessment if the local model returns the validated four-vote contract. If the
chain is incomplete, the two explicit binding files identify the missing link.
