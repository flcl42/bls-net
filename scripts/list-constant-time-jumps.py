"""List every conditional jump, call and address-bearing instruction in the audit call graph.

This is an inventory for manual data-flow review, not an automatic constant-time proof.
Requires DOTNET_ReadyToRun=0 and DOTNET_TieredCompilation=0 captures from the audit driver.
"""
from pathlib import Path
import re
import sys

path = Path(sys.argv[1])
text = path.read_text()
blocks = {}
versions = {}
for block in re.split(r'; Assembly listing for method ', text)[1:]:
    name = block.splitlines()[0]
    signature = re.sub(r' \([^)]*\)$', '', name)
    blocks[name] = block
    versions.setdefault(signature, []).append(name)

roots = [name for name in blocks if name.startswith('ConstantTimeAudit:Exercise(')]
if not roots:
    raise SystemExit('Missing audit Exercise method: check disassembly settings')
visited, pending, external = set(), list(roots), set()
while pending:
    name = pending.pop()
    if name in visited:
        continue
    visited.add(name)
    for callee in re.findall(r'(?:call|tail\.jmp)\s+\[(.*)\]', blocks[name]):
        if callee in versions:
            pending.extend(versions[callee])
        else:
            external.add(callee)

out = path.parent
with (out / 'jumps.tsv').open('w') as jumps, (out / 'calls.tsv').open('w') as calls, (out / 'addresses.tsv').open('w') as addresses:
    jumps.write('method\tblock\tinstruction\tprevious_instruction\n')
    calls.write('method\tcallee\n')
    addresses.write('method\tblock\tinstruction\n')
    for name in sorted(visited):
        label = ''
        previous = ''
        for line in blocks[name].splitlines()[1:]:
            if line.startswith('G_M'):
                label = line.split(':')[0]
            instruction = line.strip()
            if re.match(r'j(?!mp\b)\w+\s', instruction):
                jumps.write(f'{name}\t{label}\t{instruction}\t{previous}\n')
            if re.match(r'(?:call|tail\.jmp)\s', instruction):
                calls.write(f'{name}\t{instruction}\n')
            if '[' in instruction and not instruction.startswith(';'):
                addresses.write(f'{name}\t{label}\t{instruction}\n')
            if instruction and not instruction.startswith(';'):
                previous = instruction

(out / 'methods.txt').write_text('\n'.join(sorted(visited)) + '\n')
(out / 'external-calls.txt').write_text('\n'.join(sorted(external)) + '\n')
dangerous = []
for name in visited:
    for line in blocks[name].splitlines():
        if re.match(r'\s*(?:i?div|v?gather\w*|v?pdep|v?pext)\s', line):
            dangerous.append(f'{name}: {line.strip()}')
(out / 'variable-latency-candidates.txt').write_text('\n'.join(dangerous) + '\n')
print(f'{len(visited)} reachable compiled methods, {len(external)} unresolved/runtime callees, {len(dangerous)} variable-latency candidates. Review jumps.tsv, addresses.tsv, calls.tsv and external-calls.txt manually.')
