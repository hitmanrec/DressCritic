#!/usr/bin/env python3
"""Inventory immutable capture evidence; never infer human quality labels."""
import argparse
import hashlib
import json
import os
from pathlib import Path

AXES = ('plus-z', 'minus-z', 'plus-x', 'minus-x')
FILES = {'fixed-axis-' + axis + '.png' for axis in AXES}
LABELS = dict(zip(('fixed-axis-' + axis + '.png' for axis in AXES),
                  ('front (+Z)', 'back (-Z)', 'side A (+X)', 'side B (-X)')))
BINDINGS = {'history-display-chain-verified', 'active-state-verified', 'history-thumbnail-reference-verified'}


def digest(path):
    result = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            result.update(chunk)
    return result.hexdigest()


def read_json(path, limit=8 * 1024 * 1024):
    path = Path(path)
    if path.stat().st_size > limit:
        raise ValueError('JSON size limit: ' + path.name)
    return parse_json(path.read_text(encoding='utf-8-sig'))


def parse_json(text):
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise ValueError('duplicate JSON member: ' + key)
            result[key] = value
        return result
    def invalid(value):
        raise ValueError('non-finite JSON value: ' + value)
    return json.loads(text, object_pairs_hook=pairs, parse_constant=invalid)


def inspect_packet(directory):
    directory = Path(directory).resolve()
    manifest = read_json(directory / 'manifest.json')
    if not isinstance(manifest, dict):
        raise ValueError('manifest must be an object')
    if manifest.get('complete') is not True or (directory / '.capture-in-progress').exists():
        raise ValueError('capture incomplete')
    files = manifest.get('files', [])
    if len(files) > 512 or len({f['file'] for f in files}) != len(files):
        raise ValueError('invalid manifest file list')
    total_artifacts = 0
    for item in files:
        path = (directory / item['file']).resolve()
        if path.parent != directory or not path.is_file():
            raise ValueError('missing or unsafe artifact: ' + item['file'])
        if not 0 <= path.stat().st_size <= 16 * 1024 * 1024:
            raise ValueError('artifact size budget exceeded')
        total_artifacts += path.stat().st_size
        if total_artifacts > 64 * 1024 * 1024:
            raise ValueError('capture artifact byte budget exceeded')
        if path.stat().st_size != item['bytes'] or digest(path) != item['sha256']:
            raise ValueError('artifact size/hash mismatch: ' + item['file'])
    listed = {f['file'] for f in files}
    if not {'evaluation-input.json', 'fixed-views.json'} | FILES <= listed:
        raise ValueError('canonical artifacts not in manifest')
    stages = [s for s in manifest.get('stages', []) if s['name'] == 'fixed-view-rendering']
    if len(stages) != 1 or stages[0]['status'] != 'ok':
        raise ValueError('canonical capture stage failed or ambiguous')
    fixed = read_json(directory / 'fixed-views.json')
    if not isinstance(fixed, dict):
        raise ValueError('fixed metadata must be an object')
    if fixed.get('status') != 'accepted-fixed-views' or fixed.get('approvedForInference') is not True:
        raise ValueError('fixed views not approved')
    views = fixed.get('views', [])
    if len(views) != 4 or {v['file'] for v in views} != FILES or any(v['file'] != 'fixed-axis-' + v['axis'] + '.png' for v in views):
        raise ValueError('fixed-view axis metadata inconsistent')
    data = read_json(directory / 'evaluation-input.json')
    if not isinstance(data, dict) or not isinstance(data.get('garment'), dict):
        raise ValueError('garment unavailable')
    if data.get('schemaVersion') != '2.0.0':
        raise ValueError('unsupported evaluation-input schema')
    if data.get('captureId') != manifest['captureId'] or data['garment'].get('garmentStatus') != 'available':
        raise ValueError('capture identity or garment unavailable')
    images = data.get('images', [])
    if len(images) != 4 or {i['file'] for i in images} != FILES:
        raise ValueError('not a complete canonical four-view input')
    total = 0
    for image in images:
        if image.get('view') != LABELS[image['file']]:
            raise ValueError('input view label inconsistent with axis')
        path = directory / image['file']
        raw = path.read_bytes()
        total += len(raw)
        if image.get('binding') not in BINDINGS or digest(path) != image['sha256']:
            raise ValueError('image binding or hash invalid')
        if raw[:8] != b'\x89PNG\r\n\x1a\n' or raw[12:16] != b'IHDR' or len(raw) < 33:
            raise ValueError('invalid PNG header')
        width, height = int.from_bytes(raw[16:20], 'big'), int.from_bytes(raw[20:24], 'big')
        if (width, height) != (768, 768) or (image['width'], image['height']) != (width, height):
            raise ValueError('canonical dimensions invalid')
    if total > 4 * 1024 * 1024:
        raise ValueError('image byte budget exceeded')
    # A candidate grouping only: normalized state can omit appearance information.
    # Reviewers must assign familyId before any train/holdout split is trusted.
    fields = ('components', 'panels', 'accessories', 'sewingComplete', 'finished')
    state = {k: data['garment'].get(k) for k in fields}
    fingerprint = hashlib.sha256(json.dumps(state, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    return {
        'sampleId': manifest['captureId'], 'inputSha256': digest(directory / 'evaluation-input.json'),
        'candidateStateGroup': fingerprint, 'familyId': None, 'split': 'unassigned',
        'pluginVersion': manifest.get('pluginVersion'), 'qualityLabel': None,
        'visualAcceptance': 'unreviewed',
        'rendering': {'lighting': fixed.get('lighting'), 'width': 768, 'height': 768},
        'artifactCount': len(files), 'imageBytes': total,
    }


def inventory(root, output):
    root, output = Path(root).resolve(), Path(output).resolve()
    samples, rejected = [], []
    for packet in sorted(root.iterdir()):
        if not packet.is_dir():
            continue
        try:
            sample = inspect_packet(packet)
            sample['captureDirectory'] = os.path.relpath(packet, output.parent)
            if any(s['sampleId'] == sample['sampleId'] for s in samples):
                raise ValueError('duplicate capture ID')
            samples.append(sample)
        except (ValueError, OSError, KeyError, TypeError) as error:
            rejected.append({'directory': packet.name, 'reason': str(error)})
    return {'schemaVersion': '1.0.0', 'samples': samples, 'excluded': rejected,
            'annotationTemplate': {'reviewers': [], 'families': [], 'pairs': []},
            'limitations': ['No human quality labels inferred.', 'Technical eligibility does not establish acceptable lighting or visual coverage.', 'State fingerprints are candidate groups, not proven garment families.',
                            'Missing/unpaired historical garment snapshots are not additional image samples.']}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('captures')
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    output = Path(args.output)
    data = inventory(args.captures, output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'usableSamples': len(data['samples']), 'excluded': len(data['excluded']), 'output': str(output)}))


if __name__ == '__main__':
    main()
