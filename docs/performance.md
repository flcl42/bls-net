# Performance

With the carry-aware JIT, every measured EIP-2537 precompile is faster than blst. G1 addition takes 0.83x blst's time, G1 MSM 0.76–0.82x, and G1 multiplication 0.79x. G2 addition takes 0.73x, G2 MSM 0.50–0.69x, and G2 multiplication 0.63x. Maps take 0.85–0.93x and pairing checks 0.82–0.89x. These ranges cover the condensed table below; ratios are managed time divided by blst time, so lower is faster.

Stock .NET 10 takes about 1.5–3.8x blst's time in that table: G1 addition 1.84x, MSM 2.84–3.75x and multiplication 3.75x; G2 addition 1.77x, MSM 1.52–2.84x and multiplication 2.11x; maps 2.94–3.73x; pairing 2.51–2.66x. Stock RyuJIT emits MULX but materializes carries through comparisons, SETcc and MOVZX. One Fp multiply costs about 51–53 ns on stock .NET 10 versus 19–20 ns with the patch. Preserving carries in ADC/ADCX/ADOX chains removes much of that cost.

Part of the patched lead comes from different algorithms and timing contracts. Managed scalar operations are variable-time for public inputs; blst's single-point multiplication is constant-time, including G2 multiplication. Managed MSM splits scalars with GLV/GLS endomorphisms and uses batch-affine buckets at measured thresholds. blst's Pippenger uses neither. Compressed cyclotomic powers and fused map, point and Miller arithmetic also reduce work. The precompile timings therefore measure the complete implementations, rather than isolating a JIT advantage. They establish results for these benchmark inputs, not every possible input or platform.

## EIP-2537 precompiles

Nethermind's BenchmarkDotNet benchmarks use the in-process toolchain and Short job on a Ryzen 9 9950X3D VM running Windows 11. The native reference is Nethermind.Crypto.Bls 0.3.17.15 over blst. Managed uses the same Nethermind precompile code over this library, compiled for net10.0. Stock is .NET 10.0.12. Patched is .NET 12.0.0-dev from dotnet/runtime PR #134039 at `331fa2b3` plus [`jit/carry-rows.patch`](../jit/carry-rows.patch).

Values are BenchmarkDotNet means at the supplied precision. Ratios are reported values and can differ from division of rounded cells. The blst column is from the stock run; blst times in the patched run had a mean ratio of 0.980, with a range of 0.838–1.034, or about -16%/+3%. Each managed ratio uses the blst reference from its own run. MSM sizes count point/scalar pairs; pairing sizes count G1/G2 pairs. Addition size 1 means one addition of two points. The g1mul/g2mul rows use the single-multiplication benchmark inputs.

The previous column compares patched ratios before and after the latest kernel, JIT and algorithm changes. It uses each capture's own native reference; it does not attribute the whole change to one optimization.

