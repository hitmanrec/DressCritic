import contextlib
import copy
import hashlib
import http.server
import json
import os
import shutil
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import threading
import unittest
import zlib

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
import benchmark_dataset as dataset
import benchmark_models as benchmark
import observation_protocol as observations
from local_vision_runtime import OwnedVisionRuntime


def packet(root):
    root.mkdir()
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', 768, 768, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress((b'\0' + b'\0' * 768 * 3) * 768)) + chunk(b'IEND', b'')
    images = []
    for file, label in dataset.LABELS.items():
        (root / file).write_bytes(png)
        images.append(dict(file=file, view=label, width=768, height=768, sha256=dataset.digest(root / file), binding='history-display-chain-verified'))
    data = dict(schemaVersion='2.0.0', captureId='synthetic', garment={'garmentStatus': 'available', 'panels': [], 'accessories': [], 'components': []}, images=images)
    (root / 'evaluation-input.json').write_text(json.dumps(data))
    (root / 'fixed-views.json').write_text(json.dumps(dict(status='accepted-fixed-views', approvedForInference=True,
        views=[dict(file=f, axis=f[len('fixed-axis-'):-4]) for f in dataset.FILES])))
    refresh(root)
    return root


def refresh(root):
    files = [dict(file=p.name, bytes=p.stat().st_size, sha256=dataset.digest(p)) for p in root.iterdir() if p.name != 'manifest.json']
    (root / 'manifest.json').write_text(json.dumps(dict(captureId='synthetic', complete=True, files=files,
        stages=[dict(name='fixed-view-rendering', status='ok')], pluginVersion='synthetic')))


def unknown_observations():
    return dict(schemaVersion='0.1.0', observations=[], criteria=[dict(criterionId=key, status='unknown', assessment=None, observationIds=[], rationale='Insufficient evidence.') for key in sorted(observations.CRITERIA)], limitations=[])


@contextlib.contextmanager
def server(content, redirect=False, on_request=None):
    class Handler(http.server.BaseHTTPRequestHandler):
        def log_message(self, *args):
            pass
        def do_POST(self):
            data = json.loads(self.rfile.read(int(self.headers['Content-Length'])))
            self.server.requests.append(data)
            if on_request is not None:
                on_request(len(self.server.requests))
            if redirect:
                self.send_response(302); self.send_header('Location', 'https://example.com/'); self.end_headers(); return
            body = json.dumps({'choices': [{'finish_reason': 'stop', 'message': {'content': content}}]}).encode()
            self.send_response(200); self.send_header('Content-Length', str(len(body))); self.end_headers(); self.wfile.write(body)
    instance = http.server.ThreadingHTTPServer(('127.0.0.1', 0), Handler)
    instance.requests = []
    thread = threading.Thread(target=instance.serve_forever, daemon=True); thread.start()
    try:
        yield instance, 'http://127.0.0.1:%d/v1/chat/completions' % instance.server_port
    finally:
        instance.shutdown(); instance.server_close(); thread.join()


class DatasetTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.root = Path(self.temp.name)
        self.packet = packet(self.root / 'capture')
    def tearDown(self):
        self.temp.cleanup()
    def test_valid_canonical(self):
        sample = dataset.inspect_packet(self.packet)
        self.assertIsNone(sample['qualityLabel']); self.assertIsNone(sample['familyId'])
    def test_tampered_image(self):
        path = self.packet / next(iter(dataset.FILES)); path.write_bytes(path.read_bytes() + b'X')
        with self.assertRaisesRegex(ValueError, 'mismatch'): dataset.inspect_packet(self.packet)
    def test_failed_fixed_stage_even_with_images(self):
        path = self.packet / 'manifest.json'; data = dataset.read_json(path)
        data['stages'][0]['status'] = 'failed'; path.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, 'stage'): dataset.inspect_packet(self.packet)
    def test_unsafe_manifest_path(self):
        path = self.packet / 'manifest.json'; data = dataset.read_json(path)
        data['files'][0]['file'] = '../outside'; path.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, 'unsafe'): dataset.inspect_packet(self.packet)
    def test_mislabeled_view(self):
        path = self.packet / 'evaluation-input.json'; data = dataset.read_json(path)
        data['images'][0]['view'] = 'back (-Z)'; path.write_text(json.dumps(data)); refresh(self.packet)
        with self.assertRaisesRegex(ValueError, 'label'): dataset.inspect_packet(self.packet)
    def test_incomplete_capture(self):
        (self.packet / '.capture-in-progress').touch()
        with self.assertRaisesRegex(ValueError, 'incomplete'): dataset.inspect_packet(self.packet)
    def test_missing_garment_is_excluded_not_a_crash(self):
        path = self.packet / 'evaluation-input.json'; data = dataset.read_json(path)
        data['garment'] = None; path.write_text(json.dumps(data)); refresh(self.packet)
        result = dataset.inventory(self.root, self.root / 'dataset.json')
        self.assertEqual(len(result['samples']), 0)
        self.assertEqual(len(result['excluded']), 1)
    def test_rejections_are_inventoried(self):
        (self.root / 'broken').mkdir()
        result = dataset.inventory(self.root, self.root / 'dataset.json')
        self.assertEqual((len(result['samples']), len(result['excluded'])), (1, 1))


