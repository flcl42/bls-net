"""Check units, duplicate inverse rows, paired medians and incomplete captures."""
import contextlib
import io
import json
from pathlib import Path
import tempfile
import unittest

import report


class ReportTests(unittest.TestCase):
    def test_existing_harness_formats(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'raw.txt'
            path.write_text('''secret_operation,constant_time_us,variable_time_us,blst_us
CT Fp inverse,4.000,3.000,2.000
Fr mul+add | managed 60.00 ns | native 30.00 ns
Operation | managed ns | blst ns | managed bytes
Fp multiply | 80.00 | 40.00 | 0
Fp inverse | 3000.00 | 2000.00 | 0
Operation | managed ns | native ns | managed bytes
Fp inverse | 4000.00 | 2200.00 | 0
Fp inverse Fermat | 8000.00 | NaN | 0
operation,managed_us,blst_us
G1 MSM 128,6000.000,3000.000
''')
            rows = report.parse(path)
            self.assertEqual(rows['CT Fp inverse'], (4, 2))
            self.assertEqual(rows['Fp inverse (variable, same inputs)'], (3, 2))
            self.assertEqual(rows['Fr mul+add'], (0.06, 0.03))
            self.assertEqual(rows['Fp multiply'], (0.08, 0.04))
            self.assertEqual(rows['Fp inverse (kernel)'], (3, 2))
            self.assertEqual(rows['Fp inverse (tower)'], (4, 2.2))
            self.assertEqual(rows['G1 MSM 128'], (6000, 3000))

    def test_medians_use_each_hosts_native_samples(self):
        with tempfile.TemporaryDirectory() as directory:
            out = Path(directory)
            (out / 'source.json').write_text(json.dumps({
                'runtime_repo': 'example/runtime', 'runtime_commit': 'abc',
                'library_commit': 'def', 'settings': {'BENCH_REPETITIONS': '3'}}))
            (out / 'machine.json').write_text(json.dumps({'affinity': [2, 3]}))
            (out / 'executions.jsonl').write_text('\n'.join(json.dumps({
                'name': f'jit-{i}', 'exit_code': 100, 'expected': 100}) for i in range(20)))
            for policy in ('compact', 'expanded'):
                for host, times in (('patched', [(2, 1), (99, 3), (4, 2)]),
                                    ('stock', [(6, 4), (100, 2), (8, 6)])):
                    for i, (managed, native) in enumerate(times, 1):
                        (out / f'bench-{host}-{policy}-{i}.txt').write_text(
                            f'operation,managed_us,blst_us\nFp multiply,{managed},{native}\n')
            with contextlib.redirect_stdout(io.StringIO()):
                report.main(out)
            medians = json.loads((out / 'medians.json').read_text())
            self.assertEqual(medians['compact']['Fp multiply'], {
                'patched': [4, 2], 'stock': [8, 4]})
            self.assertIn('| 8.000 | 4.000 | 4.000 / 2.000 | 2.00x | 2.00x |',
                          (out / 'summary.md').read_text(encoding='utf-8'))
            self.assertIn('taskset, CPU 2', (out / 'summary.md').read_text(encoding='utf-8'))
            (out / 'machine.json').write_text(json.dumps({'affinity': None}))
            with contextlib.redirect_stdout(io.StringIO()):
                report.main(out)
            self.assertIn('CPU affinity: **unpinned**', (out / 'summary.md').read_text(encoding='utf-8'))
            (out / 'bench-stock-expanded-3.txt').write_text('')
            with self.assertRaisesRegex(RuntimeError, 'Missing or mismatched'):
                report.main(out)


if __name__ == '__main__':
    unittest.main()