| Precompile | Points / pairs | blst | Managed .NET 10 | Ratio | Managed patched JIT | Ratio | Patched ratio, previous → now |
|---|---:|---:|---:|---:|---:|---:|---:|
| G1ADD | 1 | 2.7 µs | 4.9 µs | 1.84x | 2.2 µs | 0.83x | 0.93x → 0.83x |
| G1MSM | 1 | 83.7 µs | 313.6 µs | 3.75x | 65.1 µs | 0.78x | 0.93x → 0.78x |
| G1MSM | 2 | 186.5 µs | 530.4 µs | 2.84x | 142.5 µs | 0.76x | 0.99x → 0.76x |
| G1MSM | 8 | 406.7 µs | 1.48 ms | 3.64x | 322.0 µs | 0.79x | 0.92x → 0.79x |
| G1MSM | 32 | 1.04 ms | 3.41 ms | 3.27x | 841.3 µs | 0.81x | 1.02x → 0.81x |
| G1MSM | 64 | 1.64 ms | 6.13 ms | 3.73x | 1.33 ms | 0.81x | 1.02x → 0.81x |
| G1MSM | 128 | 2.73 ms | 10.06 ms | 3.68x | 2.22 ms | 0.81x | 0.98x → 0.81x |
| G1MSM | 256 | 4.53 ms | 13.03 ms | 2.88x | 3.72 ms | 0.82x | 0.99x → 0.82x |
| G1MSM | 512 | 7.60 ms | 22.33 ms | 2.94x | 6.01 ms | 0.79x | 0.94x → 0.79x |
| G1MSM | 2048 | 23.59 ms | 78.41 ms | 3.32x | 18.28 ms | 0.77x | 0.83x → 0.77x |
| G1MSM | 4096 | 42.80 ms | 141.60 ms | 3.31x | 32.84 ms | 0.77x | 0.86x → 0.77x |
| G1 mult (g1mul) | 1 | 81.9 µs | 307.3 µs | 3.75x | 64.4 µs | 0.79x | 0.94x → 0.79x |
| G2ADD | 1 | 4.2 µs | 7.5 µs | 1.77x | 3.1 µs | 0.73x | 0.81x → 0.73x |
| G2MSM | 1 | 147.4 µs | 346.2 µs | 2.35x | 99.7 µs | 0.68x | 1.06x → 0.68x |
| G2MSM | 2 | 436.5 µs | 665.3 µs | 1.52x | 219.0 µs | 0.50x | 1.06x → 0.50x |
| G2MSM | 8 | 866.0 µs | 1.83 ms | 2.11x | 595.1 µs | 0.69x | 1.00x → 0.69x |
| G2MSM | 32 | 2.85 ms | 4.96 ms | 1.74x | 1.46 ms | 0.51x | 1.12x → 0.51x |
| G2MSM | 64 | 4.14 ms | 8.41 ms | 2.03x | 2.25 ms | 0.54x | 1.12x → 0.54x |
| G2MSM | 128 | 6.45 ms | 14.39 ms | 2.23x | 3.64 ms | 0.56x | 1.10x → 0.56x |
| G2MSM | 256 | 11.07 ms | 27.60 ms | 2.49x | 6.24 ms | 0.56x | 1.11x → 0.56x |
| G2MSM | 512 | 19.33 ms | 44.56 ms | 2.31x | 10.62 ms | 0.55x | 1.10x → 0.55x |
| G2MSM | 2048 | 58.92 ms | 167.37 ms | 2.84x | 34.75 ms | 0.59x | 1.12x → 0.59x |
| G2MSM | 4096 | 104.81 ms | 261.38 ms | 2.49x | 63.37 ms | 0.60x | 1.11x → 0.60x |
| G2 mult (g2mul) | 1 | 155.4 µs | 327.8 µs | 2.11x | 97.5 µs | 0.63x | 1.09x → 0.63x |
| MAP_FP_TO_G1 | 1 | 28.7 µs | 107.2 µs | 3.73x | 26.7 µs | 0.93x | 1.06x → 0.93x |
| MAP_FP2_TO_G2 | 1 | 102.4 µs | 300.8 µs | 2.94x | 87.3 µs | 0.85x | 1.12x → 0.85x |
| PAIRING_CHECK | 1 | 476.6 µs | 1.26 ms | 2.64x | 392.7 µs | 0.82x | 1.17x → 0.82x |
| PAIRING_CHECK | 2 | 689.0 µs | 1.76 ms | 2.55x | 595.8 µs | 0.86x | 1.15x → 0.86x |
| PAIRING_CHECK | 4 | 979.5 µs | 2.46 ms | 2.51x | 824.3 µs | 0.84x | 1.19x → 0.84x |
| PAIRING_CHECK | 8 | 1.45 ms | 3.86 ms | 2.66x | 1.29 ms | 0.89x | 1.18x → 0.89x |

## Component and kernel measurements

These tables retain the final timings from separate field and algorithm captures on 2026-10-13. They use stock .NET 10.0.12, stock .NET 11 RC1 (`11.0.0-rc.1.26425.128`) and the patched .NET 12.0.0-dev host. Native components use blst `f62244ef50ad1a603decdb8f215e982d2a467bb6`, built with ADX/BMI2. This differs from the bundled precompile reference. Each native value is paired with its managed value in the same process.

