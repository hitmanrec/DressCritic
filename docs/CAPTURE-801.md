> Исторический отчёт: выводы и команды относятся к описанным версиям.
> Актуальное состояние и следующий тест: [база знаний](knowledge/README.md).

# First verified history request: v0.8.1

User upload: `20261104-194716capture.tar.xz`, capture
`20261104-194716-102-b6fddc29`. All manifest artifacts verify.

## Confirmed

- History record 7 selected through exact state identity; garment data present.
- Display chain verified both before and after collection. Assigned and preview
  mannequin ID 66816; camera 47922 targets texture 36816, the viewer texture.
- Image SHA-256 equals the prior inspected clean full-dress preview.
- Readiness has no blockers; real local jury POST was attempted.
- Inference cancelled after 90033 ms. No response artifact or assessment exists.
  Context was correctly skipped, not silently assigned a score.

## Unknown

This does not establish whether inference completed on the server or why it was
slow. Cold loading, CPU fallback, VRAM contention with the game, and image/prompt
processing remain possibilities, not findings. No scores or model quality claims.

## v0.8.2

`[Inference] TimeoutSeconds` defaults to 300 and is bounded to 30–600 seconds.
The linked cancellation token is the sole inference deadline; HttpClient has no
competing timeout. Game shutdown still cancels requests. Both jury and context
request audits record the applied timeout. No automatic retries, shorter prompt
or image downsampling yet: establish a complete baseline before quality tradeoffs.

## Next test

Update/install while game is closed. Keep Ollama running. Optional warm-up:
`ollama run dresscritic-vision "Reply OK"` (text only; not a vision validation).
Open the same historical dress, wait until fully displayed and capture once.
While capture runs, execute `ollama ps` in another terminal to inspect the actual
CPU/GPU placement. Preserve that output with the capture if it fails again.
Do not change saved indices or create a new garment. Wait for capture completion
(up to five minutes for the jury request) before importing. A still-slow request
requires placement/load investigation, not repeated blind exports.
