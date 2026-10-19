"""Small regression corpus for security-relevant inventory coverage."""
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


class InventoryTests(unittest.TestCase):
    def test_both_architectures_and_repeated_versions(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'disassembly.txt'
            path.write_text('''; Assembly listing for method ConstantTimeAudit:Exercise() (FullOpts)
G_M000_IG01:
 cmp x0, x1
 b.ge G_M000_IG02
 bge G_M000_IG02
 cbz x0, G_M000_IG02
 cbnz x0, G_M000_IG02
 tbz x0, #1, G_M000_IG02
 tbnz x0, #1, G_M000_IG02
 bl Helper:Arm()
 csel x0, x1, x2, eq
 csinc x0, x1, x2, eq
 cset x0, eq
 csetm x0, eq
 cneg x0, x1, eq
; Assembly listing for method Helper:Arm() (FullOpts)
G_M001_IG01:
 ldr x0, [x1, x2, LSL #3]
 blr x3 // code for Helper:X64()
 br x4
 udiv x0, x1, x2
 sdiv w0, w1, w2
 blraaz x5 // code for Helper:Leaf()
 b Helper:Leaf()
; Assembly listing for method Helper:X64() (Tier0)
G_M002_IG01:
 jne G_M002_IG02
 mov rax, [rcx+rdx]
 call [Helper:Leaf()]
; Assembly listing for method Helper:Leaf() (Tier1)
 ret
; Assembly listing for method Helper:Leaf() (Tier1)
 idiv rcx
''')
            subprocess.run([sys.executable, str(Path(__file__).with_name('list-constant-time-jumps.py')), str(path)], check=True, capture_output=True)
            self.assertEqual(len((path.parent / 'jumps.tsv').read_text().splitlines()), 8)
            self.assertEqual(len((path.parent / 'methods.txt').read_text().splitlines()), 5)
            self.assertEqual(len((path.parent / 'variable-latency-candidates.txt').read_text().splitlines()), 3)
            self.assertIn('[x1, x2, LSL #3]', (path.parent / 'addresses.tsv').read_text())
            self.assertIn('[x1, x2, LSL #3]', (path.parent / 'indexed-addresses.tsv').read_text())
            self.assertIn('[rcx+rdx]', (path.parent / 'indexed-addresses.tsv').read_text())
            self.assertIn('br x4', (path.parent / 'indirect-branches.tsv').read_text())
            self.assertIn('blraaz x5', (path.parent / 'indirect-branches.tsv').read_text())


if __name__ == '__main__':
    unittest.main()
