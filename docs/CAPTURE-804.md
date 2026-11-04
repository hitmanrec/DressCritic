# First complete Instruct response: v0.8.4

Upload `20261104-200828-774-d455f3c3.tar.xz`, all artifact hashes/sizes pass.
Saved-garment identity remains verified. Inference completes in 10863 ms;
finish_reason=stop, 107 completion tokens, 8746 prompt tokens. User reports
model alias ID 55ce0858c468, 5.8 GB, 8%/92% CPU/GPU, context 16384.
This establishes a complete local request/response, not useful scoring yet.

## Failure in our prompt and validation

All four votes are unknown/null. Remarks exactly repeat the prompt's placeholder
sentences. The entire content matches the shape example. v0.8.4 accepted its
syntax and produced an insufficient-evidence summary, correctly without a mean,
but this is an unusable template echo rather than an assessment.
The prompt said “Return exactly” before a concrete all-unknown example and never
explicitly explained the assessed status. This ambiguity originated in the mod.
Do not present it as the model finding the garment unevaluable.

## v0.8.5

Remove literal answer examples from both jury/context requests. Describe required
keys/types/status choices in prose; explicitly request assessed/numeric votes
when the garment is judgeable, retaining unknown for genuinely insufficient
specific evidence. No example scores, forced positive scores or forced diversity.
Explain that material entries are per-panel, not a map of visible positions;
asset names do not override visible colors.
Reject the exact observed placeholder phrases. This is a narrow regression guard,
not a claim that all low-quality remarks can be caught automatically. The real
final content is retained as a small fixture, without image or proprietary assets.
Existing schema, score ranges, unknown propagation and aggregation stay intact.
Tests cover fixture rejection and absence of the copyable answer template.

## Next test

Update/install while the game is closed. Keep the existing Instruct alias and
Ollama settings; no download or model recreation. Open the same saved dress and
capture once. Check for actual garment-specific remarks and numeric votes, or
specific legitimate evidence gaps. Repeatability, model judgement quality,
context evaluation and final UI remain unverified/pending.
