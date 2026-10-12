# Managed C# BLST Roadmap

This area is a staged managed rewrite of BLST for high-performance C# crypto code. The
existing `bindings/c#` directory remains the generated P/Invoke proof of concept over
`blst.dll`; new managed code lives here and is split into separate projects so each
cryptographic layer can be tested and benchmarked before the next one depends on it.

## Engineering Rules

- Use fixed-width value types backed by `ulong` limbs.
- Keep hot arithmetic APIs allocation-free: prefer `in`, `out`, `Span<T>`, and
  `stackalloc`.
- Store field elements in Montgomery form internally.
- Keep parsing and serialization explicit about byte order and canonical range checks.
- Validate each layer against independent tests and against the native C BLST library
  before building dependent layers.
- Keep constant-time behavior in scope for secret-dependent operations; do not replace
  production arithmetic with `BigInteger`.

## Milestones

1. Arithmetic foundation
   - Implement `Fr` and `Fp` limb arithmetic in managed C#.
   - Support zero, one, canonical big/little-endian parsing, serialization, add, sub,
     neg, multiply, and square.
   - Add allocation checks and native-comparison benchmark hooks.

2. Extension fields
   - Implement `Fp2`, then `Fp6` and `Fp12` on top of the `Fp` kernel.
   - Add Frobenius, conjugation, inversion, cyclotomic square, and final exponent
     helpers as needed by pairings.

3. G1/G2 curve operations
   - Implement affine and Jacobian point types for G1 and G2.
   - Add point addition, doubling, scalar multiplication, generator constants, equality,
     infinity handling, curve checks, and subgroup checks.

4. Serialization compatibility
   - Match BLST compressed and uncompressed encodings.
   - Add strict error reporting compatible with the current C API surface.

5. Hash-to-field and hash-to-curve
   - Implement expand-message XMD SHA-256, hash-to-field, SSWU map, isogeny maps, and
     cofactor clearing.
   - Validate with the existing hash-to-curve vectors under `bindings/vectors`.

6. Pairing and BLS API
   - Implement Miller loop, final exponentiation, pairing aggregation, core verify,
     sign, aggregate, and the min-pk/min-sig convenience layers.

7. Packaging and performance hardening
   - Add NuGet-ready packaging, CI tests, C-vs-managed benchmark baselines, no-allocation
     regression checks, and platform-specific optimization notes.

## Current Status

Milestone 1 has an initial managed arithmetic kernel for `Fr` and `Fp`, a no-package
test runner, and a benchmark runner under this directory. The benchmark runner can
compare against native BLST when `blst.dll` is built in the repository root.

## Performance

The `mul+add` benchmarks (`acc = acc * a + b`, 1,000,000 dependent iterations; the runner
reports the best of 7 rounds, the table the best of three runs) on a Ryzen 9 9950X3D, in ns
per iteration; runs vary by about 5% on this machine:

| Kernel     | .NET 11 RC1 | .NET with carry-aware JIT | blst (ADX) via P/Invoke |
|------------|------------:|--------------------------:|------------------------:|
| Fr mul+add |        26.7 |                      13.3 |                    12.3 |
| Fp mul+add |        56.9 |                      23.3 |                    21.5 |

The native column calls blst's MULX/ADCX/ADOX assembly through `[SuppressGCTransition]`
imports, which is within a few percent of the same loop written in C. Without ADX (blst's
MULQ assembly) that C loop takes 13.2 and 24.8 ns. The numbers are for this isolated loop;
code that inlines many more operations into one method can exceed the JIT budgets below.

The released JIT never keeps a carry in the flags: every carry costs `cmp; setb; movzx` and
a register, `Math.BigMul` uses MUL, which fixes RDX:RAX and clobbers the flags, and there are
no dual ADCX/ADOX carry chains. Together this made the managed kernels 2-3 times slower than
blst. The kernels are written in carry idioms that a carry-aware JIT turns into `adc`/`sbb`
and MULX/ADCX/ADOX chains:

- multiply-accumulate: `hi = Math.BigMul(m, x, out lo); lo += t; hi += lo < t ? 1 : 0; lo += c; hi += lo < c ? 1 : 0;`
  with the multiplier shared by a row of products first (MULX takes it in RDX);
- add with carry: `s = a + b; c1 = s < a ? 1 : 0; r = s + carry; carry = c1 + (r < s ? 1 : 0);`
- subtract with borrow: `d = a - b; b1 = d > a ? 1 : 0; r = d - borrow; borrow = b1 + (r > d ? 1 : 0);`
- the final conditional subtraction adds `2^n - modulus`, whose carry out means "not below the modulus".

The carry-aware JIT is the open (unmerged) [dotnet/runtime#134039](https://github.com/dotnet/runtime/pull/134039),
which recognizes the add/subtract idioms and BigInteger-style multiply-accumulate loops, at
`331fa2b3` plus [`jit/carry-rows.patch`](jit/carry-rows.patch), which:

- keeps CF and OF across the rows of a Montgomery multiplication, the `MulAdd` shape above
  (several rows per block, rows of any width, the shorter first lanes of CIOS rows);
- lets loads, `lea`, non-zero constants and casts sit between a carry and its consumer;
- also optimizes OSR methods, whose Tier0 frame can only hold root-method locals, not carry temporaries.

The PR skips methods with exception handling and gives up on full adders in methods larger
than 2,048 IR nodes, and a fully unrolled 6-limb row loop exceeds its dead-carry scan budget,
which is why `Fp.Multiply` keeps its loop over rows.

To reproduce, build that runtime and run the benchmark on its testhost:

```powershell
git fetch https://github.com/dotnet/runtime pull/134039/head
git checkout 331fa2b3
git apply <blst>/bindings/csharp-managed/jit/carry-rows.patch
./build.cmd clr+libs -c Release
$env:DOTNET_ROLL_FORWARD = "Major"
$env:DOTNET_ROOT = "<runtime>/artifacts/bin/testhost/net11.0-windows-Release-x64"
& "$env:DOTNET_ROOT/dotnet.exe" bindings/csharp-managed/Blst.Managed.Benchmarks/bin/Release/net11.0/Blst.Managed.Benchmarks.dll
```

## Local Commands

```powershell
./bindings/csharp-managed/build-native-blst.ps1
dotnet run --project bindings/csharp-managed/Blst.Managed.Tests/Blst.Managed.Tests.csproj -c Release
dotnet run --project bindings/csharp-managed/Blst.Managed.Benchmarks/Blst.Managed.Benchmarks.csproj -c Release
```
