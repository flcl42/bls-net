# Field kernels, round 2

Kernel selection now follows the executing runtime rather than the target framework. A net10.0 library or linked source build gets compact kernels on stock .NET 10 and expanded kernels on .NET 11 and the patched .NET 12-dev runtime. Tier1 disassembly confirms constant folding of both the readonly policy field and the dispatcher. Cold calls from raw Montgomery data compile only the selected implementation.

Further changes fuse the real Fp2 product subtraction with reduction, inline one CIOS product in expanded Fp2 square, remove repeated RDX copies from carry rows, and express negation as a complemented add chain. Negation improves substantially; Fp2 gains are smaller and noisy. Fp square and Fp2 multiply/square remain slightly above their dedicated blst kernels. **Parity on every kernel is not achieved.**

## Selection and cold compilation

`Fp.UseExpandedKernels` defaults to `Environment.Version.Major >= 11`. The AppContext switch `Nethermind.Crypto.BlsNet.UseExpandedKernels` takes precedence over `BLSNET_EXPANDED_KERNELS`, which accepts `0`/`1` and `false`/`true` case-insensitively. Missing or unrecognized environment values use the runtime default. Set overrides before the first field use; selection is immutable after initialization.

A readonly field alone was insufficient for cold `AggressiveOptimization` entry points: a first Fp2 call from raw data compiled before Fp initialization, leaving both branches in permanent FullOpts code. Small inlineable dispatchers now read policy before calling distinct `NoInlining | AggressiveOptimization` implementations. The selected kernel compiles after initialization and has no policy branch. Ordinary Tier1 callers inline and fold the dispatcher. No framework guards remain in `src/` or `tests/`.

The G2 MSM threshold was the only remaining framework guard outside the field files. Its conversion is limited to using the same policy: expanded mode starts batch-affine accumulation at 128 points; compact mode retains XYZZ. G1 stays at 512. Tower, pairing, map and curve algorithms were otherwise unchanged. This threshold edit is included for integration into agent G's clone.

## Kernel timings

Numbers are ns per independent operation; **each cell is compact → expanded**. Runtimes are .NET 10.0.12, .NET 11.0.0-rc.1.26425.128, and patched .NET 12.0.0-dev at runtime `331fa2b3`. Both library targets are tested on every runtime that can load them. net11.0 cannot run on .NET 10.

Windows x64, Ryzen 9 9950X3D VM, logical CPU 11, tiering disabled. Each operation warms for at least 0.5 s, then uses the best of seven rounds of 1,000,000 calls (30,000 for inversion). Every managed measurement allocates zero bytes. Native imports suppress the GC transition. Builds, suites and timing processes ran sequentially. Native columns expose shared-host clock/contention variation; differences of a few percent are not reliable absolute speedups.

### Managed net10.0 build

| Kernel | stock10 / net10.0 | stock11 / net10.0 | patched / net10.0 |
|---|---:|---:|---:|
| Fp multiply | 51.10 → 50.96 | 51.73 → 51.41 | 18.98 → 19.55 |
| Fp square | 51.02 → 50.66 | 51.88 → 52.12 | 18.75 → 19.45 |
| Fp2 multiply | 187.62 → 261.70 | 183.56 → 157.92 | 66.78 → 64.17 |
| Fp2 square | 118.41 → 198.66 | 119.07 → 117.58 | 44.31 → 43.84 |
| Fp inverse | 1845.66 → 1864.61 | 1690.01 → 1706.61 | 1325.02 → 1344.00 |
| MultiplyWide | 27.69 → 21.74 | 25.89 → 21.24 | 12.03 → 9.46 |
| ReduceWide | 43.69 → 41.67 | 44.02 → 40.73 | 14.87 → 14.77 |
| ReduceWideSmall | 36.94 → 35.18 | 36.90 → 34.03 | 14.00 → 13.69 |
| AddWide | 5.86 → 5.87 | 5.92 → 5.93 | 1.89 → 1.93 |
| SubtractWide | 5.90 → 5.89 | 5.94 → 5.91 | 1.89 → 1.92 |
| AddUnreduced | 2.16 → 2.18 | 2.14 → 2.17 | 0.88 → 0.89 |
| Halve | 3.92 → 3.87 | 3.88 → 3.92 | 3.11 → 3.12 |
| Negate | 3.35 → 3.29 | 3.32 → 3.35 | 1.76 → 1.81 |
| SubtractSumComposite | 14.72 → 14.53 | 14.47 → 14.49 | 3.19 → 3.25 |
| Fp add | 6.07 → 6.04 | 6.03 → 5.98 | 2.07 → 2.11 |
| Fp subtract | 6.14 → 6.02 | 6.09 → 6.09 | 2.06 → 2.09 |
| MultiplyComposite | 53.00 → 52.88 | 52.07 → 52.24 | 19.55 → 20.22 |
| ReduceWidePositive | 37.27 → 30.75 | 36.18 → 29.10 | 14.04 → 12.45 |
| ReduceWideDifference | 40.61 → 40.37 | 39.22 → 39.68 | 14.46 → 14.65 |

