"""Parse the existing harness formats, retain paired native medians, fail on gaps."""
import csv
import json
import math
import os
from pathlib import Path
import re
from statistics import median
import sys


def parse(path):
    rows = {}
    section = ''
    for line in path.read_text(errors='replace').splitlines():
        if line.startswith('secret_operation,'):
            section = 'secret'
        elif line.startswith('operation,'):
            section = 'curve'
        elif line.startswith('Operation |'):
            section = 'kernel' if 'blst ns' in line else 'tower'
        if '|' in line:
            parts = [v.strip() for v in line.split('|')]
            try:
                if len(parts) == 3 and parts[1].startswith('managed '):
                    name, managed, native = parts[0], float(parts[1].split()[1]), float(parts[2].split()[1])
                    rows[name] = managed / 1000, native / 1000
                elif len(parts) == 4:
                    managed, native = float(parts[1]), float(parts[2])
                    name = parts[0]
                    if name == 'Fp inverse':
                        name += f' ({section})'
                    rows[name] = managed / 1000, native / 1000
            except ValueError:
                continue
        elif section in ('secret', 'curve'):
            parts = next(csv.reader([line]))
            try:
                if section == 'secret' and len(parts) == 4:
                    managed, variable, native = map(float, parts[1:])
                    rows[parts[0]] = managed, native
                    if math.isfinite(variable):
                        rows[parts[0].removeprefix('CT ') + ' (variable, same inputs)'] = variable, native
                elif section == 'curve' and len(parts) == 3:
                    rows[parts[0]] = float(parts[1]), float(parts[2])
            except ValueError:
                continue
    return rows


def main(out):
    source = json.loads((out / 'source.json').read_text())
    repetitions = int(source['settings']['BENCH_REPETITIONS'])
    lines = ['# ARM64 runtime comparison', '',
             f"Runtime: `{source['runtime_repo']}@{source['runtime_commit']}`; library: `{source['library_commit']}`.",
             '', f'Medians of {repetitions} alternating, single-core captures per host/policy; all times in **µs**.',
             'Each host measures native blst in the same process. Ratios use that host’s own native median.',
             'ReadyToRun and tiering are disabled. The harness reports best rounds; shared-runner noise remains.', '']
    for patch in source.get('patches', []):
        lines += [f"Additional runtime patch: `{patch['path']}` (SHA-256 `{patch['sha256']}`).", '']
    if (out / 'timings.txt').exists():
        lines += ['```text', (out / 'timings.txt').read_text().strip(), '```', '']
    executions = [json.loads(line) for line in (out / 'executions.jsonl').read_text().splitlines()]
    jit = [item for item in executions if item['name'].startswith('jit-')]
    if len(jit) != 20 or any(item['exit_code'] != item['expected'] for item in executions):
        raise RuntimeError('Incomplete or failed validation matrix')
    lines += [f'JIT regression matrix: **{len(jit)}/20 passed** (Release + Checked, tiering 0/1, default/stress2/regs1/3/8).', '']
    result = {}
    for policy in ('compact', 'expanded'):
        data = {host: [parse(out / f'bench-{host}-{policy}-{i + 1}.txt')
                       for i in range(repetitions)] for host in ('patched', 'stock')}
        names = list(data['patched'][0])
        if not names or any(set(sample) != set(names) for samples in data.values() for sample in samples):
            raise RuntimeError(f'Missing or mismatched benchmark rows for {policy}')
        lines += [f'## {policy.capitalize()} kernels', '',
                  '| Operation | Stock .NET 11 | Patched | blst (stock / patched) | Stock / blst | Patched / blst |',
                  '|---|---:|---:|---:|---:|---:|']
        result[policy] = {}
        for name in names:
            values = {host: [median(sample[name][column] for sample in samples) for column in (0, 1)]
                      for host, samples in data.items()}
            patched, pn = values['patched']
            stock, sn = values['stock']
            if not math.isfinite(patched) or not math.isfinite(stock):
                raise RuntimeError(f'Non-finite managed time: {name}')
            def fmt(value):
                return f'{value:.3f}' if math.isfinite(value) else '—'
            def ratio(value, native):
                return f'{value / native:.2f}x' if math.isfinite(native) and native > 0 else '—'
            lines.append(f'| {name} | {fmt(stock)} | {fmt(patched)} | {fmt(sn)} / {fmt(pn)} | {ratio(stock, sn)} | {ratio(patched, pn)} |')
            result[policy][name] = {host: [v if math.isfinite(v) else None for v in pair]
                                    for host, pair in values.items()}
        lines.append('')
    text = '\n'.join(lines) + '\n'
    (out / 'summary.md').write_text(text, encoding='utf-8')
    (out / 'medians.json').write_text(json.dumps(result, indent=2, allow_nan=False) + '\n')
    print(text)
    if 'GITHUB_STEP_SUMMARY' in os.environ:
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a', encoding='utf-8') as summary:
            summary.write(text)


if __name__ == '__main__':
    main(Path(sys.argv[1]))
