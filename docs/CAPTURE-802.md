> Исторический отчёт: выводы и команды относятся к описанным версиям.
> Актуальное состояние и следующий тест: [база знаний](knowledge/README.md).

# First local model response: v0.8.2

User upload: `20261104-195439-869-4b2977d5.tar.xz`. Manifest hash/size checks pass.
Saved garment identity and image remain verified. Model response arrives after
82578 ms; this run is not a timeout.

## Confirmed failure

OpenAI envelope has `finish_reason=length`, 2048 completion tokens,
8748 prompt tokens, and an empty final `message.content`. No valid four-vote
assessment was produced. Auxiliary reasoning is not an assessment and is never
used to recover votes or shown as jury remarks.

The mod passed empty content to its JSON reader, producing an opaque XmlReader
position error. v0.8.3 rejects length-limited and blank completions before parsing;
also explicitly calls MoveToContent for runtime reader compatibility.

The request now includes `reasoning_effort=none` to disable optional thinking
and reserve the output budget for the short final JSON. This is supported by
Ollama's OpenAI adapter, including v0.12.7:
https://github.com/ollama/ollama/blob/v0.12.7/openai/openai.go
No larger token budget, automatic retry or parsing of internal reasoning.
Tests cover empty content, truncated output (even if content looks valid),
reasoning isolation and outgoing request option.

## Placement report from user

`ollama ps`: 5.8 GB loaded, 10%/90% CPU/GPU, context 16384, llamacpp.
This indicates mixed CPU/GPU placement; percentages are not measured utilization
or timing shares. It does not establish why placement is mixed or quantify its
latency impact. Keep the current model/context for the next correctness test;
optimize placement and input size after receiving a complete validated reply.

## Next test

Update/install v0.8.3, keep Ollama/model/config unchanged, open the same historical
dress and capture once. No model download/recreation or new garment required.
The request option is sent by the mod. Wait for completion, import the capture.
A successful response should contain four votes in final content; only then can
we assess quality, repeatability and build the final result UI.