The kernel capture includes symmetric Fp square and disjoint-output raw products. The algorithm capture includes fused tower/map arithmetic and paired Miller lines, using the final JIT and the preceding Fp/Fp2 source revision. These are separate measurements, not one capture of all final changes together. The precompile table above measures the combined implementation. Component calls have different validation, conversion and harness costs from complete precompiles.

### Field kernels

Times are ns per operation. Native squares call the dedicated `blst_fp_sqr` and `blst_fp2_sqr` exports. Wide helpers have no equivalent exported blst ABI.

| Operation (ns) | Managed .NET 10 | blst .NET 10 | Managed .NET 11 | blst .NET 11 | Managed patched | blst patched |
|---|---:|---:|---:|---:|---:|---:|
| Fp multiply | 50.79 | 18.94 | 49.72 | 18.74 | 19.77 | 19.76 |
| Fp square | 50.48 | 18.11 | 43.77 | 18.18 | 19.22 | 18.61 |
| Fp2 multiply | 183.96 | 59.86 | 145.86 | 59.52 | 61.10 | 61.37 |
| Fp2 square | 117.92 | 40.59 | 114.70 | 40.30 | 42.89 | 40.98 |
| Fp inverse | 1841.48 | 1321.54 | 1656.70 | 1331.71 | 1332.87 | 1365.13 |
| MultiplyWide | 26.67 | — | 20.93 | — | 9.43 | — |
| ReduceWide | 43.06 | — | 40.31 | — | 18.66 | — |
| ReduceWideSmall | 36.59 | — | 33.53 | — | 16.32 | — |
| AddWide | 5.83 | — | 5.81 | — | 4.21 | — |
| SubtractWide | 5.85 | — | 5.84 | — | 1.97 | — |
| AddUnreduced | 2.08 | — | 2.10 | — | 0.89 | — |
| Halve | 3.84 | — | 3.86 | — | 3.13 | — |
| Negate | 3.27 | 3.14 | 3.28 | 3.06 | 1.78 | 3.10 |
| SubtractSumComposite | 14.26 | — | 14.11 | — | 3.19 | — |
| Fp add | 5.93 | 3.03 | 5.93 | 3.02 | 2.08 | 2.87 |
| Fp subtract | 5.96 | 2.90 | 6.04 | 2.90 | 2.08 | 2.88 |
| MultiplyComposite | 53.07 | 18.82 | 52.13 | 19.06 | 19.62 | 18.97 |
| ReduceWidePositive | 36.65 | — | 28.96 | — | 12.37 | — |
| ReduceWideDifference | 39.93 | — | 38.69 | — | 14.37 | — |
| Fp2 add | 13.42 | 5.19 | 13.42 | 5.18 | 3.83 | 5.32 |
| Fp2 subtract | 12.81 | 5.06 | 12.63 | 5.15 | 3.79 | 5.19 |
| Fp2 mul by 3 | 27.67 | 6.38 | 27.66 | 6.47 | 5.91 | 6.57 |
| Fp2 mul by 4 | 28.18 | 6.97 | 28.26 | 7.12 | 5.61 | 7.19 |
| Fp2 mul by 8 | 41.70 | 8.46 | 41.68 | 8.51 | 8.33 | 8.68 |
| MultiplyWideNoAlias | 27.32 | — | 20.58 | — | 9.04 | — |

### Kernel modes and target frameworks

Times are ns per operation in a separate final capture. Overrides compare both formulas on each supported runtime/target combination. A net11.0 assembly cannot run on .NET 10. Fp multiplication retains the same two-row CIOS kernel in both modes.

