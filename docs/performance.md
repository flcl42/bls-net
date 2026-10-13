# Performance

The [round 3 field and JIT report](field-kernel-round3-report.md) measures the
merged algorithm/field baseline at `75a7ccc` against symmetric Fp squaring,
direct disjoint-output raw products, inlined Fp2 subtraction and larger bounded
carry-analysis allowances. It includes kernels and tower/pairing/curve/map/MSM
paths on all three runtimes. Earlier tables below retain their original code
and JIT revisions.

The subsequent [algorithm-layer follow-up](agentG-report.md) records three-runtime before/after measurements, native Fp6 timings, full MSM sweeps, integration profiles, local commits and validation. It includes compressed final-exponentiation powers, an early return when the easy part yields one, and endomorphism-derived multiplication/MSM tables. The [operation-count audit](algorithm-audit.md) explains both the gains and the remaining gaps. The historical measurements below are retained as their original captures.

The follow-up uses Agent F's final shared JIT update for both patched baseline and final captures, with Fp/Fp2 source changes still unmerged. General final exponentiation improves by about 9%, G2 multiplication by about 29%, and all sampled patched MSM sizes beat their same-process native timings. General pairing and maps still retain gaps; the full report distinguishes those paths from repeated-point MSMs and cancelling-pair precompile inputs.

On stock .NET 10, the managed EIP-2537 precompiles are about 1.8–4× slower than blst. With the experimental carry-aware JIT, G1 operations take 0.83–1.02× blst's time, G2 addition 0.81×, G2 MSM about 1.00–1.12×, maps 1.06–1.12× and pairing checks 1.15–1.19×. Most operations reach or approach parity; pairing retains a 15–19% gap. Ratios are managed time divided by blst time, so lower is faster.

The main gap is carry handling. In the latest field-kernel measurements, stock RyuJIT emits MULX but materializes carry bits with comparisons, `setb` and `movzx` rather than retaining ADC/ADCX/ADOX chains. The carry-aware patch preserves these chains and flag-based reductions. An Fp multiply falls from about 53 ns on stock .NET 10/11 to about 20 ns, close to native ADX blst. Large formulas also need compact arithmetic calls to avoid JIT budgets and register pressure.

## EIP-2537 precompiles

These results use Nethermind's BenchmarkDotNet precompile benchmarks, an in-process toolchain and the Short job on a Ryzen 9 9950X3D VM running Windows 11. The native baseline is Nethermind.Crypto.Bls 0.3.17.15 over blst. The managed variants use the same Nethermind precompile code with the math replaced by this library. The patched runtime is .NET 12.0.0-dev built from dotnet/runtime PR #134039 at `331fa2b3` plus [`jit/carry-rows.patch`](../jit/carry-rows.patch).

The table uses BenchmarkDotNet means, with times rounded to the supplied report precision. Ratios are the reported values and can differ from division of the displayed timings. The displayed blst column is the .NET 10 run; native timings agreed between runs within about ±6% (patched/stock mean ratio 1.005, range 0.951–1.063). Units appear in every timing cell. MSM input sizes count point/scalar pairs; pairing sizes count G1/G2 pairs. The g1mul/g2mul cases measure the single-multiplication benchmark inputs. For addition, input size 1 means one addition of two points.

