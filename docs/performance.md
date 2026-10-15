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

## Fused maps on the carry-aware runtime

Capture: 2026-10-15, Ryzen 9 9950X3D, Windows x64, logical processor 3, net11.0 executables, expanded policy, tiering disabled. Before is library `8dd3cc4` with the exact merged `0a303c9` JIT sources; after is library `23aae81` (including `5842f3f`) with the [JIT follow-up](../jit/README.md). Both use the same .NET 12-dev testhost contents and ADX/BMI2 blst `f62244ef`. Builds and tests ran separately from timings. Values below are **medians of three process-level best samples**, from three alternating before/after pairs using the existing `--quick` harness. Each native column comes from those same processes.

| Operation | Before | After | blst before | blst after |
|---|---:|---:|---:|---:|
| Map G1, constant-time | 25.333 µs | 22.810 µs | 22.882 µs | 23.177 µs |
| Map G1, variable-time | 24.970 µs | 22.545 µs | 22.882 µs | 23.177 µs |
| Map G2, constant-time | 87.659 µs | 82.223 µs | 81.395 µs | 82.655 µs |
| Map G2, variable-time | 85.081 µs | 81.266 µs | 81.395 µs | 82.655 µs |
| Fp inverse, constant-time | 9.384 µs | 7.734 µs | 1.324 µs | 1.327 µs |
| Fp inverse, variable-time | 1.382 µs | 1.379 µs | 1.324 µs | 1.327 µs |
| G1 multiply, constant-time | 53.616 µs | 53.082 µs | 46.005 µs | 46.561 µs |
| G1 multiply, variable-time | 37.528 µs | 37.109 µs | 46.005 µs | 46.561 µs |
| G2 multiply, constant-time | 116.555 µs | 116.229 µs | 94.585 µs | 95.399 µs |
| G2 multiply, variable-time | 67.471 µs | 66.721 µs | 94.585 µs | 95.399 µs |
| Pairing check 2 | 431.620 µs | 429.700 µs | 447.350 µs | 448.730 µs |
| G1 MSM 128 | 1534.400 µs | 1527.400 µs | 1656.400 µs | 1662.400 µs |
| G2 MSM 128 | 2885.000 µs | 2887.500 µs | 4734.900 µs | 4754.800 µs |
| Fp multiply | 18.800 ns | 18.860 ns | 18.730 ns | 18.730 ns |
| Fp2 multiply | 59.620 ns | 59.700 ns | 60.470 ns | 59.840 ns |
| G1 multiply, original curve inputs | 37.400 µs | 37.200 µs | 45.000 µs | 45.700 µs |
| G2 multiply, original curve inputs | 66.700 µs | 65.800 µs | 92.700 µs | 92.500 µs |

Maps improve by 4.5–10.0% against their own baseline; fixed-chain Fp inversion improves 17.6%. All four map medians edge native: G1 constant/variable are 0.984/0.973x blst, G2 are 0.995/0.983x. Median paired ratios are 0.983/0.981x for G1 and 0.995/0.987x for G2. G2's constant-time native margin is only 0.5% and is weak evidence: one sample was 0.9% slower than its paired native sample. The three after captures (constant / variable / native, µs) were:

| Map | Run 1 | Run 2 | Run 3 |
|---|---:|---:|---:|
| G1 | 22.810 / 22.545 / 22.991 | 22.778 / 22.730 / 23.177 | 22.890 / 22.536 / 23.398 |
| G2 | 82.350 / 81.266 / 81.578 | 81.955 / 81.091 / 82.655 | 82.223 / 81.615 / 82.657 |

Other requested medians change by -1.4% to +0.3%; the small field slowdowns normalize to +0.3% (Fp) and +1.2% (Fp2) against their paired native columns. These captures do not establish a regression. Native-normalized results and repeated process samples matter more than sub-percent differences.

Isolated profiling with the same rescaled generator points, processor and 21-round minimum method gives reciprocal sqrt 9.300 → 7.668 µs; G1 times -z 12.399 → 11.329 (constant-time 12.612 → 11.590); G2 times -z 29.176 → 28.273 (constant-time 29.646 → 28.392). G2 clearing uses two such multiplications. Disassembly shows no calls inside `SquareNThenMultiply`: 373 sqrt-chain squarings execute in 66 fused runs, and 376 reciprocal-chain squarings in 67 runs. The six-word loop state stays in registers; temporary raw-product words can spill. Final reduction occurs only after the final multiply. The JIT follow-up keeps independent cross-product rows in ADCX/ADOX even beside unmatched diagonals and removes repeated RDX reloads when their multiplier remains live.

The retained formulas use lazy root powers, bounded doubling kernels, a shift-based multiply by eight, a biased Fp2 square difference and direct psi². Complete secret additions remain where a doubling is possible; [cofactor-schedule.md](cofactor-schedule.md) proves the specialized sites and tests actual 3-, 11- and 13-torsion. Blanket point inlining, larger full-adder/liveness budgets and fully unrolled repeated-square reducers were slower and were discarded.