### Managed net11.0 build

| Kernel | stock11 / net11.0 | patched / net11.0 |
|---|---:|---:|
| Fp multiply | 50.42 → 50.10 | 19.27 → 19.00 |
| Fp square | 50.97 → 52.03 | 19.17 → 18.88 |
| Fp2 multiply | 184.83 → 154.35 | 67.36 → 63.56 |
| Fp2 square | 117.80 → 114.57 | 45.10 → 43.43 |
| Fp inverse | 1690.69 → 1684.25 | 1346.28 → 1365.23 |
| MultiplyWide | 25.49 → 21.02 | 12.29 → 9.24 |
| ReduceWide | 42.97 → 40.61 | 15.23 → 14.74 |
| ReduceWideSmall | 36.10 → 33.81 | 14.49 → 13.53 |
| AddWide | 5.92 → 5.91 | 1.91 → 1.90 |
| SubtractWide | 5.90 → 5.91 | 1.94 → 1.90 |
| AddUnreduced | 2.14 → 2.16 | 0.89 → 0.88 |
| Halve | 3.87 → 3.88 | 3.19 → 3.13 |
| Negate | 3.33 → 3.30 | 1.80 → 1.77 |
| SubtractSumComposite | 14.58 → 14.43 | 3.21 → 3.21 |
| Fp add | 6.00 → 6.08 | 2.10 → 2.12 |
| Fp subtract | 6.09 → 6.01 | 2.07 → 2.05 |
| MultiplyComposite | 52.16 → 53.11 | 19.84 → 19.76 |
| ReduceWidePositive | 36.04 → 29.20 | 14.44 → 12.42 |
| ReduceWideDifference | 39.07 → 39.11 | 14.67 → 14.55 |

### Native blst in the same runs

| Kernel | stock10 / net10.0 | stock11 / net10.0 | patched / net10.0 | stock11 / net11.0 | patched / net11.0 |
|---|---:|---:|---:|---:|---:|
| Fp multiply | 18.96 → 18.90 | 19.22 → 19.27 | 19.08 → 19.24 | 19.01 → 19.04 | 19.13 → 19.01 |
| Fp square | 18.31 → 18.20 | 18.63 → 18.69 | 18.33 → 18.69 | 18.49 → 18.53 | 18.45 → 18.39 |
| Fp2 multiply | 60.53 → 60.67 | 61.62 → 61.29 | 60.29 → 62.07 | 61.34 → 60.27 | 60.83 → 61.89 |
| Fp2 square | 40.48 → 40.47 | 41.08 → 40.66 | 40.53 → 41.54 | 40.92 → 40.33 | 41.33 → 41.85 |
| Fp inverse | 1332.44 → 1339.38 | 1367.83 → 1352.87 | 1338.40 → 1349.60 | 1343.36 → 1336.11 | 1385.14 → 1359.48 |
| Negate | 3.13 → 3.10 | 3.10 → 3.13 | 3.09 → 3.15 | 3.11 → 3.09 | 3.13 → 3.11 |
| Fp add | 3.11 → 2.89 | 2.94 → 2.90 | 2.88 → 2.93 | 2.89 → 2.88 | 2.89 → 2.91 |
| Fp subtract | 2.95 → 2.93 | 2.92 → 2.90 | 2.91 → 2.95 | 2.91 → 2.89 | 2.91 → 2.90 |
| MultiplyComposite | 19.00 → 19.00 | 19.02 → 19.00 | 19.00 → 19.46 | 18.97 → 19.02 | 19.38 → 19.20 |

Expanded mode is intentionally available for testing on stock .NET 10, where Fp2 is much slower. The shared runtime policy retains the faster compact formula there. On the patched runtime it selects the faster expanded formula even for a net10.0 build.

Four interleaved before/after captures compared the round 1 kernels and JIT with the round 2 Fp2 kernels and RDX-copy change, on net10.0 with expanded mode forced in both. The baseline recompiled round 1 algorithms with the runtime selector to compare the same formula. Median managed/native ratios changed from 1.0524 to 1.0445 for Fp2 multiply and from 1.0875 to 1.0613 for Fp2 square: roughly 0.8% and 2.4% relative improvements. Individual ns captures vary in either direction. The large net10.0 selection correction and negation change are clearer than these small Fp2 gains. The earlier round 1 results remain in [the historical report](field-kernel-report.md).

## C# changes and discarded experiments