| Precompile | Points / pairs | blst | Managed .NET 10 | Ratio | Managed patched JIT | Ratio |
|---|---:|---:|---:|---:|---:|---:|
| G1ADD | 1 | 2.5 µs | 4.8 µs | 1.95x | 2.3 µs | 0.93x |
| G1MSM | 1 | 81.2 µs | 321.1 µs | 3.95x | 75.5 µs | 0.93x |
| G1MSM | 2 | 182.4 µs | 613.4 µs | 3.36x | 180.4 µs | 0.99x |
| G1MSM | 8 | 398.8 µs | 1.27 ms | 3.18x | 365.6 µs | 0.92x |
| G1MSM | 32 | 1.03 ms | 3.97 ms | 3.86x | 1.05 ms | 1.02x |
| G1MSM | 64 | 1.61 ms | 6.27 ms | 3.90x | 1.64 ms | 1.02x |
| G1MSM | 128 | 2.72 ms | 10.26 ms | 3.77x | 2.67 ms | 0.98x |
| G1MSM | 256 | 4.53 ms | 17.52 ms | 3.87x | 4.49 ms | 0.99x |
| G1MSM | 512 | 7.48 ms | 24.98 ms | 3.34x | 7.03 ms | 0.94x |
| G1MSM | 2048 | 23.68 ms | 76.43 ms | 3.23x | 19.72 ms | 0.83x |
| G1MSM | 4096 | 42.29 ms | 140.79 ms | 3.33x | 36.18 ms | 0.86x |
| G1 mult (g1mul) | 1 | 81.2 µs | 321.3 µs | 3.96x | 76.5 µs | 0.94x |
| G2ADD | 1 | 4.1 µs | 7.4 µs | 1.81x | 3.4 µs | 0.81x |
| G2MSM | 1 | 153.0 µs | 423.8 µs | 2.77x | 161.9 µs | 1.06x |
| G2MSM | 2 | 364.6 µs | 892.2 µs | 2.45x | 387.3 µs | 1.06x |
| G2MSM | 8 | 828.3 µs | 2.02 ms | 2.44x | 828.9 µs | 1.00x |
| G2MSM | 32 | 2.46 ms | 6.86 ms | 2.78x | 2.76 ms | 1.12x |
| G2MSM | 64 | 3.95 ms | 10.87 ms | 2.76x | 4.40 ms | 1.12x |
| G2MSM | 128 | 6.52 ms | 18.25 ms | 2.80x | 7.16 ms | 1.10x |
| G2MSM | 256 | 10.85 ms | 30.56 ms | 2.82x | 12.08 ms | 1.11x |
| G2MSM | 512 | 18.37 ms | 52.81 ms | 2.88x | 20.28 ms | 1.10x |
| G2MSM | 2048 | 56.62 ms | 167.24 ms | 2.95x | 63.44 ms | 1.12x |
| G2MSM | 4096 | 103.40 ms | 303.89 ms | 2.94x | 114.62 ms | 1.11x |
| G2 mult (g2mul) | 1 | 146.6 µs | 427.4 µs | 2.92x | 160.1 µs | 1.09x |
| MAP_FP_TO_G1 | 1 | 28.2 µs | 107.8 µs | 3.82x | 30.0 µs | 1.06x |
| MAP_FP2_TO_G2 | 1 | 99.1 µs | 297.4 µs | 3.00x | 110.7 µs | 1.12x |
| PAIRING_CHECK | 1 | 464.7 µs | 1.37 ms | 2.96x | 544.6 µs | 1.17x |
| PAIRING_CHECK | 2 | 678.1 µs | 1.85 ms | 2.73x | 780.6 µs | 1.15x |
| PAIRING_CHECK | 4 | 909.2 µs | 2.63 ms | 2.89x | 1.08 ms | 1.19x |
| PAIRING_CHECK | 8 | 1.45 ms | 4.17 ms | 2.87x | 1.72 ms | 1.18x |

### Reproducing the integration benchmarks

The integration lives in a Nethermind branch that is not published yet. This repository contains component runners; the precompile table also requires the Nethermind integration and its benchmark inputs.

The integration links `src/Nethermind.Crypto.BlsNet/**/*.cs` into a Nethermind library project, excluding `bin/` and `obj/`. It references that project from the managed precompile variants and benchmark projects. Keep the existing precompile parsing, validation and output encoding, replace the arithmetic calls, and register the `ManagedBls12381*Benchmark` classes alongside the native `Bls12381*Benchmark` classes. Scalar byte order and padded coordinate conversion remain in the precompile layer.

From the integrated Nethermind checkout, build and run the runner with the in-process toolchain:

```powershell
dotnet run --project src/Nethermind/Nethermind.Benchmark.Runner -c Release -- --filter '*Bls12381*' --job Short --inProcess
```

Build with `BlsNetSource` set to the library source directory if the linked project needs a path override. Run the same built runner and filter under the patched runtime, using `DOTNET_ROOT` and `DOTNET_ROLL_FORWARD=Major` as below. Include native benchmarks in both runs. The in-process toolchain is required to measure the runtime that launches the runner. The filter also includes managed variants. Record the executing runtime, target framework and any `BLSNET_EXPANDED_KERNELS` override: field kernels and the G2 batch-affine threshold now follow runtime policy.