class ProtocolTests(unittest.TestCase):
    def test_real_structured_observation_response(self):
        data = dataset.read_json(ROOT / 'tests/fixtures/qwen35-2b-probe/observations-content.json')
        observations.validate(data)
        self.assertEqual(len(data['criteria']), 5)
        self.assertNotIn('jury', data)
    def test_duplicate_json_members_rejected(self):
        with self.assertRaises(ValueError): dataset.parse_json('{"status":"unknown","status":"supported"}')
    def test_supported_criterion_needs_supported_observation(self):
        data = unknown_observations()
        data['observations'] = [dict(id='o1', description='Red panel beside pale trim.', view='front (+Z)', region='bodice', certainty='supported')]
        data['criteria'][0].update(status='supported', assessment='mixed', observationIds=['o1'])
        observations.validate(data)
        data['observations'][0]['certainty'] = 'uncertain'
        with self.assertRaises(ValueError): observations.validate(data)
    def test_abstention_is_valid_not_a_score(self):
        result = observations.validate(unknown_observations())
        self.assertTrue(all(c['assessment'] is None for c in result['criteria']))
    def test_unsupported_evidence_reference(self):
        data = unknown_observations(); row = data['criteria'][0]
        row.update(status='supported', assessment='strong', observationIds=['missing'])
        with self.assertRaises(ValueError): observations.validate(data)
    def test_scores_and_votes_forbidden(self):
        data = unknown_observations(); data['score'] = 6.5
        with self.assertRaises(ValueError): observations.validate(data)
    def test_remote_and_credential_endpoints(self):
        for url in ['https://example.com/v1/chat/completions', 'http://user:secret@127.0.0.1', 'http://localhost.attacker.test', 'http://127.0.0.1/?secret=x']:
            with self.assertRaises(ValueError): benchmark.endpoint(url)
    def test_redirects_not_followed(self):
        with server('{}', redirect=True) as (http, url):
            with self.assertRaises(ValueError): benchmark.post(url, {}, 3)
            self.assertEqual(len(http.requests), 1)
    def test_truncated_completion(self):
        with self.assertRaises(ValueError): benchmark.content(json.dumps({'choices': [{'finish_reason': 'length', 'message': {'content': '{}'}}]}))
    def test_family_leak_rejected(self):
        samples = [dict(sampleId='a', candidateStateGroup='same'), dict(sampleId='b', candidateStateGroup='same')]
        labels = dict(families=[dict(familyId='a', sampleIds=['a'], split='development'), dict(familyId='b', sampleIds=['b'], split='holdout')], pairs=[])
        with self.assertRaisesRegex(ValueError, 'leaks'): benchmark.annotations(labels, samples)
    def test_no_fake_human_metrics(self):
        report = benchmark.summarize([], [], {}, .25)
        self.assertFalse(report['humanQualityMetricsAvailable'])
    def test_real_pair_and_stability(self):
        records = [dict(profileId='model', sampleId=s, average=v, schemaValid=True) for s, v in [('a', 8), ('a', 7), ('b', 3), ('b', 4)]]
        report = benchmark.summarize(records, [dict(a='a', b='b', preference='a', reviewer='human')], {}, .25)
        self.assertTrue(report['humanPairComparisons'][0]['correct'])
        self.assertEqual(report['stability'][0]['stdev'], .5)