`ReduceWideDifference` requires |a-b| < pR. It subtracts two raw products inside the reducer, so the real difference is not written to and reread from a twelve-word product buffer. `SubtractWideImaginary` modifies the third product while preserving the first two. The three products precede output writes, preserving aliases. Multiplication still performs 108 raw-product MULX and 72 reduction MULX dynamically, versus 216 in compact mode.

Expanded Fp2 square inlines the imaginary CIOS product and calls the isolated real product. It saves one call and three combined callee-save pushes relative to the previous caller plus two multiply frames. Its body contains 24 static MULX, executed three times, plus 72 in the called product: 144 total, unchanged. Compact square retains the isolated products because stock .NET 10 spills the larger inline body.

Negation uses p+~a+1 modulo 2^384, absorbs the one into the first modulus limb, and explicitly seeds carry from that first addition. Remaining words use `AddCarry`; the existing nonzero mask maps zero to zero. This avoids constant-left subtractions turning into NEG/ADD and per-word comparisons in inline contexts. Patched captures improve from about 2.7 to 1.8 ns; stock captures improve about 3.5 to 3.3 ns. The first prototype with `carry = 1` did not expose the initial carry as a proven bit and retained five materializations, so it was replaced by the explicit first-word seed.

Discarded experiments: an explicit RDX local preference did not change the reloads; copying the right operand and writing raw products directly increased MultiplyWide from about 9.4 to 11.1 ns; inlining both imaginary subtractions restored 22 SETB/MOVZX pairs and increased Fp2 multiply to about 75.8 ns; inlining square's subtraction added a materialized mask and no reliable gain; reversing square product order showed no gain and increased its frame from 168 to 200 bytes. Previous dedicated Fp square, fully expanded CIOS, fully fused raw Fp2 and paired reducers also remained slower or indistinguishable. The retained kernels keep bounded JIT analysis and compact calls where more fusion increases spills.

## Disassembly

The following are net10.0 captures on the patched release JIT. Counts describe method bodies; looped MULX execute multiple times. “RDX copies” counts register-to-RDX MOV immediately preceding MULX. Before is the round 1 kernel/JIT shape; after is round 2.

| Kernel body | Bytes before → after | SETB/MOVZX after | Static MULX | RDX copies before → after |
|---|---:|---:|---:|---:|
| RunFpMultiply | 1075 → 1045 | 0/0 | 24 | 12 → 2 |
| RunFpSquare | 1027 → 1012 | 0/0 | 24 | 6 → 1 |
| MultiplyComposite | 979 → 949 | 0/0 | 24 | 12 → 2 |
| MultiplyWideExpanded | 993 → 993 | 0/0 | 36 | 0 → 0 |
| ReduceWideExpanded | 658 → 643 | 0/0 | 6 | 6 → 1 |
| ReduceWideSmallExpanded | 553 → 538 | 0/0 | 6 | 6 → 1 |
| ReduceWidePositiveExpanded | 443 → 428 | 0/0 | 6 | 6 → 1 |
| ReduceWideDifference | new → 624 | 0/0 | 6 | new → 0 |
| SubtractWideImaginary | new → 283 | 0/0 | 0 | 0 |
| Fp2 MultiplyExpanded | 358 → 366 | 0/0 | 0, calls products/reducers | 0 |
| Fp2 SquareExpanded | 306 → 1150 | 0/0 | 24, plus called product | 0 → 2 |
| AddWide / SubtractWide | 142 → 142 each | 0/0 | 0 | 0 |
| SubtractWideProducts | 424 → 424 | 0/0 | 0 | 0 |
| SubtractComposite | 188 → 188 | 0/0 | 0 | 0 |
| SubtractSumComposite | 334 → 334 | 0/0 | 0 | 0 |
| Halve | 184 → 184 | 0/0 | 0 | 0 |

Forced compact `ReduceWideCompact` and `ReduceWideSmallCompact` each retain one SETB/MOVZX pair for the loop-carried top bit; the default expanded reducers avoid it.

The inlined negation region loses five SETA/MOVZX pairs and uses an ADD/ADC chain with complements. The multi-case timing method still contains two SETB/MOVZX pairs in Fp add/subtract's final mask selections, where the selections span basic blocks. Isolated correction kernels above use CMOV. Zero counts above do not imply every possible large inline caller stays within the JIT budgets or block-local selection proof.

With tiering enabled and `DOTNET_TC_CallCountingDelayMs=0`, both modes and all five runtime/framework combinations were captured. `ReadKernelPolicy` Tier1 is `xor eax,eax; ret` (3 bytes) for compact or `mov eax,1; ret` (6 bytes) for expanded. `MultiplyWithPolicy` Tier1 is 16 bytes and calls only `MultiplyCompact` or only `MultiplyExpanded`, with no field read or selection branch. Default net10.0 cold captures on stock .NET 10 and patched, and both forced choices, compile only the chosen Fp2 implementations. Kernel implementation bodies contain no policy branch.

