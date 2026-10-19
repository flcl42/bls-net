"""List every conditional jump, call and address-bearing instruction in the audit call graph.

This is an inventory for manual data-flow review, not an automatic constant-time proof.
Use --all-methods for R2R captures with unresolved relocation-based calls.
"""
from pathlib import Path
import re
import sys

path = Path(sys.argv[1])
text = path.read_text(encoding='utf-8-sig')
all_methods = '--all-methods' in sys.argv[2:]
conditional = re.compile(r'^(?:j(?!mp\b)\w+|b\.[a-z]+|b(?:eq|ne|cs|hs|cc|lo|mi|pl|vs|vc|hi|ls|ge|lt|gt|le)|cbn?z|tbn?z)\s', re.I)
call = re.compile(r'^(?:(?:call|tail\.jmp|bl|blr|blraaz?|blrabz?|br|braaz?|brabz?)\s|b\s+(?=.*(?::|// code for)))', re.I)
indirect = re.compile(r'^(?:ret(?:aa|ab)?(?:\s|$)|(?:blr|br|blraaz?|blrabz?|braaz?|brabz?)\s|(?:call|jmp|tail\.jmp)\s+(?:\[|[re][a-z0-9]+\b))', re.I)
blocks = {}
versions = {}
for block in re.split(r'; Assembly listing for method ', text)[1:]:
    name = block.splitlines()[0]
    signature = re.sub(r' \([^)]*\)$', '', name)
    # Preserve repeated versions in tiered captures.
    original = name
    suffix = 2
    while name in blocks:
        name = f'{original} [capture {suffix}]'
        suffix += 1
    blocks[name] = block
    versions.setdefault(signature, []).append(name)

roots = list(blocks) if all_methods else [name for name in blocks if name.startswith('ConstantTimeAudit:Exercise(')]
if not roots:
    raise SystemExit('Missing audit Exercise method: check disassembly settings')
visited, pending, external = set(), list(roots), set()
while pending:
    name = pending.pop()
    if name in visited:
        continue
    visited.add(name)
    for line in blocks[name].splitlines()[1:]:
        instruction = line.strip()
        if not call.match(instruction):
            continue
        callee = instruction.split(None, 1)[1]
        matches = [s for s in versions if s in callee]
        if matches:
            for signature in matches:
                pending.extend(versions[signature])
        else:
            external.add(callee)

out = path.parent
with (out / 'jumps.tsv').open('w') as jumps, (out / 'calls.tsv').open('w') as calls, (out / 'addresses.tsv').open('w') as addresses, (out / 'indirect-branches.tsv').open('w') as branches, (out / 'indexed-addresses.tsv').open('w') as indexed:
    jumps.write('method\tblock\tinstruction\tprevious_instruction\n')
    calls.write('method\tcallee\n')
    addresses.write('method\tblock\tinstruction\n')
    branches.write('method\tblock\tinstruction\n')
    indexed.write('method\tblock\tinstruction\n')
    for name in sorted(visited):
        label = ''
        previous = ''
        for line in blocks[name].splitlines()[1:]:
            if line.startswith('G_M'):
                label = line.split(':')[0]
                continue
            instruction = line.strip()
            if conditional.match(instruction):
                jumps.write(f'{name}\t{label}\t{instruction}\t{previous}\n')
            if call.match(instruction):
                calls.write(f'{name}\t{instruction}\n')
            if indirect.match(instruction):
                branches.write(f'{name}\t{label}\t{instruction}\n')
            if '[' in instruction and not instruction.startswith(';'):
                addresses.write(f'{name}\t{label}\t{instruction}\n')
                if re.search(r'\[[^\]]*(?:,\s*[wx](?:\d+|ip[01])\b|\+\s*[re][a-z0-9]+\b|\*)', instruction, re.I):
                    indexed.write(f'{name}\t{label}\t{instruction}\n')
            if instruction and not instruction.startswith(';'):
                previous = instruction

(out / 'methods.txt').write_text('\n'.join(sorted(visited)) + '\n')
(out / 'external-calls.txt').write_text('\n'.join(sorted(external)) + '\n')
dangerous = []
for name in sorted(visited):
    for line in blocks[name].splitlines():
        if re.match(r'\s*(?:i?div|[us]div|v?gather\w*|v?pdep|v?pext)\s', line):
            dangerous.append(f'{name}: {line.strip()}')
(out / 'variable-latency-candidates.txt').write_text('\n'.join(dangerous) + '\n')
scope = 'all-method inventory' if all_methods else 'audit call graph'
if not all_methods and len(visited) == 1 and len(blocks) > 1 and external:
    print('WARNING: no symbolic callee closure; use --all-methods and manually review the library methods (older ARM64 JIT listings omit call annotations).', file=sys.stderr)
print(f'{len(visited)} compiled methods ({scope}), {len(external)} unresolved/runtime callees, {len(dangerous)} variable-latency candidates. Review jumps.tsv, addresses.tsv, calls.tsv, indirect-branches.tsv and external-calls.txt manually.')