## Component measurements

The [field-kernel follow-up report](field-kernel-report.md) records newer before/after kernel timings, complete disassembly counts and stress validation. It reduces patched Fp2 multiplication to three raw products and two Montgomery reductions, eliminates carry materialization in the captured field kernels, and preserves compact stock .NET 10 kernels. The [round 2 report](field-kernel-round2-report.md) replaces framework selection with a folded runtime policy and records both modes on both library targets, further kernel experiments, and the RDX-copy JIT change. The historical component tables below precede that follow-up. They do not measure the later tower/curve work in other checkouts.

The following tables retain the latest implementation follow-up measurements. They use stock .NET 10.0.12, stock .NET 11 RC1 and the patched launcher identifying itself as .NET 12.0.0-dev. Each native column comes from the same runtime and loop shape as its managed counterpart. These are warmed best-of-round component timings, measured separately from the BenchmarkDotNet precompile means. Input construction, validation, conversion and harness costs differ, so the two sets should not be compared as identical operations. Shared-host contention limits small comparisons.

### Field operations

| Operation | Managed .NET 10 | blst .NET 10 | Managed .NET 11 | blst .NET 11 | Managed patched | blst patched |
|---|---:|---:|---:|---:|---:|---:|
| Fp multiply | 53.16 ns | 21.17 ns | 52.56 ns | 21.01 ns | 20.32 ns | 20.97 ns |
| Fp square | 53.19 ns | 21.61 ns | 52.67 ns | 21.27 ns | 20.72 ns | 21.18 ns |
| Fp inverse | 1.936 µs | 1.362 µs | 1.772 µs | 1.401 µs | 1.359 µs | 1.404 µs |
| Fp2 multiply | 191.63 ns | 64.47 ns | 189.87 ns | 63.67 ns | 78.78 ns | 64.02 ns |

Binary-GCD inversion is about 27× faster than Fermat on .NET 10, 19× on .NET 11 and 16× on the patched runtime in these runs. The Fp square native reference in this historical table is `blst_fp_mul(a, a)`. The follow-up kernel runner uses the dedicated `blst_fp_sqr` entry point and also measures `blst_fp2_sqr`.

### Extension tower

| Operation | Managed .NET 10 | blst .NET 10 | Managed .NET 11 | blst .NET 11 | Managed patched | blst patched |
|---|---:|---:|---:|---:|---:|---:|
| Fp12 multiply | 3.562 µs | 1.193 µs | 3.378 µs | 1.214 µs | 1.752 µs | 1.204 µs |
| Fp12 square | 2.652 µs | 0.874 µs | 2.475 µs | 0.863 µs | 1.233 µs | 0.871 µs |

The earlier matched tower run also measured cyclotomic square: managed .NET 11 1.530 µs versus blst 0.517 µs, and managed patched 0.676 µs versus blst 0.509 µs. A matching .NET 10 measurement was not reported. This earlier run used a different capture; its values should not be merged with the latest table as one measurement.

### Pairing

| Operation | Managed .NET 10 | blst .NET 10 | Managed .NET 11 | blst .NET 11 | Managed patched | blst patched |
|---|---:|---:|---:|---:|---:|---:|
| Miller 1 | 508.825 µs | 155.305 µs | 448.956 µs | 154.585 µs | 219.070 µs | 155.152 µs |
| Miller 8 | 2844.978 µs | 850.385 µs | 2542.886 µs | 869.286 µs | 1213.107 µs | 869.722 µs |
| Final exponentiation | 732.517 µs | 209.575 µs | 634.132 µs | 210.209 µs | 285.962 µs | 212.982 µs |
| Pairing check 1 | 1222.481 µs | 365.932 µs | 1096.583 µs | 369.095 µs | 501.583 µs | 369.272 µs |
| Pairing check 2 | 1622.740 µs | 463.449 µs | 1423.978 µs | 475.302 µs | 648.168 µs | 471.365 µs |
| Pairing check 8 | 3584.570 µs | 1168.589 µs | 3212.342 µs | 1093.758 µs | 1494.303 µs | 1087.320 µs |

