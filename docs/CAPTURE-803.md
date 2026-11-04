# v0.8.3: no-thinking option did not resolve the model variant

Upload `20261104-200023-297-2e2492e8.tar.xz`; all artifact hashes/sizes pass.
Identity stays verified. Inference returns after 73313 ms, final content empty,
finish_reason=length, completion_tokens=2048, prompt_tokens=8746 (8745 cached).
The improved validator rejects it correctly. There is still no assessment.

## Root-cause evidence and limits

The original bundled Modelfile used `FROM qwen3-vl:4b`. The official registry
manifest for this tag references `dhiltgen/qwen3-vl:4b-thinking-q4_K_M-llamacpp`.
The explicit `4b-instruct-q4_K_M` manifest references Instruct weights instead.
Verified against https://registry.ollama.ai/v2/library/qwen3-vl/manifests/4b
and https://registry.ollama.ai/v2/library/qwen3-vl/manifests/4b-instruct-q4_K_M .
The model-layer digests observed for these manifests are respectively
`sha256:02fb4392a076adea79559e7a0eb119859bf618f49cb20c966a5a068843273249`
and `sha256:3a859c4dca6883593e389a08987f04762f2a374d475cabc1538360473a72b1da`.
These identify the inspected registry manifests; local model provenance should
still be checked when rebuilding the alias because tags can change.

The runtime still returned auxiliary reasoning despite the requested
reasoning_effort=none; do not promise that option can disable this variant.
The capture alone does not reveal the server version or its internal handling.
Do not convert auxiliary reasoning into scores. Use a model trained for direct
instruction responses rather than keep enlarging the reasoning budget.

## v0.8.4

The recipe now uses `FROM qwen3-vl:4b-instruct-q4_K_M`, same local alias/context.
No backend API migration, new UI or automatic retries. Request audit now records
reasoning effort, response format and max output tokens explicitly. A repeated
truncation error no longer tells the user to enable an option already requested.

## Migration and one test

With the game closed, update/install, then from the repository:

```bash
ollama pull qwen3-vl:4b-instruct-q4_K_M
ollama stop dresscritic-vision
ollama create dresscritic-vision -f model/Modelfile
ollama show --modelfile dresscritic-vision
```

The alias remains `dresscritic-vision`; no cfg edit. Inspect the resulting FROM
(the CLI may display a local blob path rather than a tag) and compare with
`ollama show --modelfile qwen3-vl:4b-instruct-q4_K_M` if necessary.
Keep Ollama running, open the same saved dress, capture once and import. Real
Instruct generation, speed, scores and quality still need this runtime test.
No new garment or repeat main-screen export required.