| Runtime | Target | Mode | Fp multiply | Fp square | Fp2 multiply | Fp2 square | Fp inverse |
|---|---|---|---:|---:|---:|---:|---:|
| stock10 | net10.0 | compact | 50.82 | 50.43 | 184.32 | 118.57 | 1790.03 |
| stock10 | net10.0 | expanded | 50.52 | 46.16 | 256.76 | 181.16 | 1856.23 |
| stock11 | net10.0 | compact | 49.68 | 50.00 | 181.38 | 114.51 | 1653.00 |
| stock11 | net10.0 | expanded | 49.58 | 43.62 | 145.54 | 112.85 | 1645.91 |
| stock11 | net11.0 | compact | 50.53 | 50.92 | 180.75 | 114.72 | 1650.17 |
| stock11 | net11.0 | expanded | 49.40 | 43.60 | 145.32 | 113.19 | 1655.87 |
| patched | net10.0 | compact | 18.93 | 18.68 | 66.91 | 43.38 | 1305.52 |
| patched | net10.0 | expanded | 18.84 | 18.15 | 59.61 | 42.16 | 1320.28 |
| patched | net11.0 | compact | 18.83 | 18.50 | 66.14 | 43.89 | 1313.31 |
| patched | net11.0 | expanded | 18.96 | 18.05 | 59.81 | 41.81 | 1302.65 |

### Extension tower and pairing

Times are µs per operation. General pairing products are nontrivial.

| Operation (µs) | Managed .NET 10 | blst .NET 10 | Managed .NET 11 | blst .NET 11 | Managed patched | blst patched |
|---|---:|---:|---:|---:|---:|---:|
| Fp6 multiply | 1.141 | 0.377 | 0.959 | 0.380 | 0.374 | 0.380 |
| Fp6 square | 1.026 | 0.287 | 0.747 | 0.283 | 0.285 | 0.278 |
| Fp12 multiply | 3.386 | 1.133 | 2.956 | 1.177 | 1.101 | 1.150 |
| Fp12 square | 2.496 | 0.815 | 2.139 | 0.848 | 0.802 | 0.816 |
| Fp12 sparse multiply | 2.330 | 0.761 | 1.966 | 0.759 | 0.764 | 0.757 |
| Fp12 two sparse multiplies | 4.623 | 1.522 | 3.867 | 1.530 | 1.457 | 1.529 |
| Cyclotomic square | 1.777 | 0.499 | 1.489 | 0.497 | 0.512 | 0.498 |
| Miller 1 | 472.380 | 147.730 | 398.230 | 146.760 | 149.190 | 146.880 |
| Miller 8 | 2708.710 | 824.580 | 2244.570 | 831.320 | 831.410 | 833.620 |
| Final exponentiation | 593.630 | 203.070 | 507.130 | 208.340 | 184.910 | 204.430 |
| Pairing check 1 | 1067.600 | 350.180 | 899.490 | 361.730 | 338.460 | 351.010 |
| Pairing check 2 | 1385.150 | 449.030 | 1157.450 | 457.340 | 435.650 | 453.860 |
| Pairing check 8 | 3287.600 | 1032.190 | 2734.880 | 1041.830 | 1033.260 | 1034.350 |

### Curves and maps

Times are µs per operation. Add + affine includes affine conversion; maps and scalar multiplication return projective points.

| Operation (µs) | Managed .NET 10 | blst .NET 10 | Managed .NET 11 | blst .NET 11 | Managed patched | blst patched |
|---|---:|---:|---:|---:|---:|---:|
| G1 add+affine | 3.862 | 1.757 | 2.836 | 1.749 | 1.773 | 1.762 |
| G1 subgroup | 119.152 | 23.932 | 68.912 | 23.879 | 24.394 | 24.187 |
| G1 multiply | 175.662 | 45.294 | 100.121 | 45.669 | 37.992 | 45.815 |
| map G1 | 100.513 | 22.453 | 66.451 | 22.528 | 24.853 | 22.759 |
| G2 add+affine | 6.135 | 2.648 | 4.969 | 2.640 | 2.620 | 2.655 |
| G2 subgroup | 96.194 | 28.519 | 76.959 | 28.914 | 27.942 | 28.832 |
| G2 multiply | 224.775 | 93.592 | 173.181 | 92.750 | 68.247 | 94.170 |
| map G2 | 275.290 | 79.920 | 227.142 | 80.825 | 86.363 | 81.829 |

