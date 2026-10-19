"""Compile all opt/Add sources at the chosen runtime commit, including new tests."""
from pathlib import Path
import shutil

root = Path(__file__).resolve().parents[1]
target = root / 'artifacts/perf/jit-runner'
target.mkdir(parents=True, exist_ok=True)
sources = sorted((root / '.perf-runtime/src/tests/JIT/opt/Add').rglob('*.cs'))
if not sources:
    raise RuntimeError('No opt/Add regression tests found')
for i, source in enumerate(sources):
    shutil.copyfile(source, target / f'{i:03}-{source.name}')
shutil.copyfile(root / 'perf/jit-runner.cs', target / 'Runner.cs')
(target / 'Runner.csproj').write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net11.0</TargetFramework>
    <Nullable>disable</Nullable><AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Optimize>true</Optimize><StartupObject>RunnerMain</StartupObject>
    <NoWarn>$(NoWarn);CS8981;CS0162;CS0168;CS0219;CS0649;CS0414;xUnit1013</NoWarn>
  </PropertyGroup>
</Project>
''')
(root / 'artifacts/perf/jit-sources.txt').write_text('\n'.join(
    str(p.relative_to(root / '.perf-runtime')) for p in sources) + '\n')
print(f'Included {len(sources)} opt/Add sources')