Reproduce the comparison with the existing filter:

```powershell
$env:DOTNET_ROOT = '<patched testhost>'
$env:DOTNET_ROLL_FORWARD = 'Major'
$env:DOTNET_TieredCompilation = '0'
$env:BLST_BENCH_PROCESSOR = '3'
& "$env:DOTNET_ROOT/dotnet.exe" 'benchmarks/Nethermind.Crypto.BlsNet.Benchmarks/bin/Release/net11.0/Nethermind.Crypto.BlsNet.Benchmarks.dll' --quick --filter 'CT Fp inverse|CT G1 multiply|CT G2 multiply|CT map|Fp multiply|Fp2 multiply|G1 multiply|G2 multiply|Pairing check 2|MSM 128|map G'
```

For a fresh baseline use a separate `8dd3cc4` library build and the `0a303c9` JIT. Rebuild after restoring experimental source files: preserved file timestamps can otherwise leave an incremental build stale. Raw experimental captures are ignored local artifacts; the tables above retain the final repeated samples.

## Fixed binary-GCD inverses

Capture: 2026-10-15, Ryzen 9 9950X3D, Windows x64, logical processor 3, expanded kernels, tiering disabled, net11.0 executables on the supplied .NET 12-dev carry-aware host. Before is `3c27b68`; after is `467434d`. Both use the unchanged [JIT follow-up](../jit/README.md) and ADX/BMI2 blst `f62244ef`. Values are **medians of three process-level best samples** from `--quick`, in µs; native columns come from the same processes. Builds, tests and timings ran sequentially.

| Operation | Before | After | blst before | blst after |
|---|---:|---:|---:|---:|
| Fp inverse, constant-time | 7.750 | 2.755 | 1.332 | 1.344 |
| Fp2 inverse, constant-time | 7.797 | 2.870 | 1.412 | 1.407 |
| Fr inverse, constant-time | 3.790 | 1.482 | 0.774 | 0.773 |
| G1 to-affine, constant-time | 7.825 | 2.837 | 1.418 | 1.406 |
| G2 to-affine, constant-time | 8.124 | 3.084 | 1.640 | 1.641 |
| G1 multiply, constant-time | 53.247 | 53.181 | 46.717 | 46.413 |
| G2 multiply, constant-time | 116.965 | 116.272 | 95.750 | 94.834 |
| map G1, constant-time | 22.725 | 22.615 | 23.070 | 23.008 |
| map G2, constant-time | 81.753 | 81.744 | 81.621 | 82.155 |
| Fp inverse, variable-time | 1.374 | 1.378 | 1.332 | 1.344 |
| G1 multiply, variable-time | 36.993 | 37.005 | 46.717 | 46.413 |
| G2 multiply, variable-time | 67.253 | 66.531 | 95.750 | 94.834 |
| map G1, variable-time | 22.480 | 22.392 | 23.070 | 23.008 |
| map G2, variable-time | 80.933 | 81.101 | 81.621 | 82.155 |
| Pairing check 2 | 430.250 | 431.860 | 454.670 | 447.730 |

Fp/Fp2/Fr constant-time inverses improve by 64.5%/63.2%/60.9%; G1/G2 constant-time affine conversion improves by 63.7%/62.0%. Final inverse times are about 2.05x/2.04x/1.92x their paired native medians. The 31-bit C# port uses fixed signed wide cofactors and blst's correction factor; native ADX can use 62-bit batches and a tighter assembly schedule. No new JIT changes were made. Signed product pairs use MUL/ADC where sign repair prevents a complete multiply-accumulate row; correction and Montgomery rows use the existing MULX/ADX lowering.

Unchanged-operation medians move by -1.1% to +0.4%. The largest paired-native normalized slowdown is about 1.9% for pairing; this capture does not establish a regression at that scale on a shared VM. Maps retain their square-root chains. The measured affine-base multipliers build projective tables without normalization, so the new inverse has little effect on those rows. Existing variable-time inverses, maps, scalar multiplication and pairing implementations are unchanged. All three new inverses allocate zero bytes after warm-up.

