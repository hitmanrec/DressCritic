#!/usr/bin/env python3
"""Linux development probe for an owned, checksummed llama-server process.

Not the shipped mod runtime. Never attaches to, stops or downloads an external server.
"""
import argparse
import json
import os
from pathlib import Path
import signal
import socket
import subprocess
import threading
import time
import urllib.error
import urllib.request
import uuid

from benchmark_dataset import digest, read_json
from benchmark_models import NoRedirect, post


class OwnedVisionRuntime:
    def __init__(self, config, output, timeout=300):
        self.config_path = Path(config).resolve()
        self.config = read_json(self.config_path)
        self.output = Path(output).resolve()
        self.timeout = timeout
        self.process = None
        self.port = None
        self.alias = 'dresscritic-owned-' + uuid.uuid4().hex
        self.peak_rss = 0
        self.stop_monitor = threading.Event()
        self.monitor = None
        self.log = None
        self.record = {'schemaVersion': '1.0.0', 'state': 'not-started', 'platform': 'linux',
                       'peakVramBytes': None, 'inGamePerformanceVerified': False,
                       'note': 'New owned process; OS cache unspecified. RSS excludes GPU memory and is not per-request allocation.'}

    def artifact(self, spec):
        path = (self.config_path.parent / spec['path']).resolve()
        if not path.is_file() or digest(path) != spec['sha256']:
            raise ValueError('artifact missing or checksum mismatch: ' + path.name)
        return path

    def sample_memory(self):
        while not self.stop_monitor.wait(0.1):
            try:
                lines = Path('/proc/%s/status' % self.process.pid).read_text().splitlines()
                rss = next(int(line.split()[1]) * 1024 for line in lines if line.startswith('VmRSS:'))
                self.peak_rss = max(self.peak_rss, rss)
            except (OSError, StopIteration, ValueError):
                pass

    def start(self):
        verification_started = time.perf_counter()
        if self.process is not None:
            raise ValueError('runtime already started')
        if os.name != 'posix' or not Path('/proc').exists():
            raise ValueError('this development probe supports Linux only')
        server = self.artifact(self.config['server'])
        model = self.artifact(self.config['model'])
        projector = self.artifact(self.config['projector'])
        for spec in self.config.get('runtimeFiles', []):
            self.artifact(spec)
        self.record['artifactVerificationSeconds'] = time.perf_counter() - verification_started
        for path in (model, projector):
            with path.open('rb') as stream:
                if stream.read(4) != b'GGUF':
                    raise ValueError('not a GGUF artifact')
        context, threads = self.config.get('context', 8192), self.config.get('threads', 4)
        if not isinstance(context, int) or not 512 <= context <= 32768 or not isinstance(threads, int) or not 1 <= threads <= 64:
            raise ValueError('invalid context/thread budget')
        if not 0 < self.timeout <= 1200:
            raise ValueError('invalid startup timeout')
        self.output.mkdir(parents=True, exist_ok=False)
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0))
            self.port = reservation.getsockname()[1]
        # A port race results in startup failure, never adoption of an existing service.
        command = [str(server), '--model', str(model), '--mmproj', str(projector),
                   '--host', '127.0.0.1', '--port', str(self.port), '--ctx-size', str(context),
                   '--parallel', '1', '--threads', str(threads), '--n-gpu-layers', '0',
                   '--no-mmproj-offload', '--alias', self.alias,
                   '--jinja', '--chat-template-kwargs', '{"enable_thinking":false}']
        self.record.update({'config': self.config, 'command': command, 'endpoint': self.endpoint,
                            'state': 'starting', 'startupTimeoutSeconds': self.timeout})
        self.log = (self.output / 'server.log').open('wb')
        started = time.perf_counter()
        try:
            self.process = subprocess.Popen(command, cwd=server.parent, stdout=self.log, stderr=subprocess.STDOUT, start_new_session=True)
            self.record['pid'] = self.process.pid
            self.monitor = threading.Thread(target=self.sample_memory, daemon=True)
            self.monitor.start()
            opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())
            deadline = time.monotonic() + self.timeout
            while time.monotonic() < deadline:
                if self.process.poll() is not None:
                    raise RuntimeError('owned server exited during startup; see server.log')
                try:
                    with opener.open('http://127.0.0.1:%s/health' % self.port, timeout=1) as response:
                        payload = response.read(4097)
                    ready = len(payload) <= 4096 and json.loads(payload).get('status') == 'ok'
                    if ready:
                        with opener.open('http://127.0.0.1:%s/v1/models' % self.port, timeout=1) as response:
                            model_ids = response.read(16385)
                        ready = len(model_ids) <= 16384 and any(m.get('id') == self.alias for m in json.loads(model_ids).get('data', []))
                    if ready and self.process.poll() is None:
                        self.record['startupSeconds'] = time.perf_counter() - started
                        self.record['totalStartupSeconds'] = time.perf_counter() - verification_started
                        self.record['state'] = 'ready'
                        self.save()
                        return self
                except (OSError, ValueError, urllib.error.HTTPError):
                    pass
                time.sleep(0.1)
            raise TimeoutError('owned server readiness deadline exceeded')
        except BaseException as error:
            self.record['error'] = type(error).__name__ + ': ' + str(error)
            self.stop()
            raise

    @property
    def endpoint(self):
        return 'http://127.0.0.1:%s/v1/chat/completions' % self.port

    def save(self):
        self.record['peakResidentSetBytes'] = self.peak_rss or None
        (self.output / 'session.json').write_text(json.dumps(self.record, indent=2))

    def stop(self):
        if self.process is not None:
            if self.process.poll() is None:
                try:
                    os.killpg(self.process.pid, signal.SIGTERM)
                    self.process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    os.killpg(self.process.pid, signal.SIGKILL)
                    self.process.wait(timeout=5)
                except ProcessLookupError:
                    self.process.wait(timeout=5)
            self.record['exitCode'] = self.process.returncode
            self.record['processExited'] = self.process.poll() is not None
        self.stop_monitor.set()
        if self.monitor is not None:
            self.monitor.join(timeout=2)
        if self.log is not None:
            self.log.close()
        self.record['state'] = 'stopped'
        if self.output.is_dir():
            self.save()

    def __enter__(self):
        return self.start()

    def __exit__(self, *args):
        self.stop()


def main():
    def terminate(signum, frame):
        raise SystemExit(128 + signum)
    signal.signal(signal.SIGTERM, terminate)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', required=True); parser.add_argument('--output', required=True)
    parser.add_argument('--request', help='One actual multimodal request for startup/inference/cleanup validation')
    parser.add_argument('--startup-timeout', type=float, default=300)
    args = parser.parse_args()
    with OwnedVisionRuntime(args.config, args.output, args.startup_timeout) as runtime:
        if args.request:
            request = read_json(args.request)
            request['model'] = runtime.alias
            (runtime.output / 'request.json').write_text(json.dumps(request))
            started = time.perf_counter()
            raw = post(runtime.endpoint, request, 600)
            (runtime.output / 'response.json').write_bytes(raw)
            runtime.record['requestSeconds'] = time.perf_counter() - started
            runtime.record['sourceRequestSha256'] = digest(args.request)
            runtime.record['requestSha256'] = digest(runtime.output / 'request.json')
        print(json.dumps({'endpoint': runtime.endpoint, 'pid': runtime.process.pid, 'state': 'ready'}), flush=True)
    print(json.dumps({'processExited': runtime.record.get('processExited'), 'session': str(runtime.output / 'session.json')}))


if __name__ == '__main__':
    main()
