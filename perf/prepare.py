"""Apply branch patches, fingerprint all effective runtime inputs except JIT/tests."""
import hashlib
import json
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ROOT / '.perf-runtime'
OUT = ROOT / 'artifacts/perf'
OUT.mkdir(parents=True, exist_ok=True)


def git(*args):
    return subprocess.check_output(['git', '-C', str(RUNTIME), *args])


def config():
    # Let bash interpret the documented env-file syntax, without eval in Python.
    result = subprocess.check_output([
        'bash', '-c', 'set -a; source perf/runtime.env; python3 -c '
        + "'import json,os; print(json.dumps(dict(os.environ)))'"], cwd=ROOT)
    return json.loads(result)


if __name__ == '__main__':
    settings = config()
    patches = []
    for name in settings.get('RUNTIME_PATCHES', '').split():
        path = (ROOT / name).resolve()
        path.relative_to(ROOT)
        subprocess.run(['git', '-C', str(RUNTIME), 'apply', str(path)], check=True)
        patches.append({'path': name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})

    # Tree records contain blob ID, path and mode. Include working-tree patch changes
    # (including additions) using a private index, never changing the checkout's index.
    index = ROOT / 'artifacts/perf/runtime.index'
    env = dict(os.environ, GIT_INDEX_FILE=str(index))
    subprocess.run(['git', '-C', str(RUNTIME), 'read-tree', 'HEAD'], env=env, check=True)
    subprocess.run(['git', '-C', str(RUNTIME), 'add', '-A'], env=env, check=True)
    records = subprocess.check_output(['git', '-C', str(RUNTIME), 'ls-files', '-s', '-z'], env=env)
    nonjit = b'\0'.join(record for record in records.split(b'\0') if record and
                        not record.split(b'\t', 1)[1].startswith((b'src/coreclr/jit/', b'src/tests/JIT/')))
    # Keep build flags/version in the key too; no unsafe restore-key fallback.
    recipe = (ROOT / 'perf/build-runtime.sh').read_bytes()
    compiler = subprocess.check_output(['clang', '--version'])
    key = hashlib.sha256(nonjit + recipe + compiler).hexdigest()
    index.unlink()
    metadata = {'runtime_repo': settings['RUNTIME_REPO'], 'runtime_ref': settings['RUNTIME_REF'],
                'runtime_commit': git('rev-parse', 'HEAD').decode().strip(), 'patches': patches,
                'nonjit_key': key, 'library_commit': subprocess.check_output(
                    ['git', 'rev-parse', 'HEAD'], cwd=ROOT).decode().strip(),
                'compiler': compiler.decode(), 'settings': {k: settings[k] for k in
                    ('BENCH_FILTER', 'BENCH_REPETITIONS', 'RUN_MACOS')}}
    (OUT / 'source.json').write_text(json.dumps(metadata, indent=2) + '\n')
    with open(os.environ['GITHUB_OUTPUT'], 'a') as output:
        output.write(f'key={key}\n')