### G1 MSM

Times are µs per operation, using distinct random subgroup points and full-width scalars. Component MSM excludes the precompile's parsing, validation and output encoding.

| Operation (µs) | Managed .NET 10 | blst .NET 10 | Managed .NET 11 | blst .NET 11 | Managed patched | blst patched |
|---|---:|---:|---:|---:|---:|---:|
| G1 MSM 1 | 173.065 | 66.188 | 99.714 | 66.379 | 37.451 | 66.657 |
| G1 MSM 2 | 282.670 | 83.660 | 160.583 | 85.433 | 60.768 | 86.240 |
| G1 MSM 4 | 451.517 | 124.967 | 247.000 | 126.367 | 94.723 | 126.810 |
| G1 MSM 8 | 767.900 | 207.267 | 414.143 | 207.400 | 160.111 | 210.267 |
| G1 MSM 16 | 1466.550 | 385.550 | 790.700 | 386.067 | 305.617 | 388.650 |
| G1 MSM 32 | 2449.400 | 606.400 | 1369.200 | 617.000 | 535.100 | 615.460 |
| G1 MSM 64 | 4186.300 | 981.700 | 2277.500 | 989.500 | 906.300 | 999.633 |
| G1 MSM 128 | 7227.500 | 1659.600 | 3857.500 | 1648.500 | 1525.400 | 1645.300 |
| G1 MSM 256 | 9405.100 | 2872.200 | 5631.200 | 2839.500 | 2605.800 | 2812.200 |
| G1 MSM 512 | 16021.400 | 4815.200 | 9329.100 | 4799.800 | 4276.100 | 4794.900 |
| G1 MSM 1024 | 30925.000 | 8541.200 | 17934.500 | 8494.700 | 7867.900 | 8471.300 |
| G1 MSM 2048 | 53270.800 | 14997.800 | 30849.700 | 15071.000 | 13407.800 | 15088.900 |
| G1 MSM 4096 | 94279.500 | 26949.000 | 54152.200 | 27005.600 | 23650.500 | 27387.300 |

### G2 MSM

Times are µs per operation under the same conditions.

| Operation (µs) | Managed .NET 10 | blst .NET 10 | Managed .NET 11 | blst .NET 11 | Managed patched | blst patched |
|---|---:|---:|---:|---:|---:|---:|
| G2 MSM 1 | 219.954 | 159.869 | 171.524 | 158.600 | 67.175 | 160.173 |
| G2 MSM 2 | 422.517 | 204.533 | 325.288 | 207.000 | 129.859 | 208.405 |
| G2 MSM 4 | 760.400 | 312.867 | 578.600 | 315.740 | 230.775 | 320.692 |
| G2 MSM 8 | 1412.950 | 538.450 | 1068.600 | 534.150 | 429.233 | 541.483 |
| G2 MSM 16 | 2431.300 | 859.100 | 1792.900 | 864.100 | 738.400 | 873.333 |
| G2 MSM 32 | 4170.500 | 1738.800 | 2505.500 | 1741.000 | 1091.300 | 1751.600 |
| G2 MSM 64 | 7298.500 | 2815.800 | 4095.500 | 2867.900 | 1716.900 | 2836.800 |
| G2 MSM 128 | 12502.700 | 4717.500 | 6882.700 | 4719.000 | 2951.200 | 4734.800 |
| G2 MSM 256 | 21965.400 | 8126.100 | 12098.000 | 8116.700 | 5153.300 | 8199.600 |
| G2 MSM 512 | 40450.700 | 13891.200 | 21007.000 | 13691.500 | 8834.700 | 13889.800 |
| G2 MSM 1024 | 71599.100 | 24347.200 | 38191.100 | 24153.700 | 15940.000 | 24209.800 |
| G2 MSM 2048 | 132420.700 | 43817.700 | 70756.300 | 43312.000 | 29340.600 | 43025.700 |
| G2 MSM 4096 | 228071.900 | 77861.600 | 128148.900 | 78293.800 | 53344.300 | 77442.300 |

