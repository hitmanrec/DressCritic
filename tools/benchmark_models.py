#!/usr/bin/env python3
"""Offline, loopback-only baseline/observation experiments on verified captures."""
import argparse
import ipaddress
import json
import math
import os
from pathlib import Path
import platform
import statistics
import signal
import copy
import subprocess
import time
import urllib.request
import urllib.error
from urllib.parse import urlsplit

from benchmark_dataset import digest, inspect_packet, read_json, parse_json
from observation_protocol import INSTRUCTION, validate as validate_observations

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'tools/EvaluationHarness/EvaluationHarness.csproj'
DLL = PROJECT.parent / 'bin/Release/net8.0/EvaluationHarness.dll'


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        raise ValueError('model endpoint redirects are forbidden')


class ModelHttpError(RuntimeError):
    def __init__(self, status, body):
        super().__init__('local model HTTP status %s' % status)
        self.status, self.body = status, body


def endpoint(value):
    uri = urlsplit(value)
    if uri.scheme not in ('http', 'https') or uri.username or uri.password or uri.query or uri.fragment:
        raise ValueError('explicit credential-free loopback endpoint required')
    if uri.hostname != 'localhost':
        try:
            if not ipaddress.ip_address(uri.hostname).is_loopback:
                raise ValueError('remote endpoints forbidden')
        except (ValueError, TypeError):
            raise ValueError('remote endpoints forbidden') from None
    return value


def post(url, request, timeout):
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())
    body = json.dumps(request).encode()
    message = urllib.request.Request(endpoint(url), data=body, headers={'Content-Type': 'application/json'})
    try:
        with opener.open(message, timeout=timeout) as response:
            raw = response.read(262145)
    except urllib.error.HTTPError as error:
        raise ModelHttpError(error.code, error.read(262144)) from error
    if len(raw) > 262144:
        raise ValueError('model response exceeds 256 KiB')
    return raw


def content(raw):
    envelope = parse_json(raw)
    choice = envelope['choices'][0]
    if choice.get('finish_reason') == 'length':
        raise ValueError('truncated model completion')
    result = choice['message'].get('content')
    if not isinstance(result, str) or not result.strip() or len(result.encode()) > 65536:
        raise ValueError('no bounded final assessment')
    return result


def harness(dotnet, *args):
    result = subprocess.run([dotnet, str(DLL), *map(str, args)], capture_output=True, text=True, timeout=90)
    if result.returncode:
        raise ValueError('production harness rejected input/output: ' + result.stderr[-1500:])
    return json.loads(result.stdout)


def decoding_schema(value):
    # Portable grammar subset; the full Python validator remains authoritative.
    # llama.cpp versions differ on regex/conditionals and cannot enforce ID references.
    if isinstance(value, list):
        return [decoding_schema(item) for item in value]
    if isinstance(value, dict):
        return {key: decoding_schema(item) for key, item in value.items()
                if key not in ('$schema', '$id', '$comment', 'pattern', 'allOf', 'uniqueItems')}
    return value


def annotations(data, samples):
    known = {s['sampleId'] for s in samples}
    assigned, groups, family_ids = {}, {}, set()
    for family in data.get('families', []):
        if family['split'] not in ('development', 'holdout') or not family['familyId'] or family['familyId'] in family_ids:
            raise ValueError('invalid human family split')
        family_ids.add(family['familyId'])
        for sample in family['sampleIds']:
            if sample not in known or sample in assigned:
                raise ValueError('unknown or multiply assigned family sample')
            assigned[sample] = family['split']
    for sample in samples:
        split = assigned.get(sample['sampleId'])
        group = sample['candidateStateGroup']
        if split and group in groups and groups[group] != split:
            raise ValueError('same captured state leaks across development/holdout')
        if split:
            groups[group] = split
    pairs = data.get('pairs', [])
    for pair in pairs:
        if pair.get('criterion', 'overall') != 'overall':
            raise ValueError('only overall pairs can be compared with the jury mean')
        if pair['a'] not in known or pair['b'] not in known or pair['a'] == pair['b'] or pair['preference'] not in ('a', 'b', 'tie'):
            raise ValueError('invalid human pair')
        if not pair.get('reviewer') or not str(pair.get('reason', '')).strip():
            raise ValueError('human pair requires reviewer and reason')
    return assigned, pairs