class HarnessIntegration(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.dotnet = os.environ.get('DRESSCRITIC_DOTNET') or shutil.which('dotnet') or '/workspace/.dotnet/dotnet'
        subprocess.run([cls.dotnet, 'build', str(benchmark.PROJECT), '-c', 'Release', '--nologo', '-v', 'quiet'], check=True, capture_output=True)
    def test_request_and_response_use_production_contract(self):
        with tempfile.TemporaryDirectory() as temp:
            root = packet(Path(temp) / 'capture')
            data = dataset.read_json(root / 'evaluation-input.json')
            data['order'] = dict(association='SECRET_HISTORY', definition={'prettyName': 'SECRET_CLIENT'}, brief={'summary': 'SECRET_BRIEF'})
            (root / 'evaluation-input.json').write_text(json.dumps(data))
            request = benchmark.harness(self.dotnet, 'request', root / 'evaluation-input.json', 'fixture')
            self.assertEqual(sum(p['type'] == 'image_url' for p in request['messages'][1]['content']), 4)
            self.assertNotIn('SECRET_', json.dumps(request))
            fixture = ROOT / 'tests/fixtures/jury-string-remarks.json'
            result = benchmark.harness(self.dotnet, 'validate', fixture, 'fixture')
            self.assertEqual(result['summary']['average'], 6.625)
    def test_complete_mock_experiment_and_artifacts(self):
        from types import SimpleNamespace
        with tempfile.TemporaryDirectory() as temp, server((ROOT / 'tests/fixtures/jury-string-remarks.json').read_text()) as (http, url):
            root = Path(temp); packet(root / 'capture')
            path = root / 'dataset.json'; path.write_text(json.dumps(dataset.inventory(root, path)))
            profiles = root / 'profiles.json'; profiles.write_text(json.dumps({'profiles': [dict(id='fixture', model='fixture', endpoint=url)]}))
            args = SimpleNamespace(dataset=str(path), profiles=str(profiles), annotations=None, output=str(root / 'run'),
                dotnet=self.dotnet, repeats=2, protocol='baseline', timeout=3, tie_tolerance=.25, runtime_config=None)
            benchmark.run(args)
            summary = dataset.read_json(root / 'run/summary.json')
            self.assertEqual(len(http.requests), 2)
            self.assertTrue(all(r['schemaValid'] for r in summary['records']))
            self.assertFalse(summary['humanQualityMetricsAvailable'])
            self.assertTrue((root / 'run/sample-000-profile-000-repeat-00/response.json').exists())
            with self.assertRaises(FileExistsError): benchmark.run(args)
    def test_invalid_reply_is_retained_and_cli_fails(self):
        with tempfile.TemporaryDirectory() as temp, server('{}') as (http, url):
            root = Path(temp); packet(root / 'capture')
            path = root / 'dataset.json'; path.write_text(json.dumps(dataset.inventory(root, path)))
            profiles = root / 'profiles.json'; profiles.write_text(json.dumps({'profiles': [dict(id='fixture', model='fixture', endpoint=url)]}))
            result = subprocess.run([sys.executable, str(ROOT / 'tools/benchmark_models.py'), '--dataset', str(path),
                '--profiles', str(profiles), '--output', str(root / 'run'), '--repeats', '1', '--dotnet', self.dotnet], capture_output=True, timeout=60)
            self.assertEqual(result.returncode, 2)
            report = dataset.read_json(root / 'run/summary.json')
            self.assertFalse(report['records'][0]['schemaValid'])
            self.assertFalse(report['humanQualityMetricsAvailable'])
            self.assertTrue((root / 'run/sample-000-profile-000-repeat-00/response.json').exists())
    def test_structured_observation_request_keeps_four_images(self):
        from types import SimpleNamespace
        with tempfile.TemporaryDirectory() as temp, server(json.dumps(unknown_observations())) as (http, url):
            root = Path(temp); packet(root / 'capture')
            path = root / 'dataset.json'; path.write_text(json.dumps(dataset.inventory(root, path)))
            profiles = root / 'profiles.json'; profiles.write_text(json.dumps({'profiles': [dict(id='fixture', model='fixture', endpoint=url)]}))
            args = SimpleNamespace(dataset=str(path), profiles=str(profiles), annotations=None, output=str(root / 'run'),
                dotnet=self.dotnet, repeats=1, protocol='observations-v1', timeout=3, tie_tolerance=.25,
                runtime_config=None, structured_observations=True)
            report = benchmark.run(args)
            self.assertTrue(report['records'][0]['schemaValid'])
            self.assertFalse(report['humanQualityMetricsAvailable'])
            self.assertEqual(http.requests[0]['response_format']['type'], 'json_schema')
            self.assertEqual(sum(p['type'] == 'image_url' for p in http.requests[0]['messages'][1]['content']), 4)
    def test_changed_input_between_repeats_is_not_sent(self):
        from types import SimpleNamespace
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); capture = packet(root / 'capture')
            path = root / 'dataset.json'; path.write_text(json.dumps(dataset.inventory(root, path)))
            def mutate(count):
                if count == 1:
                    value = dataset.read_json(capture / 'evaluation-input.json')
                    value['garment']['finished'] = True
                    (capture / 'evaluation-input.json').write_text(json.dumps(value)); refresh(capture)
            with server((ROOT / 'tests/fixtures/jury-string-remarks.json').read_text(), on_request=mutate) as (http, url):
                profiles = root / 'profiles.json'; profiles.write_text(json.dumps({'profiles': [dict(id='fixture', model='fixture', endpoint=url)]}))
                args = SimpleNamespace(dataset=str(path), profiles=str(profiles), annotations=None, output=str(root / 'run'),
                    dotnet=self.dotnet, repeats=2, protocol='baseline', timeout=3, tie_tolerance=.25, runtime_config=None)
                report = benchmark.run(args)
                self.assertEqual(len(http.requests), 1)
                self.assertTrue(report['records'][0]['schemaValid'])
                self.assertFalse(report['records'][1]['schemaValid'])
                self.assertIn('changed between requests', report['records'][1]['error'])