The tower/pairing harness warms each operation for at least 0.5 s and takes the best of seven rounds. Arithmetic rounds run 30,000 iterations; pairing rounds run 250. Inputs are deterministic. Native full checks combine `blst_miller_loop_n`, `blst_final_exp` and the same `IsOne` comparison. Measured managed loops allocate zero heap bytes.

### Curve operations

The curve harness also warms for at least 0.5 s and takes the best of seven rounds with deterministic inputs. Managed and native use the same Action/Stopwatch loop shape. Native MSM scratch memory and pointer arrays are reused outside measurement. Map and scalar multiplication timings include projective output; add + affine includes affine conversion. Values below are the final values from the latest before/after sweep, in µs per operation.

| Operation | Managed .NET 10 | blst .NET 10 | Managed .NET 11 | blst .NET 11 | Managed patched | blst patched |
|---|---:|---:|---:|---:|---:|---:|
| G1 add+affine | 3.938 | 1.862 | 2.879 | 1.772 | 1.815 | 1.761 |
| G1 subgroup | 120.916 | 25.484 | 69.423 | 24.487 | 27.810 | 24.266 |
| G1 multiply | 199.464 | 48.010 | 113.116 | 46.719 | 46.093 | 46.072 |
| map G1 | 102.887 | 23.723 | 70.265 | 23.170 | 27.815 | 23.110 |
| G2 add+affine | 6.286 | 2.759 | 5.526 | 2.656 | 2.902 | 2.623 |
| G2 subgroup | 99.448 | 30.097 | 84.413 | 29.039 | 36.661 | 28.702 |
| G2 multiply | 327.485 | 97.123 | 286.195 | 96.061 | 121.709 | 93.358 |
| map G2 | 289.441 | 83.337 | 254.424 | 82.016 | 107.201 | 80.829 |

### MSM sweep