def summarize(records, pairs, splits, tolerance):
    by = {}
    for record in records:
        key = (record['profileId'], record['sampleId'])
        if record.get('schemaValid') and record.get('average') is not None:
            by.setdefault(key, []).append(record['average'])
    stability = [{'profileId': model, 'sampleId': sample, 'runs': len(values),
                  'mean': statistics.mean(values), 'stdev': statistics.pstdev(values) if len(values) > 1 else None,
                  'range': max(values) - min(values) if len(values) > 1 else None}
                 for (model, sample), values in sorted(by.items())]
    comparisons = []
    for model in sorted({r['profileId'] for r in records}):
        for pair in pairs:
            a, b = by.get((model, pair['a'])), by.get((model, pair['b']))
            if not a or not b:
                continue
            delta = statistics.mean(a) - statistics.mean(b)
            predicted = 'tie' if abs(delta) <= tolerance else 'a' if delta > 0 else 'b'
            comparisons.append({'profileId': model, 'a': pair['a'], 'b': pair['b'], 'reviewer': pair['reviewer'],
                                'correct': predicted == pair['preference'], 'predicted': predicted,
                                'split': 'holdout' if splits.get(pair['a']) == splits.get(pair['b']) == 'holdout' else 'exploratory'})
    latency = []
    for model in sorted({r['profileId'] for r in records}):
        times = sorted(r['httpSeconds'] for r in records if r['profileId'] == model and 'httpSeconds' in r)
        if times:
            latency.append({'profileId': model, 'requests': len(times), 'p50HttpSeconds': statistics.median(times),
                            'p95HttpSeconds': times[max(0, math.ceil(len(times) * .95) - 1)]})
    return {'stability': stability, 'latency': latency, 'humanPairComparisons': comparisons,
            'humanQualityMetricsAvailable': bool(comparisons),
            'note': 'Protocol validity and score spread do not demonstrate visual correctness or calibration.'}


def run_experiment(args, supplied_profiles=None):
    source = Path(args.dataset).resolve()
    dataset = read_json(source)
    samples = dataset['samples']
    if not samples:
        raise ValueError('no eligible canonical samples; nothing to benchmark')
    labels = read_json(args.annotations) if args.annotations else {'pairs': [], 'families': []}
    splits, pairs = annotations(labels, samples)
    profiles = supplied_profiles if supplied_profiles is not None else read_json(args.profiles)['profiles']
    ids = [p['id'] for p in profiles]
    if len(ids) != len(set(ids)) or not profiles:
        raise ValueError('profile IDs must be unique')
    for profile in profiles:
        endpoint(profile['endpoint'])
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=False)  # Never overwrite a previous experiment.
    build = subprocess.run([args.dotnet, 'build', str(PROJECT), '-c', 'Release', '--nologo', '-v', 'quiet'],
                           capture_output=True, text=True, timeout=180)
    (output / 'harness-build.log').write_text(build.stdout + build.stderr)
    if build.returncode:
        raise ValueError('harness build failed; see harness-build.log')
    snapshot = copy.deepcopy(dataset)
    for sample in snapshot['samples']:
        sample['captureDirectory'] = os.path.relpath((source.parent / sample['captureDirectory']).resolve(), output)
    (output / 'dataset.json').write_text(json.dumps(snapshot, indent=2))
    (output / 'human-annotations.json').write_text(json.dumps(labels, indent=2))
    evidence = {'schemaVersion': '1.0.0', 'protocol': args.protocol, 'datasetSha256': digest(source),
                'profiles': profiles, 'repeats': args.repeats, 'pairTieTolerance': args.tie_tolerance,
                'platform': platform.platform(), 'cpuCount': os.cpu_count(),
                'cpuAffinityCount': len(os.sched_getaffinity(0)) if hasattr(os, 'sched_getaffinity') else None,
                'sourceCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
                'sourceDirty': bool(subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True)),
                'harnessSha256': digest(DLL), 'qualityValidated': False,
                'datasetSnapshotSha256': digest(output / 'dataset.json'),
                'dotnetVersion': subprocess.check_output([args.dotnet, '--version'], text=True).strip(),
                'limitations': ['Cold/warm state is unknown for an externally managed endpoint.',
                                'RAM/VRAM unavailable unless separately measured by owned-runtime session.',
                                'Human criteria/recognition review is not inferred from JSON validity.']}
    evidence['toolSourceHashes'] = {name: digest(ROOT / 'tools' / name) for name in
        ('benchmark_models.py', 'benchmark_dataset.py', 'observation_protocol.py', 'local_vision_runtime.py')}
    structured = getattr(args, 'structured_observations', False)
    evidence['structuredObservations'] = structured
    if structured:
        if args.protocol != 'observations-v1':
            raise ValueError('structured decoder option is only for the experimental observation protocol')
        evidence['responseSchemaSha256'] = digest(ROOT / 'docs/observation-response.schema.json')
    (output / 'experiment.json').write_text(json.dumps(evidence, indent=2))
    records = []
    for sample_index, sample in enumerate(samples):
        packet = (source.parent / sample['captureDirectory']).resolve()
        current = inspect_packet(packet)
        if current['inputSha256'] != sample['inputSha256']:
            raise ValueError('dataset input changed after inventory')
        for repeat in range(args.repeats):
            # Rotate execution order to avoid giving one model every first request.
            shift = (sample_index + repeat) % len(profiles)
            for profile in profiles[shift:] + profiles[:shift]:
                folder = output / ('sample-%03d-profile-%03d-repeat-%02d' % (sample_index, ids.index(profile['id']), repeat))
                folder.mkdir()
                record = {'sampleId': sample['sampleId'], 'profileId': profile['id'], 'repeat': repeat,
                          'protocol': args.protocol, 'schemaValid': False, 'qualityValidated': False, 'peakRamBytes': None, 'peakVramBytes': None}
                started = time.perf_counter()
                try:
                    if inspect_packet(packet)['inputSha256'] != sample['inputSha256']:
                        raise ValueError('dataset input changed between requests')
                    request = harness(args.dotnet, 'request', packet / 'evaluation-input.json', profile['model'])
                    if args.protocol == 'observations-v1':
                        request['messages'][0]['content'][0]['text'] = INSTRUCTION
                        if structured:
                            request['response_format'] = {'type': 'json_schema', 'json_schema': {
                                'name': 'garment_observations', 'strict': True,
                                'schema': decoding_schema(read_json(ROOT / 'docs/observation-response.schema.json'))}}
                    for key, value in profile.get('requestOverrides', {}).items():
                        if key not in ('temperature', 'top_p', 'seed', 'max_tokens', 'chat_template_kwargs'):
                            raise ValueError('unsupported override: ' + key)
                        request[key] = value
                    (folder / 'request.json').write_text(json.dumps(request))
                    record['requestSha256'] = digest(folder / 'request.json')
                    http_start = time.perf_counter()
                    raw = post(profile['endpoint'], request, args.timeout)
                    record['httpSeconds'] = time.perf_counter() - http_start
                    (folder / 'response.json').write_bytes(raw)
                    record['responseSha256'] = digest(folder / 'response.json')
                    text = content(raw)
                    content_path = folder / 'content.json'
                    content_path.write_text(text)
                    result = harness(args.dotnet, 'validate', content_path, sample['sampleId']) if args.protocol == 'baseline' else validate_observations(parse_json(text))
                    (folder / 'validated.json').write_text(json.dumps(result, indent=2))
                    record['schemaValid'] = True
                    if args.protocol == 'baseline':
                        record['average'] = result['summary']['average']
                        record['votes'] = {v['houseId']: v['score'] for v in result['jury']}
                    else:
                        record['supportedCriteria'] = sum(c['status'] == 'supported' for c in result['criteria'])
                except Exception as error:
                    if isinstance(error, ModelHttpError):
                        (folder / 'http-error-body.bin').write_bytes(error.body)
                        record['httpStatus'] = error.status
                    record['error'] = type(error).__name__ + ': ' + str(error)[:2000]
                record['elapsedSeconds'] = time.perf_counter() - started
                (folder / 'record.json').write_text(json.dumps(record, indent=2))
                records.append(record)
                print(json.dumps(record), flush=True)
    report = summarize(records, pairs, splits, args.tie_tolerance)
    report['records'] = records
    (output / 'summary.json').write_text(json.dumps(report, indent=2))
    return report


