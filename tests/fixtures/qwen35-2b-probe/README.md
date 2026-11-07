# Actual local CPU probe content

Source: one canonical four-view capture, `20261104-230842-469-86722d39`.
Qwen3.5-2B Q4_K_M with F16 vision projector on llama.cpp b12110, Linux CPU.
Exact model revision/artifact hashes, request/response hashes and limitations:
[research report](../../../docs/research/2026-11-07-cpu-probe.json).

`invalid-jury-content.json` preserves the full content of the 2048-token-budget
baseline attempt (stop finish); missing schemaVersion and numeric status strings
must not be silently converted into a valid jury. No scores are accepted from it.

`observations-content.json` is the full schema-constrained experimental reply.
It passes structural/cross-reference validation. All five interpretations were
strong; no human-calibrated correctness or novelty claim follows from this.

Content is retained with a normalized final newline. Full HTTP envelopes and
image-bearing requests remain in the original development workspace, not Git;
their hashes are recorded in the report. Model weights, runtime libraries and new
game images are not included in these fixtures.
