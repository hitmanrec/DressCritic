# Offline evaluation toolkit

Development tools for [the primary plan](EVALUATION-PLAN.md). Nothing here replaces
the current model, invents quality labels or adds a comparison UI to the game.
Requires Python 3.10+ and .NET SDK 8. No Python packages are required by the tools.
Use a checkout with the normal core fixtures; proprietary game assemblies are not
needed. Models/runtime/output artifacts belong outside the Git checkout.

## 1. Build an inventory

```bash
python3 tools/benchmark_dataset.py DressmakerCritic-Diagnostics/exports/captures \
  --output /workspace/dresscritic-research/dataset.json
```

The tool verifies manifest hashes **and sizes**, capture completion, actual
fixed-view stage success, approval metadata, all four unique axes, input labels,
binding, PNG dimensions and the image byte budget. An invalid packet is recorded
in excluded with a reason, not silently admitted. Files are not moved or changed.
Source paths are relative to the generated dataset. Each input hash is rechecked
before inference. A failed old jury call does not disqualify a valid image sample.
Technical eligibility does not accept the image's lighting or decoration coverage;
visualAcceptance starts as unreviewed and needs human inspection.

The current committed evidence provides **one** eligible image sample, not the
twelve garments described in history metadata. Additional captured views and
human annotation are required for the planned 30–50-dress pilot.

candidateStateGroup hashes selected normalized state. It only suggests duplicate
grouping; missing color/spatial data can make different garments hash alike.
familyId, split and qualityLabel stay unknown. No automatic good/bad labels,
percentile ranks or held-out accuracy are manufactured.

## 2. Record human annotation separately

Example structure (replace IDs with actual sample IDs; these are not labels for
the current capture):

```json
{
  "reviewers": ["reviewer-1"],
  "families": [
    {"familyId": "design-A", "sampleIds": ["capture-A", "capture-A-variant"], "split": "development"},
    {"familyId": "design-B", "sampleIds": ["capture-B"], "split": "holdout"}
  ],
  "pairs": [
    {"a": "capture-A", "b": "capture-A-variant", "preference": "a", "criterion": "overall",
     "reviewer": "reviewer-1", "reason": "Describe the specific observed improvement."}
  ]
}
```

Preference is a/b/tie. Reasons and reviewer are required. Duplicate assignments
and state-group leakage across development/holdout are rejected. Families need
manual review; variants of the same original must stay together. These labels,
qualityLabel, order and customer data are not injected into the jury request.
Current pair metrics use the overall jury mean, not individual criterion ratings.
Agree thresholds before holdout testing. Pair tie tolerance defaults to 0.25 and
is recorded; it is an experimental parameter, not a calibrated product rule.

## 3. Compare baseline requests

Prepare a profiles JSON using [the example](../model/benchmark-profiles.example.json).
For externally managed local servers, record exact weights/projector hashes,
runtime build, backend and quantization in runtimeEvidence. Unknown provenance
must remain unknown. Endpoint credentials, remote hosts, proxies and redirects
are disallowed. Loopback processing keeps images on this machine.

```bash
python3 tools/benchmark_models.py \
  --dataset /workspace/dresscritic-research/dataset.json \
  --profiles /path/to/profiles.json --protocol baseline --repeats 3 \
  --dotnet /workspace/.dotnet/dotnet \
  --output /workspace/dresscritic-research/run-baseline
```

Add `--annotations /path/to/human-annotations.json` when available. Baseline
requests and response validation use linked **production C# code** through
EvaluationHarness; Python does not recreate the prompt or scoring arithmetic.
Four images, view labels, material facts and isolation are identical to the
baseline mod builder. Protocol compatibility still permits bounded string remarks.

Each request, raw HTTP response, content, validated output and failure is retained.
Dataset/annotation snapshots are stored in the run directory with capture paths
rebased for replay. CLI exit status is 2 if any response fails; the partial report
and raw evidence remain available. No-sample runs fail before inference.
Experiment records include source commit/dirty flag, dataset and harness hashes,
profiles, request/response hashes, decoding overrides and repeat count. Only
temperature/top_p/seed/max_tokens/chat_template_kwargs can be overridden; changing
messages or supplied images via a profile is forbidden.

Summary includes repeated means/stdev/range, per-request votes and HTTP p50/p95
(nearest-rank p95; a tiny sample is not a latency guarantee). Stdev/range remain
null with fewer than two scored repeats. Human pair comparisons are produced only
when usable labels/results exist. Holdout is only claimed when
both samples have human-assigned holdout splits. Otherwise results are exploratory.
qualityValidated remains false: JSON validity is not perceptual correctness.