def run(args):
    if not args.runtime_config:
        return run_experiment(args)
    from local_vision_runtime import OwnedVisionRuntime
    profiles = read_json(args.profiles)['profiles']
    if len(profiles) != 1:
        raise ValueError('one owned model per run; do not load competing models into shared RAM')
    runtime_output = Path(args.output).resolve().with_name(Path(args.output).name + '-runtime')
    with OwnedVisionRuntime(args.runtime_config, runtime_output) as runtime:
        profile = dict(profiles[0], endpoint=runtime.endpoint, model=runtime.alias,
                       ownedRuntimeSession=str(runtime_output / 'session.json'),
                       runtimeEvidence=runtime.record['config'])
        return run_experiment(args, [profile])


def main():
    def terminate(signum, frame):
        raise SystemExit(128 + signum)
    signal.signal(signal.SIGTERM, terminate)
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--dataset', required=True); p.add_argument('--profiles', required=True)
    p.add_argument('--annotations'); p.add_argument('--output', required=True)
    p.add_argument('--runtime-config', help='Own one Linux CPU runtime for this experiment, then stop it')
    p.add_argument('--protocol', choices=('baseline', 'observations-v1'), default='baseline')
    p.add_argument('--structured-observations', action='store_true', help='Experimental schema-constrained decoding, never silently downgraded')
    p.add_argument('--repeats', type=int, default=3); p.add_argument('--timeout', type=float, default=300)
    p.add_argument('--tie-tolerance', type=float, default=0.25)
    p.add_argument('--dotnet', default='dotnet')
    args = p.parse_args()
    if not 1 <= args.repeats <= 20 or not 0 < args.timeout <= 1200 or not math.isfinite(args.tie_tolerance) or args.tie_tolerance < 0:
        p.error('invalid repeat/timeout/tolerance budget')
    report = run(args)
    return 0 if report['records'] and all(r['schemaValid'] for r in report['records']) else 2


if __name__ == '__main__':
    raise SystemExit(main())