Values are µs per operation. Warmed single multiplication and MSM allocate zero heap bytes at the checked sizes; initial pool use can allocate. n = 1 uses scalar multiplication, n < 32 uses signed width-4 tables, and larger inputs use Pippenger. The historical captures used a framework-dependent G2 threshold. Current batch-affine buckets begin at n = 512 for G1 and n = 128 for G2 in expanded mode. Compact mode retains XYZZ G2 buckets at all sizes; both modes are selected by the executing runtime. See [design](design.md#multi-scalar-multiplication).

#### G1

| Operation | Managed .NET 10 | blst .NET 10 | Managed .NET 11 | blst .NET 11 | Managed patched | blst patched |
|---|---:|---:|---:|---:|---:|---:|
| G1 MSM 1 | 191.693 | 69.003 | 110.761 | 66.875 | 45.582 | 67.468 |
| G1 MSM 2 | 382.189 | 87.917 | 219.741 | 85.605 | 88.679 | 84.505 |
| G1 MSM 4 | 555.669 | 131.669 | 315.224 | 128.189 | 126.878 | 126.839 |
| G1 MSM 8 | 886.168 | 220.844 | 488.845 | 211.712 | 197.845 | 211.239 |
| G1 MSM 16 | 1561.328 | 407.137 | 862.360 | 392.318 | 349.820 | 388.940 |
| G1 MSM 32 | 3088.516 | 645.495 | 1723.362 | 619.909 | 712.519 | 620.333 |
| G1 MSM 64 | 4957.169 | 1033.869 | 2807.019 | 1007.620 | 1153.958 | 1007.572 |
| G1 MSM 128 | 8292.016 | 1722.897 | 4648.978 | 1664.019 | 1890.784 | 1648.769 |
| G1 MSM 256 | 14143.044 | 2960.794 | 8076.675 | 2863.938 | 3247.812 | 2810.113 |
| G1 MSM 512 | 18738.825 | 4969.288 | 10823.025 | 4846.775 | 5089.500 | 4780.688 |
| G1 MSM 1024 | 31618.650 | 8784.300 | 17850.950 | 8545.350 | 8219.600 | 8457.500 |
| G1 MSM 2048 | 55334.200 | 15569.000 | 31281.950 | 15184.550 | 13938.150 | 15092.300 |
| G1 MSM 4096 | 96916.000 | 27940.700 | 54897.100 | 27290.900 | 24394.700 | 27144.000 |

#### G2

| Operation | Managed .NET 10 | blst .NET 10 | Managed .NET 11 | blst .NET 11 | Managed patched | blst patched |
|---|---:|---:|---:|---:|---:|---:|
| G2 MSM 1 | 328.941 | 163.267 | 282.602 | 161.952 | 120.589 | 161.957 |
| G2 MSM 2 | 694.040 | 212.596 | 583.282 | 209.991 | 255.181 | 208.612 |
| G2 MSM 4 | 1015.791 | 331.770 | 859.941 | 326.929 | 367.993 | 319.791 |
| G2 MSM 8 | 1671.307 | 554.335 | 1426.240 | 544.695 | 607.620 | 541.157 |
| G2 MSM 16 | 2957.365 | 899.648 | 2560.842 | 900.792 | 1083.604 | 875.924 |
| G2 MSM 32 | 6122.801 | 1825.011 | 5327.488 | 1780.047 | 2298.349 | 1763.847 |
| G2 MSM 64 | 9928.384 | 2938.973 | 8581.144 | 2877.908 | 3699.581 | 2858.889 |
| G2 MSM 128 | 16636.866 | 4972.056 | 11528.797 | 4747.822 | 5472.484 | 4852.884 |
| G2 MSM 256 | 27768.812 | 8229.094 | 18488.606 | 8129.962 | 8453.531 | 8259.081 |
| G2 MSM 512 | 47159.338 | 14003.612 | 32211.213 | 14072.138 | 13888.475 | 13912.513 |
| G2 MSM 1024 | 85078.100 | 24989.375 | 52785.350 | 24335.350 | 23205.025 | 24244.425 |
| G2 MSM 2048 | 148462.900 | 43705.050 | 92788.400 | 43337.800 | 40481.400 | 43039.950 |
| G2 MSM 4096 | 267497.400 | 79657.400 | 163603.400 | 77388.900 | 72540.300 | 77217.300 |

In this component sweep, patched G1 MSM is faster than blst at n ≥ 1,024; n = 64–512 takes about 1.06–1.16× blst's time. Patched G2 MSM is faster at n ≥ 256; n = 64 and 128 take about 1.29× and 1.13×. At n = 4,096, the follow-up reduced patched G1 from 32.4 ms to 24.4 ms and G2 from 103.7 ms to 72.5 ms. Stock .NET 11 improved from 78.3 ms to 54.9 ms for G1 and 243.0 ms to 163.6 ms for G2. Stock .NET 10 improved from 164.4 ms to 96.9 ms for G1 and 285.8 ms to 267.5 ms for G2.

### Running the component runners

Use the .NET 11 SDK selected by `global.json`:

```powershell
dotnet run --project benchmarks/Nethermind.Crypto.BlsNet.Benchmarks -c Release
dotnet run --project tests/Nethermind.Crypto.BlsNet.CurveTests -c Release -f net11.0 -- --bench
dotnet run --project tests/Nethermind.Crypto.BlsNet.PairingTests -c Release -f net11.0 -- --timing
dotnet run --project tests/Nethermind.Crypto.BlsNet.PairingTests -c Release -f net11.0 -- --kernel-timing
```

The test projects default to net11.0. Build the net10.0 runners with `dotnet build Nethermind.Crypto.BlsNet.slnx -c Release -p:BlsNetTestTargetFramework=net10.0`; changing only the launcher does not retarget the assembly. `scripts/test-field-runtime-matrix.ps1` builds both targets and runs all three suites, kernel tests and optional timing sweeps sequentially, with both kernel modes on every compatible runtime. Native comparisons require the optional reference build described in the [README](../README.md#build-and-test). Without it, managed timings still run and native columns are skipped.

On a shared Windows host, `BLST_BENCH_PROCESSOR` optionally pins the runners to one logical processor. `BLST_BENCH_ROUND_MS` enables shorter arithmetic/curve rounds and best-of-21 sampling to reduce scheduler interruptions; default runner settings retain the historical best-of-seven mode. Curve rounds adapt their iteration count toward the supplied millisecond duration. Pairing short rounds use 1,000 tower iterations and 10 pairing iterations, with the same counts for native and managed operations. Warmup remains at least 0.5 seconds per operation.

## Kernel and JIT details

The `mul+add` benchmarks (`acc = acc * a + b`, 1,000,000 dependent iterations; the runner
reports the best of 7 rounds, the table the best of five interleaved runs) on a Ryzen 9
9950X3D, in ns per iteration; runs vary by about 5% on this machine:

| Kernel     | .NET 11 RC1 | #134039 | #134039 + patch | blst (ADX) via P/Invoke | blst (ADX), C loop |
|------------|------------:|--------:|----------------:|------------------------:|-------------------:|
| Fr mul+add |        26.4 |    17.7 |            10.9 |                    12.1 |               11.9 |
| Fp mul+add |        56.7 |    33.8 |            19.8 |                    20.4 |               20.4 |

The native columns call blst's MULX/ADCX/ADOX assembly, through `[SuppressGCTransition]`
imports and from a C loop over `libblst.a`. Without ADX (blst's MULQ assembly) that C loop
takes 13.2 and 24.8 ns. The `#134039` columns are the carry-aware JIT described below,
without and with the patch. With the patch the managed loop was faster in every run,
by 8-10% for Fr and 2-4% for Fp. Multiplication alone is at parity for Fp (18.7 ns against
18.6 in C) and about 7% faster for Fr (9.8 against 10.6); the rest comes from the addition,
which the JIT inlines and reduces with `cmov` while blst calls a function. The numbers are
for this isolated loop; code that inlines many more operations into one method can exceed
the JIT budgets below.

The older disassembly capture described MUL lowering for `Math.BigMul`. The latest
.NET 10/11 kernel captures use MULX, but still materialize carries with compare/`setb`/`movzx`
and registers rather than dual ADCX/ADOX chains. MULX alone does not close the 2–3× field gap. The kernels are written in carry idioms that a carry-aware JIT turns into `adc`/`sbb`
and MULX/ADCX/ADOX chains:

- multiply-accumulate: `hi = Math.BigMul(m, x, out lo); lo += t; hi += lo < t ? 1 : 0; lo += c; hi += lo < c ? 1 : 0;`
  with the multiplier shared by a row of products first (MULX takes it in RDX);
- add with carry: `s = a + b; c1 = s < a ? 1 : 0; r = s + carry; carry = c1 + (r < s ? 1 : 0);`
- subtract with borrow: `d = a - b; b1 = d > a ? 1 : 0; r = d - borrow; borrow = b1 + (r > d ? 1 : 0);`
- the final conditional subtraction adds `2^n - modulus`, whose carry out means "not below the modulus".

The historical measurements used the experimental [dotnet/runtime#134039](https://github.com/dotnet/runtime/pull/134039),
which recognizes the add/subtract idioms and BigInteger-style multiply-accumulate loops, at
`331fa2b3` plus [`carry-rows.patch`](../jit/carry-rows.patch), which:

- keeps CF and OF across the rows of a Montgomery multiplication, the `MulAdd` shape above
  (several rows per block, rows of any width, the shorter first lanes of CIOS rows);
- lets loads, `lea`, non-zero constants and casts sit between a carry and its consumer;
- turns the constant-time select `t ^ ((t ^ s) & (0 - carry))` after a carry or borrow chain
  into `cmov`s that read the chain's flags, so the final conditional subtraction no longer
  turns the carry into a mask (`setb; movzx; neg`, then `xor; and; xor` per limb, all on the
  critical path);
- keeps following a carry to its single consumer in large blocks, where the PR's scan gave up
  after 64 nodes and left the last limb of a long chain going through a register;
- also optimizes OSR methods, whose Tier0 frame can only hold root-method locals, not carry temporaries.

The PR skips methods with exception handling and gives up on full adders in methods larger
than 2,048 IR nodes. A fully unrolled 6-limb row loop exceeds that budget, so `Fp.Multiply`
keeps a loop over rows, two rows per iteration: the twelve live words leave few spare
registers, and each back edge costs register shuffles (about 4% of an Fp mul+add; on the
stock .NET 11 RC1 JIT the two-row loop is about 1% slower).

To reproduce, use a separate dotnet/runtime checkout at `<runtime>` and this repository at `<BlsNet>`. Build the library benchmark with the .NET 11 SDK first, then build the experimental runtime and run that assembly on its testhost:

```powershell
dotnet build '<BlsNet>/benchmarks/Nethermind.Crypto.BlsNet.Benchmarks' -c Release
git -C '<runtime>' fetch https://github.com/dotnet/runtime pull/134039/head
git -C '<runtime>' checkout 331fa2b3
git -C '<runtime>' apply '<BlsNet>/jit/carry-rows.patch'
Push-Location '<runtime>'
try { ./build.cmd clr+libs -c Release } finally { Pop-Location }
$env:DOTNET_ROLL_FORWARD = "Major"
$env:DOTNET_ROOT = "<runtime>/artifacts/bin/testhost/net11.0-windows-Release-x64"
& "$env:DOTNET_ROOT/dotnet.exe" '<BlsNet>/benchmarks/Nethermind.Crypto.BlsNet.Benchmarks/bin/Release/net11.0/Nethermind.Crypto.BlsNet.Benchmarks.dll'
```

The historical runtime build path above is named `net11.0-windows-Release-x64`; the supplied patched launcher identifies itself as .NET 12.0.0-dev. Use the actual testhost path produced by the runtime build, then run the same built library and runner assemblies under it. These measurements describe the specified PR revision and patch, not a claim about a current released JIT.

Compact calls also matter for tower arithmetic. In a patched disassembly capture, the original Tier1 Fp6 multiply occupied 8,248 bytes and still contained 56 `setb` instructions. The final Fp6 wrapper occupied 149 bytes, delegating to wide helpers. `Fp.MultiplyComposite` occupied 1,005 bytes with 24 MULX, 24 ADCX, 28 ADOX and one `setb`; `MultiplyWide` occupied 380 bytes with 6 MULX, 6 ADCX, 7 ADOX and no `setb`. These are static counts from that capture, not throughput estimates.

## Experiments that did not help

- BMI2 output-pointer multiplication and UInt128 accumulation did not improve the CIOS kernel across stock and patched runtimes.
- A dedicated Comba Fp square, a one-row multiply loop and a non-inlined public multiply were discarded. There is no runtime-dependent field-kernel selector.
- Fully lazy Fp4/cyclotomic formulas, forced field inlining into Fp2 and alternate tower call boundaries regressed the measured paths.
- Curve variants with alternate inlining, width-3/5 small MSM tables, adjacent Pippenger windows, Jacobian buckets and batch-affine accumulation below the chosen thresholds did not improve their applicable paths.
- A radix-48 Montgomery prototype used eight 48-bit limbs and preserved R = 2^384. Its edge and random products matched the references, but conversion, 128-bit column arithmetic and reduction outweighed simpler carry handling. The prototype was removed after its test-only correctness run passed 598,209 assertions.

The radix-48 experiment recorded these best field times, in ns per operation:

| Operation | Stock .NET 10 | Stock .NET 11 | Patched runtime |
|---|---:|---:|---:|
| Fp multiply, current | 53.25 | 53.04 | 20.45 |
| Fp multiply, radix-48 | 187.52 | 182.83 | 131.76 |
| Fp2 multiply, current | 195.15 | 192.82 | 78.61 |
| Fp2 multiply, radix-48 | 606.31 | 588.47 | 431.37 |

The tested .NET 10 and .NET 11 reference packs exposed neither `Avx512Ifma` nor an Avx10 52-bit multiply equivalent. On the tested Ryzen 9 9950X3D, `Avx512F.IsSupported` was true and `Avx10v1.IsSupported` was false. No SIMD IFMA kernel was added. This records the tested CPU/runtime surface; it is not a general statement about future intrinsics or hardware.

Algorithm and calling-pattern details are in [design](design.md). Stock-JIT improvements and platform validation remain on the [roadmap](roadmap.md).