Patched component results still have gaps: maps, cyclotomic square and the single-pair Miller loop can trail their native reference. Exported native calls include ABI overhead; native internal calls can have lower costs. Scratch traffic, call boundaries and register allocation remain plausible causes after matching or reducing the source product counts. This attribution is an inference from disassembly and measured variants, not a hardware-counter profile. Precompile wins do not imply universal kernel parity.

## Kernel policy

`Fp.UseExpandedKernels` follows the executing runtime: compact on .NET 10, expanded on .NET 11 and later, including a net10.0 assembly on the patched host. Expanded mode selects symmetric square, expanded raw products/reducers and the three-product/two-reduction Fp2 multiplier. Compact Fp2 multiplication uses three CIOS calls. Small inlineable dispatchers select isolated implementations; optimized callers fold the initialized readonly policy and remove the unused branch.

Set the AppContext switch `Nethermind.Crypto.BlsNet.UseExpandedKernels` before first field use, or set `BLSNET_EXPANDED_KERNELS` before process startup (`0`/`1`, or case-insensitive `false`/`true`). AppContext takes precedence; invalid environment values use the runtime default. Selection is fixed after initialization. Expanded Fp2 is slower on stock .NET 10, so forcing it is a test option. G2 batch-affine MSM also follows this policy. Thresholds count effective points after splitting: G1 starts at 512 in either mode; G2 starts at 128 in expanded mode and keeps XYZZ in compact mode. See [design](design.md#multi-scalar-multiplication).

## Measurement method and caveats

Field loops warm for at least 0.5 s, then take the best of seven rounds of 1,000,000 independent operations (30,000 for inversion). Tiering is disabled and logical processor 11 is selected. Native field imports use `SuppressGCTransition`. Large inline kernels have separate benchmark methods so unrelated cases do not consume their JIT analysis budgets.

Algorithm loops warm for at least 0.5 s and use best-of-21 samples on logical processor 3, with tiering disabled. Tower rounds use 1,000 iterations and pairing rounds use 10. Curve rounds calibrate toward 3 ms, with at least one iteration. Native and managed share inputs and loop shapes; native MSM scratch and pointer arrays are reused outside timing. Algorithm-native imports include their P/Invoke overhead. Stock .NET 10 uses net10.0 runners; stock .NET 11 and patched algorithm captures use net11.0 runners. The field capture uses net10.0 on stock .NET 10/patched and net11.0 on stock .NET 11.

Builds, tests and measurement processes run sequentially. These component values are best samples, not statistical means or confidence intervals. Shared VM clock/cache contention makes differences of a few percent weak evidence. Interleaved kernel repeats put Fp square at about native parity and Fp2 square about 5.6% slower; a single noisy capture is insufficient to claim a small improvement. Managed kernel/tower loops report zero allocations, and warmed curve/MSM allocation checks pass; initial pool use and complete precompile output allocations are separate costs.

## Reproduction

Use the .NET 11 SDK selected by `global.json`. Build the optional native reference with test-only Fp6 exports when reproducing tower rows. [`scripts/blst-tower.c`](../scripts/blst-tower.c) exposes those formulas.

```powershell
./scripts/build-native-blst.ps1 -TowerBenchmarks
dotnet build Nethermind.Crypto.BlsNet.slnx -c Release
dotnet build Nethermind.Crypto.BlsNet.slnx -c Release -p:BlsNetTestTargetFramework=net10.0

dotnet run --project benchmarks/Nethermind.Crypto.BlsNet.Benchmarks -c Release
dotnet run --project tests/Nethermind.Crypto.BlsNet.CurveTests -c Release -f net11.0 -- --bench
dotnet run --project tests/Nethermind.Crypto.BlsNet.PairingTests -c Release -f net11.0 -- --perf
dotnet run --project tests/Nethermind.Crypto.BlsNet.PairingTests -c Release -f net11.0 -- --kernel-timing
```

These are console runners; use `dotnet run`, not `dotnet test`. The test projects default to net11.0; `-p:BlsNetTestTargetFramework=net10.0` builds the .NET 10 runners. Changing only the launcher does not retarget an assembly. Without native blst, managed checks and timings still run and native comparisons print `SKIP`.

[`scripts/test-field-runtime-matrix.ps1`](../scripts/test-field-runtime-matrix.ps1) builds both targets and runs all three suites, kernel tests and cold Fp2/square checks sequentially in both modes on every compatible runtime. `-Timings` adds kernel timing sweeps; `-Cpu` sets kernel affinity through `KERNEL_CPU`.

```powershell
./scripts/test-field-runtime-matrix.ps1 `
  -SdkDotnet '<dotnet11>/dotnet.exe' `
  -Stock10Dotnet 'C:/Program Files/dotnet/dotnet.exe' `
  -Stock11Dotnet '<dotnet11>/dotnet.exe' `
  -PatchedDotnet '<testhost>/dotnet.exe' -Cpu 11 -Timings

$env:DOTNET_ROLL_FORWARD = 'Major'
$env:DOTNET_TieredCompilation = '0'
$env:BLST_BENCH_PROCESSOR = '3'
$env:BLST_BENCH_ROUND_MS = '3'
$env:BLST_BENCH_FILTER = 'Miller|Pairing|Fp12'
& '<testhost>/dotnet.exe' tests/Nethermind.Crypto.BlsNet.PairingTests/bin/Release/net10.0/Nethermind.Crypto.BlsNet.PairingTests.dll --perf
```

`BLST_BENCH_PROCESSOR` optionally pins the curve and tower/pairing runners on Windows. `BLST_BENCH_ROUND_MS` selects shorter rounds and best-of-21 sampling; curves calibrate to that many milliseconds. Pairing short rounds use 1,000 tower iterations and 10 pairing iterations. Warmup remains at least 0.5 s. `BLST_BENCH_FILTER` selects names containing any `|`-separated string, case-insensitively, without changing generated inputs. Unset it for complete tables. `BLST_MSM_SIZES` accepts comma-separated sizes for curve sweeps. Kernel timings use `KERNEL_CPU` and the optional substring `KERNEL_FILTER` instead. Clear overrides before correctness suites.

### Integration benchmarks

The Nethermind integration branch is not published yet. Reproducing precompile means requires that integration and its benchmark inputs. It links `src/Nethermind.Crypto.BlsNet/**/*.cs`, excluding `bin/` and `obj/`, into a net10.0 library project. Managed precompiles keep Nethermind's parsing, validation and output encoding; scalar byte order and padded coordinates are converted in that layer. Register `ManagedBls12381*Benchmark` beside `Bls12381*Benchmark`.

From the integrated Nethermind source tree:

```powershell
dotnet run --project src/Nethermind/Nethermind.Benchmark.Runner -c Release -- --filter '*Bls12381*' --job Short --inProcess
```

Set `BlsNetSource` to the library source directory if needed. Run the same built runner under the patched host with `DOTNET_ROOT` pointing to its testhost and `DOTNET_ROLL_FORWARD=Major`. Include native benchmarks in both runs. The in-process toolchain measures the launcher runtime. Record the executing runtime, target and kernel override. Component best-round values and BenchmarkDotNet means are separate measurements.

### Building the experimental runtime

In a separate dotnet/runtime source tree, fetch PR #134039, check out `331fa2b3`, apply [`carry-rows.patch`](../jit/carry-rows.patch), then build and run the existing benchmark assembly:

```powershell
git -C '<runtime>' fetch https://github.com/dotnet/runtime pull/134039/head
git -C '<runtime>' checkout 331fa2b3
git -C '<runtime>' apply '<BlsNet>/jit/carry-rows.patch'
Push-Location '<runtime>'
try { ./build.cmd clr+libs -c Release } finally { Pop-Location }
$env:DOTNET_ROLL_FORWARD = 'Major'
$env:DOTNET_ROOT = '<runtime>/artifacts/bin/testhost/net11.0-windows-Release-x64'
& "$env:DOTNET_ROOT/dotnet.exe" '<BlsNet>/benchmarks/Nethermind.Crypto.BlsNet.Benchmarks/bin/Release/net11.0/Nethermind.Crypto.BlsNet.Benchmarks.dll'
```

Use the actual testhost directory produced by the runtime build. Its directory can be named net11.0 while the launcher identifies itself as .NET 12.0.0-dev. These measurements describe the specified revision and patch. Patch behavior and regression coverage are in [`jit/README.md`](../jit/README.md).

## Kernel and JIT details

The original dependent `mul+add` loop uses `acc = acc * a + b` for 1,000,000 iterations, best of seven rounds. The table takes the best of five interleaved runs on the Ryzen 9 9950X3D; runs vary by about 5%. Times are ns per iteration. These isolated-loop measurements precede the independent-operation kernel captures above.

| Kernel | .NET 11 RC1 | #134039 | #134039 + patch | blst ADX, P/Invoke | blst ADX, C loop |
|---|---:|---:|---:|---:|---:|
| Fr mul+add | 26.4 | 17.7 | 10.9 | 12.1 | 11.9 |
| Fp mul+add | 56.7 | 33.8 | 19.8 | 20.4 | 20.4 |

Native imports suppress the GC transition; the C loop calls `libblst.a`. Without ADX, the native C loop takes 13.2 ns for Fr and 24.8 ns for Fp. In these runs patched managed mul+add is 8–10% faster for Fr and 2–4% for Fp. Multiplication alone is about 18.7 ns versus 18.6 ns for native Fp, and 9.8 ns versus 10.6 ns for Fr. Inlined addition with flag-based selection accounts for part of the loop's lead over native function calls.

The kernels expose multiply-accumulate, add-carry and subtract-borrow idioms. For example, `hi = Math.BigMul(m, x, out lo); lo += t; hi += lo < t ? 1 : 0;` exposes the first carry. Final conditional subtraction adds the radix minus the modulus and selects by carry. The patched JIT retains the carry in flags and folds mask selects to CMOV. MULX alone does not remove stock carry materialization.

Fp multiplication processes two Montgomery rows per iteration: 24 static widening products execute three times, or 72 per call. Expanded symmetric square uses 57 widening products: 15 cross products, six diagonal products and 36 reduction products. Expanded Fp2 multiplication uses 180 widening products, versus 216 for three CIOS calls. Fp2 square retains two CIOS products, or 144 widening products. Logical field-product counts and static instruction counts are different measures.

## Rejected experiments

- Three-row or fully expanded CIOS, one-row multiplication, BMI2 output-pointer and UInt128 accumulation, and forced non-inlined public multiplication did not improve the applicable stock/patched paths.
- Earlier Comba/cross-product squares, separate diagonal buffers and all-word shift doubling lost to the retained symmetric bit-carry square. Unconditional symmetric square on stock .NET 10 did not establish a curve gain.
- Fully inlined raw Fp2 products, paired reducers, extra square subtraction inlining, reversed square product order and alternate operand copies increased spills or gave no reliable gain. Public Fp2 subtraction inlining is retained.
- Fully lazy Fp4/cyclotomic square, alternate tower boundaries and blanket curve-wrapper inlining did not repay their scratch/call costs. Delayed Fp6 square reductions are retained.
- Width-3/5 small MSM tables, adjacent Pippenger windows, Jacobian buckets and lower batch-affine thresholds did not improve the applicable paths. Small split MSMs require endomorphism-derived tables to avoid rebuilding each image.
- Radix-48 Montgomery arithmetic passed reference checks but conversion and wide-column costs dominated. No SIMD IFMA kernel was added. Blanket expanded kernels and changed root calls on stock .NET 10 regressed composite paths; compact fallbacks remain.

See [design](design.md) for bounds and algorithms, and [roadmap](roadmap.md) for open work.
