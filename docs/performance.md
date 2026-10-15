# Performance

The carry-aware JIT makes every measured EIP-2537 precompile faster than the native baseline in this capture (0.50–0.93x its time). Stock .NET 10 takes about 1.5–3.8x. These are results for the supplied inputs on a Ryzen 9 9950X3D VM running Windows 11, not platform-wide guarantees.

Stock RyuJIT materializes carries through comparisons, SETcc and MOVZX. [dotnet/runtime PR #134039](https://github.com/dotnet/runtime/pull/134039), head `0a303c9` or later, includes the carry-aware lowering: it keeps carries in flags across arithmetic chains. MULX alone does not avoid carry materialization. The retained fast numbers were captured with that lowering before it was incorporated into the PR; reproducing them now requires building the PR, with no separate patch.

## EIP-2537 precompiles

Nethermind's BenchmarkDotNet in-process Short job measures complete precompiles. Native is Nethermind.Crypto.Bls 0.3.17.15; managed uses the same parsing, validation and output encoding over net10.0 sources. Stock is .NET 10.0.12; the carry-aware host identifies itself as .NET 12.0.0-dev.

Values are BenchmarkDotNet means at the supplied precision; ratios can differ from division of rounded cells. The native column is from the stock run. Native timings in the fast run varied from 0.838–1.034x that baseline (mean 0.980x). MSM sizes count point/scalar pairs; pairing sizes count G1/G2 pairs.

| Precompile | Points / pairs | blst | Managed .NET 10 | Ratio | Managed patched JIT | Ratio |
|---|---:|---:|---:|---:|---:|---:|
| G1ADD | 1 | 2.7 µs | 4.9 µs | 1.84x | 2.2 µs | 0.83x |
| G1MSM | 1 | 83.7 µs | 313.6 µs | 3.75x | 65.1 µs | 0.78x |
| G1MSM | 2 | 186.5 µs | 530.4 µs | 2.84x | 142.5 µs | 0.76x |
| G1MSM | 8 | 406.7 µs | 1.48 ms | 3.64x | 322.0 µs | 0.79x |
| G1MSM | 32 | 1.04 ms | 3.41 ms | 3.27x | 841.3 µs | 0.81x |
| G1MSM | 64 | 1.64 ms | 6.13 ms | 3.73x | 1.33 ms | 0.81x |
| G1MSM | 128 | 2.73 ms | 10.06 ms | 3.68x | 2.22 ms | 0.81x |
| G1MSM | 256 | 4.53 ms | 13.03 ms | 2.88x | 3.72 ms | 0.82x |
| G1MSM | 512 | 7.60 ms | 22.33 ms | 2.94x | 6.01 ms | 0.79x |
| G1MSM | 2048 | 23.59 ms | 78.41 ms | 3.32x | 18.28 ms | 0.77x |
| G1MSM | 4096 | 42.80 ms | 141.60 ms | 3.31x | 32.84 ms | 0.77x |
| G1 mult (g1mul) | 1 | 81.9 µs | 307.3 µs | 3.75x | 64.4 µs | 0.79x |
| G2ADD | 1 | 4.2 µs | 7.5 µs | 1.77x | 3.1 µs | 0.73x |
| G2MSM | 1 | 147.4 µs | 346.2 µs | 2.35x | 99.7 µs | 0.68x |
| G2MSM | 2 | 436.5 µs | 665.3 µs | 1.52x | 219.0 µs | 0.50x |
| G2MSM | 8 | 866.0 µs | 1.83 ms | 2.11x | 595.1 µs | 0.69x |
| G2MSM | 32 | 2.85 ms | 4.96 ms | 1.74x | 1.46 ms | 0.51x |
| G2MSM | 64 | 4.14 ms | 8.41 ms | 2.03x | 2.25 ms | 0.54x |
| G2MSM | 128 | 6.45 ms | 14.39 ms | 2.23x | 3.64 ms | 0.56x |
| G2MSM | 256 | 11.07 ms | 27.60 ms | 2.49x | 6.24 ms | 0.56x |
| G2MSM | 512 | 19.33 ms | 44.56 ms | 2.31x | 10.62 ms | 0.55x |
| G2MSM | 2048 | 58.92 ms | 167.37 ms | 2.84x | 34.75 ms | 0.59x |
| G2MSM | 4096 | 104.81 ms | 261.38 ms | 2.49x | 63.37 ms | 0.60x |
| G2 mult (g2mul) | 1 | 155.4 µs | 327.8 µs | 2.11x | 97.5 µs | 0.63x |
| MAP_FP_TO_G1 | 1 | 28.7 µs | 107.2 µs | 3.73x | 26.7 µs | 0.93x |
| MAP_FP2_TO_G2 | 1 | 102.4 µs | 300.8 µs | 2.94x | 87.3 µs | 0.85x |
| PAIRING_CHECK | 1 | 476.6 µs | 1.26 ms | 2.64x | 392.7 µs | 0.82x |
| PAIRING_CHECK | 2 | 689.0 µs | 1.76 ms | 2.55x | 595.8 µs | 0.86x |
| PAIRING_CHECK | 4 | 979.5 µs | 2.46 ms | 2.51x | 824.3 µs | 0.84x |
| PAIRING_CHECK | 8 | 1.45 ms | 3.86 ms | 2.66x | 1.29 ms | 0.89x |

## Key components

These retained component captures (2026-10-13) use blst `f62244ef50ad1a603decdb8f215e982d2a467bb6` with ADX/BMI2, independently of the bundled precompile reference. Native and managed are paired in each process. Field and algorithm captures used adjacent source revisions; the precompile capture combines the final kernel and algorithm changes. Components exclude precompile parsing/conversion costs.

### Field kernels

| Operation (ns) | Managed .NET 10 | blst .NET 10 | Managed .NET 11 | blst .NET 11 | Managed carry-aware | blst carry-aware |
|---|---:|---:|---:|---:|---:|---:|
| Fp multiply | 50.79 | 18.94 | 49.72 | 18.74 | 19.77 | 19.76 |
| Fp square | 50.48 | 18.11 | 43.77 | 18.18 | 19.22 | 18.61 |
| Fp2 multiply | 183.96 | 59.86 | 145.86 | 59.52 | 61.10 | 61.37 |
| Fp2 square | 117.92 | 40.59 | 114.70 | 40.30 | 42.89 | 40.98 |
| Fp inverse | 1841.48 | 1321.54 | 1656.70 | 1331.71 | 1332.87 | 1365.13 |

### Tower, pairing and curves

| Operation (µs) | Managed .NET 10 | blst .NET 10 | Managed .NET 11 | blst .NET 11 | Managed carry-aware | blst carry-aware |
|---|---:|---:|---:|---:|---:|---:|
| Fp12 multiply | 3.386 | 1.133 | 2.956 | 1.177 | 1.101 | 1.150 |
| Fp12 square | 2.496 | 0.815 | 2.139 | 0.848 | 0.802 | 0.816 |
| Cyclotomic square | 1.777 | 0.499 | 1.489 | 0.497 | 0.512 | 0.498 |
| Miller 1 | 472.380 | 147.730 | 398.230 | 146.760 | 149.190 | 146.880 |
| Miller 8 | 2708.710 | 824.580 | 2244.570 | 831.320 | 831.410 | 833.620 |
| Final exponentiation | 593.630 | 203.070 | 507.130 | 208.340 | 184.910 | 204.430 |
| Pairing check 2 | 1385.150 | 449.030 | 1157.450 | 457.340 | 435.650 | 453.860 |
| map G2 | 275.290 | 79.920 | 227.142 | 80.825 | 86.363 | 81.829 |
| G1 MSM 128 | 7227.500 | 1659.600 | 3857.500 | 1648.500 | 1525.400 | 1645.300 |
| G2 MSM 128 | 12502.700 | 4717.500 | 6882.700 | 4719.000 | 2951.200 | 4734.800 |

## Method and caveats

Loops warm for at least 0.5 s and report the best of seven rounds. Fields use 1,000,000 independent operations (30,000 for inversion); the Fr/Fp mul+add loops have a dependent local accumulator. `--quick` divides kernel counts by ten, uses 1,000 tower iterations and ten pairing iterations, and calibrates curve rounds toward 3 ms with best-of-21 sampling. Tower/pairing also use best-of-21 in quick mode. Default curve sweeps cover 1–4,096 points; quick mode selects 1 and 128. `BLST_MSM_SIZES` overrides the sweep. Filters do not change input generation.

Native and managed use the same inputs and loop shapes. Native MSM pointer arrays and scratch are reused outside timing. Small native field imports suppress GC transitions; algorithm imports include ordinary P/Invoke overhead. The CLI prints kernel/tower times in ns, curve times in µs and allocation counts for kernels/tower. Missing native results are NaN with an explicit SKIP message.

For the retained field captures, tiering was disabled and logical processor 11 selected; algorithm captures used processor 3 and short rounds. Field captures used net10.0 on stock .NET 10 and the fast host, net11.0 on stock .NET 11. Algorithm captures used net10.0 on .NET 10 and net11.0 on the other hosts. A net10.0 build adopts the executing runtime's policy: compact on .NET 10, expanded on .NET 11 and later. Both CLI policy overrides are fixed before field initialization. See [design](design.md#runtime-kernel-policy).

Run builds, tests and timings sequentially. Best samples are not means or confidence intervals. Shared VM cache/clock contention makes differences of a few percent weak evidence. Zero warmed allocations do not include initial pool growth or complete precompile output allocation. Exported native calls carry ABI overhead; internal native calls may cost less. Some components, including maps and cyclotomic squaring, can still trail native despite complete precompile wins.

The lead also has an algorithmic part: managed scalar operations are variable-time for public inputs, while blst's single-point multiplications are constant-time. Managed MSM uses GLV/GLS splits and batch-affine buckets that blst's Pippenger does not. Compressed cyclotomic powers and fused map, point and Miller formulas reduce arithmetic work. Precompile comparisons do not isolate a JIT advantage.

## Reproduction

Use the SDK from [global.json](../global.json), then build native blst and both executable targets:

```powershell
./scripts/build-native-blst.ps1
dotnet build Nethermind.Crypto.BlsNet.slnx -c Release
dotnet build Nethermind.Crypto.BlsNet.slnx -c Release -p:BlsNetTestTargetFramework=net10.0
$env:DOTNET_TieredCompilation = '0'
$env:BLST_BENCH_PROCESSOR = '3'
dotnet run --project benchmarks/Nethermind.Crypto.BlsNet.Benchmarks -c Release -- --quick
dotnet run --project benchmarks/Nethermind.Crypto.BlsNet.Benchmarks -c Release -- --filter 'Fp multiply|Fp2 multiply|Fp12 multiply|Pairing check 2|MSM 128|map G2'
```

To reproduce fast numbers, build a separate dotnet/runtime checkout from PR #134039 at head `0a303c9` or later, which includes the carry-aware lowering:

```powershell
git -C '<runtime>' fetch https://github.com/dotnet/runtime pull/134039/head
git -C '<runtime>' checkout FETCH_HEAD
# Verify this revision contains 0a303c9 before building.
Push-Location '<runtime>'
try { ./build.cmd clr+libs -c Release } finally { Pop-Location }
$env:DOTNET_ROLL_FORWARD = 'Major'
$env:DOTNET_ROOT = '<runtime>/artifacts/bin/testhost/net11.0-windows-Release-x64'
& "$env:DOTNET_ROOT/dotnet.exe" '<BlsNet>/benchmarks/Nethermind.Crypto.BlsNet.Benchmarks/bin/Release/net11.0/Nethermind.Crypto.BlsNet.Benchmarks.dll' --quick
```

Use the actual testhost output directory: its net11.0 name may contain a .NET 12-dev host. Record runtime, target, processor and kernel override. Stock .NET 10 requires a net10.0 executable build, rather than just changing the launcher.

The Nethermind integration branch is not published yet. It links library sources through `BlsNetSource`, excluding bin/obj. Its `Bls12381` tests cover precompile integration; component checks use the console runner in this repository. To reproduce precompile means in that integration tree:

```powershell
dotnet run --project src/Nethermind/Nethermind.Benchmark.Runner -c Release -- --filter '*Bls12381*' --job Short --inProcess
```

Run the same built benchmark under the carry-aware testhost, with `DOTNET_ROOT` and roll-forward as above. Include native benchmarks in both runs. The in-process toolchain measures the launcher runtime. Component best samples and BenchmarkDotNet means are separate measurements.

## Experiments retained as constraints

Three-row/fully expanded CIOS, blanket inlining, fully lazy cyclotomic formulas, radix-48 arithmetic and lower batch-affine thresholds did not improve the applicable stock/fast paths. Compact fallbacks and isolated kernels remain deliberate. See [design](design.md) for bounds and algorithms and [roadmap](roadmap.md) for open work.
