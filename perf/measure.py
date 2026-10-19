"""Sequential hardware validation and paired, single-core benchmark captures."""
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/perf'
HOST = ROOT / '.perf-cache/testhost'
STOCK = Path(os.environ['STOCK_DOTNET']).resolve()
TESTS = ROOT / 'tests/Nethermind.Crypto.BlsNet.Tests/bin/Release/net11.0/Nethermind.Crypto.BlsNet.Tests.dll'
BENCH = ROOT / 'benchmarks/Nethermind.Crypto.BlsNet.Benchmarks/bin/Release/net11.0/Nethermind.Crypto.BlsNet.Benchmarks.dll'
JIT_TESTS = OUT / 'jit-runner/bin/Release/net11.0/Runner.dll'
EXT = 'so' if platform.system() == 'Linux' else 'dylib'


def install_jit(config):
    targets = list((HOST / 'shared/Microsoft.NETCore.App').glob(f'*/libclrjit.{EXT}'))
    if len(targets) != 1:
        raise RuntimeError(f'Expected one testhost JIT, got {targets}')
    shutil.copyfile(OUT / f'libclrjit-{config}.{EXT}', targets[0])


def environment(host):
    env = {k: v for k, v in os.environ.items() if not k.startswith(('DOTNET_Jit', 'COMPlus_',
           'DOTNET_Tiered', 'DOTNET_ReadyToRun'))}
    env.update(DOTNET_ROOT=str(HOST if host == 'patched' else STOCK.parent),
               DOTNET_ROLL_FORWARD='Major', DOTNET_ROLL_FORWARD_TO_PRERELEASE='1',
               DOTNET_TieredCompilation='0', DOTNET_ReadyToRun='0',
               DOTNET_EnableDiagnostics='0', LC_ALL='C', LANG='C')
    return env


def run(host, dll, args, name, extra=None, pinned=False, success=0, timeout=900):
    env = environment(host)
    env.update(extra or {})
    launcher = str(HOST / 'dotnet') if host == 'patched' else str(STOCK)
    command = [launcher, str(dll), *args]
    if pinned and platform.system() == 'Linux':
        core = min(os.sched_getaffinity(0))
        command = ['taskset', '-c', str(core), *command]
    print('RUN', name, flush=True)
    start = time.monotonic()
    with (OUT / f'{name}.txt').open('w') as output:
        output.write(f'Command: {command!r}\nEnvironment: {json.dumps({k: env[k] for k in env if k.startswith("DOTNET_")})}\n')
        output.flush()
        # Preserve complete output and enforce timeouts even if the JIT hangs.
        result = subprocess.run(command, cwd=ROOT, env=env, stdout=output,
                                stderr=subprocess.STDOUT, timeout=timeout)
    seconds = time.monotonic() - start
    with (OUT / 'executions.jsonl').open('a') as record:
        record.write(json.dumps({'name': name, 'exit_code': result.returncode,
                                 'expected': success, 'seconds': seconds}) + '\n')
    print((OUT / f'{name}.txt').read_text(errors='replace')[-2500:], flush=True)
    if result.returncode != success:
        raise RuntimeError(f'{name}: exit {result.returncode}, expected {success}')


def test():
    if platform.machine().lower() not in ('aarch64', 'arm64'):
        raise RuntimeError('Real ARM64 process required')
    machine = {'system': platform.platform(), 'machine': platform.machine(),
               'processor': platform.processor(), 'stock_dotnet': str(STOCK),
               'affinity': sorted(os.sched_getaffinity(0)) if hasattr(os, 'sched_getaffinity') else None}
    command = ['lscpu'] if platform.system() == 'Linux' else ['sysctl', '-a']
    machine['cpu_details'] = subprocess.check_output(command, text=True)
    (OUT / 'machine.json').write_text(json.dumps(machine, indent=2) + '\n')
    install_jit('Release')
    # Executing managed code with ReadyToRun disabled verifies JIT-EE compatibility.
    for policy in ('compact', 'expanded'):
        run('patched', TESTS, [f'--{policy}-kernels', '--require-native'], f'library-{policy}-native')
        run('patched', TESTS, [f'--{policy}-kernels', '--no-native'], f'library-{policy}-managed')
    for config in ('Release', 'Checked'):
        install_jit(config)
        for tiering in ('0', '1'):
            for stress, settings in (
                ('default', {}), ('stress2', {'DOTNET_JitStress': '2'}),
                ('regs1', {'DOTNET_JitStressRegs': '1'}),
                ('regs3', {'DOTNET_JitStressRegs': '3'}),
                ('regs8', {'DOTNET_JitStressRegs': '8'})):
                run('patched', JIT_TESTS, [], f'jit-{config}-tier{tiering}-{stress}',
                    dict(settings, DOTNET_TieredCompilation=tiering), success=100)
    install_jit('Release')


def bench():
    source = json.loads((OUT / 'source.json').read_text())
    settings = source['settings']
    repetitions = int(settings['BENCH_REPETITIONS'])
    if repetitions < 3 or repetitions % 2 == 0:
        raise ValueError('Use an odd BENCH_REPETITIONS >= 3')
    install_jit('Release')
    for policy in ('compact', 'expanded'):
        for host in ('patched', 'stock'):
            run(host, BENCH, ['--quick', '--require-native', f'--{policy}-kernels'],
                f'quick-{host}-{policy}', pinned=True)
        for repetition in range(repetitions):
            order = ('patched', 'stock') if repetition % 2 == 0 else ('stock', 'patched')
            for host in order:
                run(host, BENCH, ['--quick', '--require-native', f'--{policy}-kernels',
                    '--filter', settings['BENCH_FILTER']],
                    f'bench-{host}-{policy}-{repetition + 1}', pinned=True)
        # Capture separately so disassembly does not perturb timed samples.
        for host in ('patched', 'stock'):
            path = OUT / f'disasm-{host}-{policy}.asm'
            run(host, BENCH, ['--quick', '--require-native', f'--{policy}-kernels',
                '--filter', settings['BENCH_FILTER']], f'disasm-{host}-{policy}',
                {'DOTNET_JitDisasm': '*', 'DOTNET_JitDisasmAssemblies': 'Nethermind.Crypto.BlsNet',
                 'DOTNET_JitStdOutFile': str(path)}, pinned=True)
            if not path.exists() or 'Assembly listing' not in path.read_text():
                raise RuntimeError(f'JIT disassembly missing: {path}')


if __name__ == '__main__':
    {'test': test, 'bench': bench}[sys.argv[1]]()
