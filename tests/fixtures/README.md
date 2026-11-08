# Regression evidence

`runtime/` contains 13 gzip-compressed JSON files recovered byte-for-byte from
commit d322691. `runtime/sources.json` records original paths and SHA-256 hashes.
They cover the existing nine-stage garment and order/unlock tests independently
of the rotating exports/captures directory. These are diagnostic records, not
game DLLs or mesh/texture assets. Full old capture packets were not restored.

`transport.png` is a generated, solid-colour 2048x2048 PNG for transport/hash/
dimension tests; it contains no game image. `jury-string-remarks.json` is the
exact final content of the model response in capture 20261104-230842-469-86722d39.
It verifies compatibility at the model boundary while canonical schema validation
remains strict. The original HTTP envelope stays in the source capture.

The three `20261108-*` runtime fixture directories come from user-uploaded 0.15.12
captures, not d322691. Original per-artifact hashes and acceptance findings are in
`docs/research/2026-11-09-history-context.json`. These contain selected compressed
text only; missing PNGs/raw artifacts mean they are not complete capture packets.
`dress-exports/` preserves the three user state exports; `context-2b-probe/` records
real local development responses with explicit semantic limitations.
