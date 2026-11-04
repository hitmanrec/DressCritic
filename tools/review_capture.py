#!/usr/bin/env python3
"""Review a v0.7 machine capture without game DLLs. Output JSON, never reinterpret unknowns."""
import argparse
import hashlib
import json
from pathlib import Path


def review(directory):
    directory = Path(directory)
    manifest = json.loads((directory / 'manifest.json').read_text(encoding='utf-8-sig'))
    errors = []
    for item in manifest.get('files', []):
        path = directory / item['file']
        if path.resolve().parent != directory.resolve():
            errors.append({'file': item['file'], 'error': 'unsafe-path'})
            continue
        if not path.is_file():
            errors.append({'file': item['file'], 'error': 'missing'})
        elif hashlib.sha256(path.read_bytes()).hexdigest() != item['sha256']:
            errors.append({'file': item['file'], 'error': 'hash-mismatch'})
    result = {'captureId': manifest['captureId'], 'complete': manifest.get('complete', False),
              'artifactErrors': errors, 'stages': manifest.get('stages', []), 'readiness': manifest.get('readiness')}
    identity = directory / 'identity-evidence.json'
    if identity.exists():
        evidence = json.loads(identity.read_text(encoding='utf-8-sig'))
        result['exactTextureReferenceCandidates'] = [
            {'viewerId': viewer['objectId'], 'historyIndex': record['index'], 'thumbnailDirty': record.get('thumbnailDirty')}
            for viewer in evidence.get('viewers', []) if viewer.get('active') and viewer.get('textureId') is not None
            for record in evidence.get('history', []) if record.get('thumbnailTextureId') == viewer['textureId']]
        result['identityNote'] = 'Candidates only; dirty thumbnail state and uniqueness must also be checked by runtime binding.'
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('capture')
    print(json.dumps(review(parser.parse_args().capture), ensure_ascii=False, indent=2))