The [design and bound](design.md#fixed-binary-gcd-inversion) and [functional/generated-code/timing evidence](constant-time-validation.md) accompany these numbers. Regenerate with processor 3, `DOTNET_TieredCompilation=0`, patched `DOTNET_ROOT`, `DOTNET_ROLL_FORWARD=Major`, and this filter, repeated in three fresh processes for each revision:

```powershell
& "$env:DOTNET_ROOT/dotnet.exe" '<benchmark DLL>' --quick --filter 'CT |Fp inverse|G1 multiply|G2 multiply|map G|Pairing check 2'
```

## Constant-time components

Capture: 2026-10-15, Ryzen 9 9950X3D, Windows x64, logical processor 3, tiering disabled. Stock hosts are .NET 10.0.12 (net10.0, compact) and .NET 11.0.0-rc.1.26425.128 (net11.0, expanded). The supplied patched net11.0 testhost identifies itself as .NET 12.0.0-dev (expanded), runtime checkout `331fa2b32a9c1cbfe1c6b377c5d436b3e04ceb44`. Native uses the supplied ADX/BMI2 blst reference. Rows use the same inputs for all three implementations within a process. Each cell is **constant-time / variable-time / blst**, in µs.

| Operation | Stock .NET 10 | Stock .NET 11 | Patched runtime |
|---|---:|---:|---:|
| Fp inverse | 43.025 / 1.981 / 1.351 | 23.269 / 1.771 / 1.358 | 9.917 / 1.436 / 1.406 |
| Fp2 inverse | 43.340 / 2.195 / 1.423 | 23.133 / 1.994 / 1.432 | 10.092 / 1.478 / 1.486 |
| Fr inverse | 15.799 / — / 0.794 | 7.907 / — / 0.777 | 3.947 / — / 0.802 |
| G1 multiply | 250.363 / 182.606 / 47.970 | 130.670 / 96.409 / 47.310 | 56.449 / 39.530 / 49.149 |
| G2 multiply | 364.162 / 243.895 / 99.499 | 276.906 / 176.733 / 97.781 | 119.563 / 68.515 / 96.226 |
| G1 to-affine | 44.055 / 2.385 / 1.490 | 23.362 / 1.938 / 1.432 | 9.892 / 1.463 / 1.465 |
| G2 to-affine | 44.919 / 3.048 / 1.684 | 24.028 / 2.557 / 1.682 | 9.758 / 1.670 / 1.654 |
| Map G1 | 107.859 / 104.302 / 23.894 | 66.214 / 64.882 / 23.584 | 25.619 / 25.163 / 23.796 |
| Map G2 | 299.506 / 299.683 / 85.158 | 238.597 / 237.385 / 85.352 | 89.451 / 85.959 / 82.573 |

Fr has no original variable-time inverse API. `SecretBenchmarks` warms each comparison for 0.5 seconds and takes the best of 21 rounds calibrated toward 10 ms each. Inverse/map inputs are fixed nontrivial field values; multiplication uses mapped points and a deterministic scalar reduced modulo r before timing. The managed constant-time multiply still performs its own fixed reduction inside timing. Canonical nonzero scalars keep the pinned native `blst_p1_mult`/`blst_p2_mult` on their GLV/GLS paths: those exports branch to a fallback for noncanonical/zero scalars. Edge-input differential tests also exercise those fallbacks. Native inverses use blst's safegcd path; native to-affine uses its reciprocal path. This historical capture used the Fermat reciprocal baseline. The current fixed binary-GCD replacement is measured [above](#fixed-binary-gcd-inverses).

In the earlier constant-time API capture below, the original public-input bodies were preserved. It uses their original benchmark inputs and quick harness; values are µs except the first two rows, which are ns. At that revision, existing production files only gained `partial` declarations and documentation. The subsequent fused-map changes are measured separately above.

| Original operation | .NET 10 before → after | .NET 11 before → after | Patched before → after |
|---|---:|---:|---:|
| Fp multiply (ns) | 55.40 → 52.49 | 53.61 → 50.16 | 19.63 → 19.34 |
| Fp2 multiply (ns) | 211.11 → 188.91 | 155.20 → 152.90 | 62.42 → 63.44 |
| G1 multiply | 179.3 → 181.9 | 98.4 → 100.4 | 38.7 → 38.3 |
| G2 multiply | 227.9 → 232.5 | 174.8 → 172.0 | 67.9 → 68.1 |
| G1 MSM 128 | 7433.0 → 7065.1 | 4032.7 → 4026.3 | 1580.7 → 1579.0 |
| G2 MSM 128 | 13543.6 → 12483.3 | 7173.5 → 7239.4 | 2999.9 → 2984.3 |
| Pairing check 2 | 1518.03 → 1390.22 | 1193.96 → 1199.00 | 447.56 → 450.07 |
| Map G1 | 105.5 → 102.9 | 64.2 → 65.3 | 25.2 → 25.2 |
| Map G2 | 285.0 → 279.9 | 224.5 → 222.4 | 84.9 → 85.0 |

There is no consistent regression; the largest retained slowdown is about 2%. These best samples cannot establish improvements of a few percent. A first .NET 11 after-capture gave 67.87 ns Fp multiply, 230.9 µs G2 multiply and 300.8 µs G2 map. Native G2 timings rose in that interval too; repeating these three rows produced the values above. Both captures are retained. See [constant-time validation](constant-time-validation.md) for functional, generated-code and timing-leak evidence.

Reproduce with the existing runtime launch commands, selecting `--quick --filter 'CT '` for the new comparison. `--timing-leak --timing-samples 20000 --no-native` selects the separate statistical experiment; it is not part of the ordinary benchmark or test run.

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