An external server's load state is unknown; first request does **not** imply cold
start. RAM/VRAM fields stay null unless separately measured. No dummy outputs count
as real model performance. Existing result directories are never overwritten.

## 4. Experimental observation contract

Use `--protocol observations-v1` to replace only the system instruction while
retaining the exact production visual evidence/images. See
[schema](observation-response.schema.json) and tools/observation_protocol.py.

At most twelve observations name their view, region and supported/uncertain status.
Five criteria (composition, material coherence, expressiveness, novelty, proportions)
reference supported observation IDs, or abstain with unknown/null. Validation checks
unique IDs, cross-references, bounded text, statuses and no extra score/advice keys.
There are **no numeric house scores or final verdicts** in this protocol. Weak/mixed/
strong criterion interpretations are also model judgments, not measured truth.

`--structured-observations` additionally requests schema-constrained decoding from
the local server. The grammar uses a portable structural subset (no conditional,
regex or uniqueness keywords); the full Python validator still enforces all
constraints and ID references. Unsupported server schemas fail explicitly, never
fall back silently. The mode/schema hash and serialized request are recorded.
This option is not allowed for baseline, which preserves the production request.

This contract makes errors inspectable; it does not prove observations are true or
solve aesthetic calibration. Human recognition/critique review is still required.
No automatic scoring-head, learned weights or score stretching is shipped here.

## 5. Owned runtime investigation

tools/local_vision_runtime.py is a **Linux CPU development probe**, not the final
Unity runtime integration. It verifies the server, GGUF and projector hashes,
checks listed runtime dependency hashes, starts one owned subprocess, waits for
health plus a unique per-process model alias and terminates its own process group
on completion/failure. It never adopts or stops an existing Ollama/server.

Use a verified, pinned llama.cpp release with image support (investigated build:
b12110), its matching CPU libraries and a compatible model/projector pair. Verify
the official release archive digest and the model repository's pinned LFS SHA-256
before executing/loading. Fill [runtime-probe.example.json](../model/runtime-probe.example.json),
including runtimeFiles for the release libraries. Files are not downloaded by the
supervisor. Keep TLS/checksum verification enabled; placeholders cannot launch.

```bash
python3 tools/benchmark_models.py \
  --dataset /workspace/dresscritic-research/dataset.json \
  --profiles /path/to/one-profile.json --runtime-config /path/to/runtime.json \
  --protocol baseline --repeats 3 --dotnet /workspace/.dotnet/dotnet \
  --output /workspace/dresscritic-research/owned-baseline
```

Owned mode requires one profile to avoid co-loading competing models into RAM.
Run candidates separately under identical conditions. Endpoint/model alias are
generated locally; the original model identity is in artifact provenance.
A sibling `owned-baseline-runtime` directory contains server.log and session.json:
startup load/readiness time, peak sampled Linux process RSS, hashes/config and
processExited. RSS is sampled every 100ms, excludes GPU allocation and may miss
brief peaks. VRAM is null on this CPU probe. OS file cache/runtime warmup can affect
startup; do not call it a cold-disk test. Artifact verification reads the model
before launch, warming file cache;
startupSeconds excludes that verification, totalStartupSeconds includes it in
newer runs. Early probe records lack the separate verification timer. The runner
sends SIGTERM to only its own session, escalates to SIGKILL after a bounded wait,
and preserves failure logs.

For one prepared request, the standalone runtime CLI accepts `--request request.json`
and writes request/response/session evidence before cleanup. Its API uses no hosted
service. CPU startup without Unity does not establish performance while gaming.
Windows/Proton process ownership, packaged installation/model discovery and the
mod's automatic backend selection remain future work.

## Verification

```bash
dotnet run --project tests/Introspection.Tests.csproj
python3 tests/test_offline_evaluation.py
python3 tests/test_capture_importer.py
```

Override DRESSCRITIC_DOTNET for Python tests if the SDK is outside
/workspace/.dotnet/dotnet. Tests include synthetic canonical captures, tampering,
failed stages, leakage, actual C# request/response validation, a mock end-to-end
benchmark and owned fake-server lifecycle/checksum failures. They are engineering
checks, not a comparison of model intelligence. Real model runs must be labelled
separately in the research findings and retain their raw artifacts.
