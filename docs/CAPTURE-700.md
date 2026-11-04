# Real 0.7.0 packet: findings and 0.7.1 correction

Input: capture-700.tar.xz, packet 20261104-182433-869-c1721003.
All 13 artifact hashes pass; collection completed. No image/inference was produced.
This is not an installation failure: runtime, progress, metadata and inventory read
successfully. Reduced original identity/manifest data is kept in tests/fixtures.

## Actual blockers

1. **Historical viewer binding is unresolved.** Both OldDressView instances use
   the same RenderTexture instance 36816, named DressPreview. Only one viewer is
   active. Saved thumbnail Texture2D IDs are different or unloaded. The viewer's
   siblingIndex=5 is not evidence of completedQuests[5]. No such guess is made.
2. **Collection lost an available diagnostic image.** When automatic selection
   failed, activeDress.state was null on Sketchbook. The images stage threw before
   trying the viewer texture. This was a collector bug, not evidence the viewer
   could not provide an image.
3. **No model is configured and the default local server is unreachable.** The
   HTTP error was too generic to diagnose connection versus dependency details.

Hardware: RTX 5070, reported 12227 MB VRAM; i7-14700KF; 31902 MB RAM. The game is
Unity 6000.3.4f1, **URP**, BepInEx 5.4.23.5, loader 0.7.8. Camera.Render fallback
is unsupported on URP. The already proven viewer-texture capture works independently
of that fallback, and remains the intended immediate image route.

## Changes in 0.7.1

- Always attempt an available viewer image even if target state/manager is unknown;
  preserve it as unpaired diagnostic evidence. Readiness still blocks inference.
- Collect bounded normalized saved-garment candidates (first 16 records), separately from the selected model input, so accessory/data inspection does not depend on resolving the viewer.
- Probe Sketchbook/PhotoScene/Mannequin/Dress/OldDressView instance fields and their
  exact references to completedQuests records/dress states. Preserve numeric selector
  fields and targeted method signatures, never invoke them. Both missing and shared
  references are data, not an excuse to pick the first record.
- Add bounded nested exception causes for useful local-server diagnosis.
- Retain the existing no-guess image binding, hash checks and model-output gates.

The next single viewer capture should expose the owning selection/state path and
include the PNG in the same packet. New native probe code remains unverified in
game here; production core tests reproduce the shared-RT mismatch and refusal of
false/null state matches. No full-game loop is needed.

## Concrete local model setup, independent of selection repair

Start with **qwen3-vl:4b**, rather than filling VRAM with the largest possible model
while the game runs. Official Ollama model page reports 3.3 GB, Q4_K_M weights plus
vision projector; it requires Ollama **0.12.7 or later**. This is a starting choice
for real evaluation testing, not a benchmark/quality claim on the user's GPU.

1. Install/start current Ollama from https://ollama.com/download . On Linux run
   `ollama serve` if no service is already listening; desktop app/service may already
   start it. Keep the server running.
2. In another terminal, from this repo:

```bash
ollama pull qwen3-vl:4b
ollama create dresscritic-vision -f model/Modelfile
ollama show dresscritic-vision
```

The Modelfile fixes context to 16384 because the OpenAI-compatible endpoint has no
request field for num_ctx. `ollama show` should report vision support. The model
weights/server have **not** been installed on the user's computer by this agent.

3. In BepInEx/config/community.dressmakercritic.diagnostics.cfg:

```ini
[Inference]
Model = dresscritic-vision
Endpoint = http://127.0.0.1:11434/v1/chat/completions
```

Restart the game after installing/configuring. Do not change the selection index
to 5 on the basis of viewer.siblingIndex: that mapping is unproven. The default
collector remains safe if model setup is done before image binding is solved;
it captures server/model metadata without posting an unpaired evaluation.

Official sources checked 2026-11-04:
https://ollama.com/library/qwen3-vl:4b
https://docs.ollama.com/api/openai-compatibility
