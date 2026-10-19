"""Exercise effective-source fingerprints against a real patched Git worktree."""
from pathlib import Path
import subprocess
import tempfile
import unittest

from prepare import source_records


class CacheTests(unittest.TestCase):
    def test_jit_and_new_tests_reuse_runtime_but_interface_changes_invalidate(self):
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory)
            runtime = base / 'runtime'
            runtime.mkdir()
            def git(*args):
                return subprocess.check_output(['git', '-C', str(runtime), *args], stderr=subprocess.STDOUT)
            def write(name, text):
                path = runtime / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(text)
            def state():
                return source_records(runtime, base / 'private.index')
            git('init', '-q')
            write('src/coreclr/inc/corjit.h', 'original\n')
            write('src/coreclr/jit/lowerarmarch.cpp', 'original\n')
            write('src/tests/JIT/opt/Add/Carry.cs', 'original\n')
            git('add', '-A')
            git('-c', 'user.name=Cache test', '-c', 'user.email=test@example.invalid',
                'commit', '-qm', 'Initialize fixture')
            original_index = (runtime / '.git/index').read_bytes()
            original, original_nonjit = state()
            write('src/coreclr/jit/lowerarmarch.cpp', 'changed JIT\n')
            write('src/tests/JIT/opt/Add/NewRegression.cs', 'new test\n')
            changed, changed_nonjit = state()
            self.assertNotEqual(changed, original)
            self.assertEqual(changed_nonjit, original_nonjit)
            self.assertEqual((runtime / '.git/index').read_bytes(), original_index)
            write('src/coreclr/inc/corjit.h', 'changed interface\n')
            self.assertNotEqual(state()[1], original_nonjit)
            write('src/libraries/NewLibrary.cs', 'new non-JIT input\n')
            self.assertIn(b'src/libraries/NewLibrary.cs', state()[1])
            self.assertFalse((base / 'private.index').exists())


if __name__ == '__main__':
    unittest.main()
