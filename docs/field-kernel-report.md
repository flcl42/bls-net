# Field-kernel follow-up

This is the round 1 capture at `5369bab`. The [round 2 report](field-kernel-round2-report.md) supersedes its framework-based selection and records the subsequent kernels, JIT changes and validation.

The follow-up eliminates carry materialization from the captured hot field kernels and reduces net11.0 Fp2 multiplication from three Montgomery multiplications to three raw products and two reductions. Patched Fp2 multiply improves about 15%, and stock .NET 11 about 18%. The goal of beating blst on every multiply/square is **not fully reached**: this capture puts patched Fp multiply near parity, Fp square about 3% above the dedicated native square, Fp2 multiply about 6% above native, and Fp2 square about 10% above native.

## Kernel timings

Values are ns per operation; cells show before → after. The runner warms each operation for at least 0.5 s, then takes the best of seven rounds of 1,000,000 independent operations (30,000 for inversion). Native imports use `SuppressGCTransition`. Native squares use `blst_fp_sqr`/`blst_fp2_sqr`. Every managed loop allocates zero bytes.

Before: library commit `a6d129e` and the supplied carry-row JIT. After: the field/JIT changes described below. Both use the same final harness, with separate methods for large inline Fp multiply/square loops. Combining these kernels in one switch exceeds the 2,048-node full-adder budget and inflates unrelated cases.

Runtimes: .NET 10.0.12, .NET 11.0.0-rc.1.26425.128, and patched .NET 12.0.0-dev at runtime `331fa2b3`. Windows x64, Ryzen 9 9950X3D VM. Tiering is disabled for these captures. Patched/.NET 11 pin logical CPU 11. The .NET 10 paired repeat pins CPU 3, after the CPU 11 run showed contention outliers in unchanged reducers/adders. Native columns expose clock/contention differences; differences of a few percent are not reliable improvements or regressions on this shared host.

| Kernel | Managed patched | Managed .NET 11 | Managed .NET 10 | blst patched | blst .NET 11 | blst .NET 10 |
|---|---:|---:|---:|---:|---:|---:|
| Fp multiply | 21.13 → 19.99 | 52.46 → 52.65 | 51.36 → 51.64 | 20.55 → 19.86 | 19.73 → 19.70 | 18.97 → 19.03 |
| Fp square | 20.74 → 19.43 | 53.41 → 53.23 | 51.26 → 51.14 | 19.30 → 18.90 | 19.13 → 19.16 | 18.34 → 18.28 |
| Fp2 multiply | 78.58 → 66.50 | 195.12 → 160.68 | 186.33 → 187.78 | 62.91 → 62.91 | 62.70 → 62.28 | 60.56 → 61.15 |
| Fp2 square | 50.30 → 45.81 | 122.49 → 119.32 | 120.15 → 119.98 | 46.50 → 41.75 | 42.42 → 41.54 | 41.22 → 41.37 |
| Fp inverse | 1405.12 → 1352.22 | 1744.51 → 1712.16 | 1897.70 → 1901.09 | 1421.82 → 1398.26 | 1407.55 → 1381.43 | 1409.33 → 1383.36 |
| MultiplyWide | 12.77 → 9.53 | 26.98 → 21.56 | 27.85 → 28.19 | — | — | — |
| ReduceWide | 19.90 → 15.12 | 45.55 → 42.35 | 44.57 → 45.68 | — | — | — |
| ReduceWideSmall | 16.93 → 13.95 | 37.91 → 35.40 | 37.73 → 38.83 | — | — | — |
| AddWide | 3.99 → 1.94 | 6.03 → 6.04 | 5.99 → 6.08 | — | — | — |
| SubtractWide | 3.97 → 1.93 | 6.13 → 6.14 | 6.05 → 6.10 | — | — | — |
| AddUnreduced | 1.72 → 0.90 | 2.22 → 2.25 | 2.18 → 2.18 | — | — | — |
| Halve | 3.71 → 3.21 | 4.07 → 4.03 | 3.92 → 3.93 | — | — | — |
| Negate | 2.82 → 2.82 | 3.60 → 3.50 | 3.53 → 3.46 | 3.18 → 3.20 | 3.16 → 3.16 | 3.13 → 3.13 |
| SubtractSumComposite | 7.14 → 3.27 | 15.80 → 14.73 | 15.41 → 14.73 | — | — | — |
| Fp add | 3.36 → 2.11 | 6.78 → 6.23 | 6.60 → 6.23 | 2.93 → 2.95 | 3.28 → 3.10 | 3.27 → 3.08 |
| Fp subtract | 2.80 → 2.11 | 6.36 → 6.17 | 6.10 → 6.07 | 2.95 → 3.28 | 2.97 → 2.96 | 2.94 → 2.95 |
| MultiplyComposite | 21.47 → 20.44 | 54.74 → 53.66 | 53.64 → 53.37 | 20.37 → 19.74 | 19.83 → 19.64 | 19.02 → 18.96 |
| ReduceWidePositive | — → 12.93 | — → 30.06 | — → 37.62 | — | — | — |