## JIT changes and regression coverage

The RDX emitter peephole removes a repeated 64-bit register copy only after seeing MULX, through known MOV/MULX/add/subtract/carry/test instructions, with neither register written. The existing safe-region iterator bounds it to 32 instructions and respects instruction-group boundaries. Partial writes, MULX output writes, calls, branches and unknown instructions stop the search. It retains the allocator's assignments and changes no flag instruction. `MulxMultiplierCopies` tests 5,000 randomized repeated rows, a partial-width multiplier update, a call and register pressure.

Integer NOT is accepted as preserving CF/OF on x64. A complement's closed operand tree can be moved before the flags producer only with the existing side-effect, alias, exception-order and local-definition checks; carry-consuming producers still use the established flag-chain root logic. `ComplementedCarry` tests 5,000 randomized 32/64-bit cases, dependent complements, exact aliases and shifted aliases whose stores change later reads. This change does not raise analysis budgets.

The regenerated `jit/carry-rows.patch` is LF-only and passes `git apply --check --cached` against a fresh index populated from `331fa2b3`. The unrelated `eng/build.ps1` change is excluded. Release and checked JITs built successfully and were installed only in private hosts during development.

## Validation and reproduction

Both solution builds (`BlsNetTestTargetFramework=net10.0` and the default net11.0) finish with zero warnings and errors. The complete matrix passes all three console suites, separate kernel tests, and cold-start tests in both modes for net10.0 on .NET 10/.NET 11/patched and net11.0 on .NET 11/patched. Each run reports 951,325 pairing/tower checks, 801,001 kernel checks, 74,954 curve checks, and all managed arithmetic groups passing. Native comparisons are enabled.

The final checked JIT passes all 16 regression entry points in default, `JitStress=2`, and `JitStressRegs=1/3/8`, with tiering both off and on: 160 entry-point runs. Both target builds and both modes also pass kernel tests in all ten configurations: 40 runs of 801,001 checks. All three full suites pass under the checked JIT in both target builds and both modes. The external regression runner waits one second before success to allow background Tier1 compilation and returns 100 on success.

The policy diagnostics pass 40 combinations covering defaults, numeric and case-insensitive Boolean environment values, invalid-value fallback, and both AppContext overrides against conflicting environment settings. The separate Tier1 captures verify both choices for every supported runtime/framework combination. Added kernel coverage checks the fused reducer against BigInteger at signed boundaries and random differences, overlapping output with either wide input, imaginary-input preservation, and negation against BigInteger/native blst with aliases. Existing exhaustive Fp2 coefficient edges, signed/unreduced products, inversion, curve, MSM, map, tower and pairing comparisons remain enabled.

```powershell
./scripts/test-field-runtime-matrix.ps1 `
  -SdkDotnet '<dotnet11>/dotnet.exe' `
  -Stock10Dotnet 'C:/Program Files/dotnet/dotnet.exe' `
  -Stock11Dotnet '<dotnet11>/dotnet.exe' `
  -PatchedDotnet '<private-testhost>/dotnet.exe' -Timings -Cpu 11

$env:DOTNET_ROLL_FORWARD = 'Major'
$env:DOTNET_TieredCompilation = '1'
$env:DOTNET_TC_CallCountingDelayMs = '0'
$env:BLSNET_EXPANDED_KERNELS = '1' # repeat with 0
$env:DOTNET_JitDisasm = 'Program:ReadKernelPolicy Program:MultiplyWithPolicy'
dotnet tests/Nethermind.Crypto.BlsNet.PairingTests/bin/Release/net10.0/Nethermind.Crypto.BlsNet.PairingTests.dll --kernel-policy
```

Raw logs, timings and disassembly are under the ignored `artifacts/field-study/round2/` directory. The full C# matrix is in `complete-matrix/`; final checked runs use `complete-jit-*` and `complete-checked-*`. Historical captures remain available alongside them.

## Delivery

The implementation and this report are committed locally; no push is performed. The round 1 parent is `5369bab`; the round 2 commit is the commit containing this report and is recorded explicitly in `C:/Temp/blst-speedy/agentF-final.md`. That report is published before final shared-testhost installation so agent G can integrate the kernel and threshold changes.

Final release JIT SHA-256: `11E3B0E0395C4625721E074B7E9615DE43F0EAC98171F30938EA5B3ABCA6819F`. The final shared host receives this validated JIT at completion, after report publication, with hash verification and a net10.0 default-policy kernel smoke run. Stock runtimes use their original JITs.