@unittest.skipUnless(os.name == 'posix' and Path('/proc').exists(), 'owned runtime probe is Linux-only')
class OwnedRuntimeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.root = Path(self.temp.name)
        script = self.root / 'fake-server'
        script.write_text('''#!/usr/bin/env python3
import sys,json,http.server,pathlib
port=int(sys.argv[sys.argv.index('--port')+1]); alias=sys.argv[sys.argv.index('--alias')+1]
class H(http.server.BaseHTTPRequestHandler):
 def log_message(self,*args):pass
 def do_GET(self):
  identity='unrelated' if pathlib.Path(__file__).with_name('bad-alias').exists() else alias
  data={'status':'ok'} if self.path=='/health' else {'data':[{'id':identity}]}
  body=json.dumps(data).encode();self.send_response(200);self.end_headers();self.wfile.write(body)
http.server.HTTPServer(('127.0.0.1',port),H).serve_forever()
''')
        script.chmod(0o755)
        model = self.root / 'model.gguf'; model.write_bytes(b'GGUFfixture')
        artifact = lambda p: dict(path=str(p), sha256=dataset.digest(p))
        self.config = self.root / 'runtime.json'
        self.config.write_text(json.dumps(dict(server=artifact(script), model=artifact(model), projector=artifact(model), threads=1, context=512)))
    def tearDown(self):
        self.temp.cleanup()
    def test_owned_readiness_and_cleanup(self):
        runtime = OwnedVisionRuntime(self.config, self.root / 'session', timeout=5)
        with runtime:
            self.assertIsNone(runtime.process.poll())
            self.assertEqual(runtime.record['state'], 'ready')
        self.assertIsNotNone(runtime.process.poll())
        self.assertTrue(dataset.read_json(self.root / 'session/session.json')['processExited'])
    def test_checksum_failure_does_not_start_process(self):
        (self.root / 'model.gguf').write_bytes(b'GGUFtampered')
        runtime = OwnedVisionRuntime(self.config, self.root / 'session', timeout=5)
        with self.assertRaises(ValueError): runtime.start()
        self.assertIsNone(runtime.process)
    def test_wrong_model_identity_fails_readiness_and_cleans_up(self):
        (self.root / 'bad-alias').touch()
        runtime = OwnedVisionRuntime(self.config, self.root / 'session', timeout=.4)
        with self.assertRaises(TimeoutError): runtime.start()
        self.assertIsNotNone(runtime.process.poll())
        self.assertTrue(dataset.read_json(self.root / 'session/session.json')['processExited'])
    def test_exception_inside_context_stops_only_owned_process(self):
        runtime = OwnedVisionRuntime(self.config, self.root / 'session', timeout=5)
        with server('{}') as (unrelated, url):
            with self.assertRaisesRegex(RuntimeError, 'injected'):
                with runtime:
                    raise RuntimeError('injected')
            self.assertIsNotNone(runtime.process.poll())
            self.assertTrue(benchmark.post(url, {}, 3))


if __name__ == '__main__':
    unittest.main()