Wide/unreduced/halve helpers have no matching exported blst ABI. The positive reducer is new. Stock .NET 10 keeps the original compact raw-product/reducer loops and three-CIOS Fp2 formula; selection is by target framework at compilation. Its timings remain within host noise, with a small fused-subtraction improvement. A net11.0 assembly uses the new formula on stock .NET 11 and patched runtimes.

## Disassembly

Static FullOpts counts follow. `setb`/`movzx` counts are identical in each listed method. This measures the captured kernels, not arbitrary large inline callers.

| Kernel | setb/movzx before → after | Bytes before → after | Static MULX after |
|---|---:|---:|---:|
| `Program:RunFpMultiply` | 1 → 0 | 1105 → 1075 | 24 |
| `Program:RunFpSquare` | 1 → 0 | 1055 → 1027 | 24 |
| `Nethermind.Crypto.BlsNet.Arithmetic.Fp:MultiplyComposite` | 1 → 0 | 1005 → 979 | 24 |
| `Nethermind.Crypto.BlsNet.Arithmetic.Fp:MultiplyWide` | 0 → 0 | 380 → 993 | 36 |
| `Nethermind.Crypto.BlsNet.Arithmetic.Fp:ReduceWide` | 13 → 0 | 894 → 658 | 6 |
| `Nethermind.Crypto.BlsNet.Arithmetic.Fp:ReduceWideSmall` | 7 → 0 | 662 → 553 | 6 |
| `Nethermind.Crypto.BlsNet.Arithmetic.Fp:ReduceWidePositive` | — → 0 | — → 443 | 6 |
| `Nethermind.Crypto.BlsNet.Arithmetic.Fp:AddWide` | 11 → 0 | 330 → 142 | 0 |
| `Nethermind.Crypto.BlsNet.Arithmetic.Fp:SubtractWide` | 11 → 0 | 330 → 142 | 0 |
| `Nethermind.Crypto.BlsNet.Arithmetic.Fp:SubtractWideProducts` | — → 0 | — → 424 | 0 |
| `Nethermind.Crypto.BlsNet.Arithmetic.Fp:SubtractComposite` | 1 → 0 | 248 → 188 | 0 |
| `Nethermind.Crypto.BlsNet.Arithmetic.Fp:SubtractSumComposite` | 17 → 0 | 631 → 334 | 0 |
| `Nethermind.Crypto.BlsNet.Arithmetic.Fp2:Multiply` | 10 → 0 | 502 → 358 | 0 |
| `Nethermind.Crypto.BlsNet.Arithmetic.Fp2:Square` | 10 → 0 | 465 → 306 | 0 |
| `Nethermind.Crypto.BlsNet.Arithmetic.Fp:Halve` | 5 → 0 | 243 → 184 | 0 |

Fp CIOS retains two rows per iteration: 24 static MULX execute three times, giving 72 per call. `MultiplyWide` expands all 36 products; reducers execute six static MULX six times. Fp2 multiply therefore executes **3×36 + 2×36 = 180 MULX**, matching blst's `mul_382x`/two-`redc` algorithm, versus 216 in the original three-CIOS formula. Comparison used the pinned perlasm sources in `external/blst/src/asm/`, particularly `mulx_mont_384-x86_64.pl` and `add_mod_384-x86_64.pl`.

Fp2 square retains two CIOS calls (144 dynamic MULX). Native Fp square computes cross products once. Zero materialized carries does not remove all allocation costs: managed kernels retain prologues/calls, stack scratch buffers, some operand spills, row-boundary moves and RDX reloads.

## JIT changes

The regenerated `jit/carry-rows.patch` retains the original patch and adds:

- Scalar first carry/borrow seeds in different locals after copy propagation, accepted only when every input definition is independently a bit. Arbitrary words and promoted input fields are rejected.
- CF/OF propagation across nonvolatile integer stores, excluding GC/write-barrier stores.
- Hoisting closed invariant AND/OR/XOR operand trees before the carry chain's root. Moving one merely ahead of an ADC/SBB would clobber its incoming flag; regression testing exposed and fixed that case.
- Final ADC/SBB folding in place when moving a load would cross an aliased store, provided surviving intervening instructions preserve flags.
- Shared invariant MULX multiplier orientation and result allocation excluding RDX; some reloads remain.
- Recomputed operand containment after existing multiply-carry loop/chain rewrites change operand roles. This fixes an existing checked-JIT assertion in `BigIntegerCalculator.MulAdd1` under tiering plus `JitStressRegs=1`, also reproduced with the original patch.
- A bounded extended carry-use scan allowance of 16,384 instead of 4,096 node visits, allowing the combined wide subtractions to retain flags. The full-adder size limit remains 2,048 nodes and the candidate limit remains 32.

`SeededMaskedCarry.cs` adds two regression entry points for masked add/subtract, non-bit inputs, dependent masks, constant multiply rows, overlapping stores and span multiply loops. It is copied to the supplied external `jittests` runner. The runner waits one second before reporting success to allow background Tier1 compilation. Temporary diagnostic printing was removed and is absent from the patch; `eng/build.ps1` is excluded.

The patch has no CR bytes. `git apply --check --cached` succeeds against an isolated index populated from `331fa2b3`, checking exact fresh-base contents without modifying the runtime checkout's index. Release and checked JIT builds succeed. Final release `clrjit.dll` SHA-256: `CE33640E302FE92C4BD55718EE79D35C444796733F3CA2101C8585F19F8489A2`.

## Correctness

- Solution Release build with the .NET 11 RC1 SDK: zero warnings/errors, both library targets.
- Arithmetic and native curve runners pass on .NET 10, .NET 11 and patched; 74,954 curve checks per runtime.
- Expanded pairing/tower runner: 811,043 checks on .NET 10; 841,151 on .NET 11, patched release and checked. The difference is direct tests of the net11.0-only in-place combiner.
- Kernel tests cover BigInteger and native Fp/Fp2 multiplication/squaring, 10,000 random cases, signed reducer bounds, all 6⁴ edge coefficient combinations, unreduced inputs, public aliases, overlapping raw-product/reducer buffers and every fused-subtraction input alias.
- All 14 checked-JIT entry points pass under default, `JitStress=2`, `JitStressRegs=1/3/8`, each with `TieredCompilation=0/1`: 140 successful entry-point runs.
- The 690,827-check kernel subset passes on the checked JIT under default, `JitStress=2` and `JitStressRegs=1/3/8` with tiering enabled.

No tower, curve, map or pairing algorithm files were changed. EIP-2537 integration benchmarks were not remeasured in this checkout.

## Reproduction and remaining work

`ReduceWidePositive` can help algorithm callers only with a proof of 0 ≤ value < pR. A raw Fp2 imaginary product satisfies this; arbitrary lazy tower sums/differences can be negative and need a signed reducer. `SubtractWideProducts` transforms three **distinct** product buffers in place. The report does not apply these changes to agent G's files.

```powershell
dotnet build Nethermind.Crypto.BlsNet.slnx -c Release
$env:DOTNET_ROLL_FORWARD = 'Major'
$env:DOTNET_TieredCompilation = '0'
$env:KERNEL_CPU = '11' # optional Windows logical CPU pin
# KERNEL_FILTER optionally selects a kernel substring.
& '<private-testhost>/dotnet.exe' 'tests/Nethermind.Crypto.BlsNet.PairingTests/bin/Release/net11.0/Nethermind.Crypto.BlsNet.PairingTests.dll' --kernel-timing
& '<private-testhost>/dotnet.exe' 'tests/Nethermind.Crypto.BlsNet.PairingTests/bin/Release/net11.0/Nethermind.Crypto.BlsNet.PairingTests.dll' --kernel-tests
```

Run the same net11.0 DLL on stock .NET 11. For .NET 10, use a temporary net10.0 console project compiling the same runner source and referencing the net10.0 library, retaining the assembly name for internal access. A .NET 10 launcher cannot retarget a net11.0 assembly. Local captures and temporary projects are in ignored `artifacts/field-study/`.

Discarded variants: three-row/fully expanded CIOS (about 30–54 ns patched instead of about 20 ns), cross-product Fp square (about 32 ns), wide Fp2 square (about 57 ns instead of about 46 ns), fully inlining raw products into Fp2 (more spills), combining both reductions in one method (no throughput gain), and expanded net10.0 kernels (substantial regressions). Broader calling/algorithm changes belong to the separate algorithm work. Further native parity work must address remaining register allocation, scratch and call costs rather than carry materialization.
