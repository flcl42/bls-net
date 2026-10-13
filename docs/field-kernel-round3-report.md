# Field kernels and JIT: round 3

Baseline: merged library `75a7ccc` and round 2 JIT `11E3B0E0395C4625721E074B7E9615DE43F0EAC98171F30938EA5B3ABCA6819F`. Measured on 2026-10-13 in the supplied Windows VM, Ryzen 9 9950X3D with twelve exposed logical processors. No repository was pushed.

This round retains symmetric Fp square for expanded mode, direct disjoint-output raw multiplication inside Fp2 multiply, inlined Fp2 subtraction, larger bounded carry-analysis allowances, and flags-preserving unsigned scalar products between carry limbs. Stock .NET 10 retains its compact default and original CIOS square. Universal native parity is not achieved; the tables show the remaining gaps.

The three interleaved patched repeats put Fp square near blst parity and show a 2.2% improvement in its managed/native ratio. Fp2 square changes little and remains about 5.6% slower. G2 subgroup checking improves 8.5% to near parity, G2 multiplication improves 6.0%, map-to-G2 6.3%, and the sampled G2 MSM ratios improve 3.3–3.9%. Stock .NET 11 also benefits from symmetric square and direct products. Stock .NET 10 keeps its existing default square/multiply algorithms.

## Measurement method

All builds and test/benchmark processes ran sequentially. Benchmarks use processor 11, tiering disabled, a minimum 0.5-second warm-up, best-of-seven kernel rounds, and best-of-21 tower/curve rounds. Kernels use one million iterations (inversion: 30,000). Tower rounds use 1,000 iterations and pairing rounds ten; curves calibrate toward 3 ms. Native blst is pinned at `f62244ef` and rebuilt with ADX/BMI2 and the test-only Fp6 exports. Native and managed inputs/loop shapes match within each process. Kernel/tower loops reported zero managed allocations; curve-suite warmed allocation assertions pass. This is not a whole-process allocation claim.

Patched and stock .NET 10 rows execute net10.0 binaries; stock .NET 11 rows execute net11.0 binaries. The mode matrix also verifies net10.0 on stock .NET 11 and both targets on the patched host. Baseline binaries were saved before edits. The private patched host selects the saved baseline JIT using `DOTNET_JitName=clrjit-r3-before.dll`; final captures use its new primary JIT. Native values are retained separately in both captures. Shared VM clock/cache contention remains visible; changes of a few percent, especially untouched controls, are weak evidence.

## Kernels: default mode, nanoseconds

### Patched .NET 12-dev

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| Fp multiply | 18.81 | 18.79 | 19.77 | 19.76 |
| Fp square | 18.53 | 18.10 | 19.22 | 18.61 |
| Fp2 multiply | 61.94 | 59.66 | 61.10 | 61.37 |
| Fp2 square | 42.15 | 39.86 | 42.89 | 40.98 |
| Fp inverse | 1315.41 | 1340.16 | 1332.87 | 1365.13 |
| MultiplyWide | 9.09 | — | 9.43 | — |
| ReduceWide | 14.38 | — | 18.66 | — |
| ReduceWideSmall | 13.25 | — | 16.32 | — |
| AddWide | 1.89 | — | 4.21 | — |
| SubtractWide | 1.87 | — | 1.97 | — |
| AddUnreduced | 0.88 | — | 0.89 | — |
| Halve | 3.09 | — | 3.13 | — |
| Negate | 1.74 | 3.07 | 1.78 | 3.10 |
| SubtractSumComposite | 3.17 | — | 3.19 | — |
| Fp add | 2.05 | 2.91 | 2.08 | 2.87 |
| Fp subtract | 2.10 | 2.92 | 2.08 | 2.88 |
| MultiplyComposite | 19.46 | 18.79 | 19.62 | 18.97 |
| ReduceWidePositive | 12.21 | — | 12.37 | — |
| ReduceWideDifference | 14.13 | — | 14.37 | — |
| Fp2 add | 3.76 | 5.20 | 3.83 | 5.32 |
| Fp2 subtract | 5.25 | 5.20 | 3.79 | 5.19 |
| Fp2 mul by 3 | 17.62 | 6.48 | 5.91 | 6.57 |
| Fp2 mul by 4 | 17.16 | 7.12 | 5.61 | 7.19 |
| Fp2 mul by 8 | 26.88 | 8.55 | 8.33 | 8.68 |
| MultiplyWideNoAlias | — | — | 9.04 | — |

### .NET 11 RC1

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| Fp multiply | 51.13 | 19.30 | 49.72 | 18.74 |
| Fp square | 50.67 | 18.27 | 43.77 | 18.18 |
| Fp2 multiply | 153.20 | 59.73 | 145.86 | 59.52 |
| Fp2 square | 114.11 | 40.65 | 114.70 | 40.30 |
| Fp inverse | 1688.75 | 1328.69 | 1656.70 | 1331.71 |
| MultiplyWide | 21.28 | — | 20.93 | — |
| ReduceWide | 41.63 | — | 40.31 | — |
| ReduceWideSmall | 34.15 | — | 33.53 | — |
| AddWide | 5.92 | — | 5.81 | — |
| SubtractWide | 5.92 | — | 5.84 | — |
| AddUnreduced | 2.16 | — | 2.10 | — |
| Halve | 3.93 | — | 3.86 | — |
| Negate | 3.34 | 3.11 | 3.28 | 3.06 |
| SubtractSumComposite | 14.53 | — | 14.11 | — |
| Fp add | 6.00 | 2.88 | 5.93 | 3.02 |
| Fp subtract | 6.02 | 2.91 | 6.04 | 2.90 |
| MultiplyComposite | 52.10 | 18.97 | 52.13 | 19.06 |
| ReduceWidePositive | 29.72 | — | 28.96 | — |
| ReduceWideDifference | 39.63 | — | 38.69 | — |
| Fp2 add | 13.65 | 5.32 | 13.42 | 5.18 |
| Fp2 subtract | 13.61 | 5.19 | 12.63 | 5.15 |
| Fp2 mul by 3 | 28.33 | 6.53 | 27.66 | 6.47 |
| Fp2 mul by 4 | 28.57 | 7.13 | 28.26 | 7.12 |
| Fp2 mul by 8 | 43.15 | 8.69 | 41.68 | 8.51 |
| MultiplyWideNoAlias | — | — | 20.58 | — |

### .NET 10.0.12

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| Fp multiply | 52.09 | 19.14 | 50.79 | 18.94 |
| Fp square | 51.02 | 18.27 | 50.48 | 18.11 |
| Fp2 multiply | 184.66 | 60.08 | 183.96 | 59.86 |
| Fp2 square | 116.91 | 39.92 | 117.92 | 40.59 |
| Fp inverse | 1816.09 | 1318.82 | 1841.48 | 1321.54 |
| MultiplyWide | 26.84 | — | 26.67 | — |
| ReduceWide | 43.23 | — | 43.06 | — |
| ReduceWideSmall | 36.61 | — | 36.59 | — |
| AddWide | 5.84 | — | 5.83 | — |
| SubtractWide | 5.84 | — | 5.85 | — |
| AddUnreduced | 2.16 | — | 2.08 | — |
| Halve | 3.84 | — | 3.84 | — |
| Negate | 3.27 | 3.09 | 3.27 | 3.14 |
| SubtractSumComposite | 14.53 | — | 14.26 | — |
| Fp add | 5.95 | 3.04 | 5.93 | 3.03 |
| Fp subtract | 5.96 | 2.89 | 5.96 | 2.90 |
| MultiplyComposite | 52.23 | 18.78 | 53.07 | 18.82 |
| ReduceWidePositive | 36.59 | — | 36.65 | — |
| ReduceWideDifference | 40.06 | — | 39.93 | — |
| Fp2 add | 14.02 | 5.36 | 13.42 | 5.19 |
| Fp2 subtract | 14.09 | 5.19 | 12.81 | 5.06 |
| Fp2 mul by 3 | 28.36 | 6.48 | 27.67 | 6.38 |
| Fp2 mul by 4 | 28.57 | 7.10 | 28.18 | 6.97 |
| Fp2 mul by 8 | 42.86 | 8.56 | 41.70 | 8.46 |
| MultiplyWideNoAlias | — | — | 27.32 | — |

## Final kernel modes and target frameworks, nanoseconds

Compact/expanded settings affect Fp square, wide kernels and Fp2 formulas. Fp multiply remains the same two-row CIOS kernel. These final mode captures are separate from the before/after captures above.

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

## Composite paths, microseconds

### Patched .NET 12-dev

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| Fp12 multiply | 1.098 | 1.145 | 1.098 | 1.148 |
| Fp12 square | 0.805 | 0.816 | 0.797 | 0.820 |
| Cyclotomic square | 0.522 | 0.494 | 0.506 | 0.496 |
| Miller 1 | 151.190 | 147.240 | 147.520 | 145.270 |
| Miller 8 | 890.220 | 832.930 | 849.560 | 827.490 |
| Final exponentiation | 189.250 | 206.260 | 185.350 | 204.800 |
| Pairing check 1 | 343.980 | 351.640 | 336.690 | 351.900 |
| Pairing check 2 | 448.940 | 448.920 | 435.120 | 447.050 |
| Pairing check 8 | 1074.610 | 1032.560 | 1033.240 | 1028.080 |
| Fp6 multiply | 0.365 | 0.376 | 0.365 | 0.374 |
| Fp6 square | 0.297 | 0.283 | 0.288 | 0.275 |
| Fp12 sparse multiply | 0.747 | 0.749 | 0.757 | 0.758 |
| G1 add+affine | 1.765 | 1.770 | 1.759 | 1.756 |
| G1 subgroup | 25.774 | 24.275 | 25.433 | 24.141 |
| G1 multiply | 39.996 | 46.703 | 39.051 | 45.719 |
| map G1 | 26.539 | 23.318 | 25.609 | 23.249 |
| G1 MSM 32 | 553.840 | 620.840 | 546.940 | 620.340 |
| G1 MSM 256 | 2693.200 | 2830.800 | 2728.000 | 2817.700 |
| G2 add+affine | 2.591 | 2.660 | 3.659 | 3.309 |
| G2 subgroup | 31.792 | 28.811 | 42.832 | 38.185 |
| G2 multiply | 74.047 | 94.058 | 100.817 | 127.041 |
| map G2 | 94.113 | 81.555 | 88.500 | 81.617 |
| G2 MSM 32 | 1106.800 | 1748.200 | 1082.000 | 1754.950 |
| G2 MSM 256 | 5315.000 | 8175.200 | 5131.600 | 8148.300 |

### .NET 11 RC1

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| Fp12 multiply | 2.876 | 1.146 | 2.848 | 1.141 |
| Fp12 square | 2.101 | 0.821 | 2.104 | 0.821 |
| Cyclotomic square | 1.481 | 0.497 | 1.481 | 0.496 |
| Miller 1 | 385.920 | 146.910 | 386.690 | 146.200 |
| Miller 8 | 2216.220 | 834.140 | 2199.410 | 825.670 |
| Final exponentiation | 501.870 | 207.070 | 493.000 | 202.270 |
| Pairing check 1 | 918.570 | 355.750 | 881.700 | 350.130 |
| Pairing check 2 | 1167.630 | 449.700 | 1149.950 | 452.680 |
| Pairing check 8 | 2712.580 | 1041.280 | 2740.330 | 1031.870 |
| Fp6 multiply | 0.926 | 0.376 | 0.921 | 0.373 |
| Fp6 square | 0.817 | 0.273 | 0.782 | 0.272 |
| Fp12 sparse multiply | 1.868 | 0.752 | 1.889 | 0.762 |
| G1 add+affine | 2.867 | 1.792 | 2.775 | 1.760 |
| G1 subgroup | 70.044 | 24.137 | 67.574 | 24.029 |
| G1 multiply | 103.138 | 45.341 | 99.270 | 45.933 |
| map G1 | 68.949 | 22.795 | 67.039 | 22.748 |
| G1 MSM 32 | 1396.150 | 610.600 | 1376.200 | 608.500 |
| G1 MSM 256 | 5623.500 | 2817.600 | 5601.100 | 2817.100 |
| G2 add+affine | 4.846 | 2.616 | 4.768 | 2.645 |
| G2 subgroup | 78.229 | 28.513 | 76.484 | 28.595 |
| G2 multiply | 178.425 | 93.581 | 173.544 | 93.263 |
| map G2 | 233.758 | 80.883 | 233.333 | 82.667 |
| G2 MSM 32 | 2514.900 | 1739.200 | 2438.200 | 1749.900 |
| G2 MSM 256 | 12672.600 | 8304.700 | 11968.400 | 8164.300 |

### .NET 10.0.12

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| Fp12 multiply | 3.466 | 1.141 | 3.465 | 1.137 |
| Fp12 square | 2.523 | 0.821 | 2.496 | 0.819 |
| Cyclotomic square | 1.638 | 0.497 | 1.714 | 0.496 |
| Miller 1 | 471.000 | 146.840 | 468.520 | 145.690 |
| Miller 8 | 2701.700 | 829.630 | 2683.560 | 821.920 |
| Final exponentiation | 565.600 | 204.570 | 576.660 | 202.610 |
| Pairing check 1 | 1042.030 | 353.510 | 1047.290 | 349.080 |
| Pairing check 2 | 1356.250 | 451.890 | 1364.030 | 451.950 |
| Pairing check 8 | 3273.600 | 1073.740 | 3269.400 | 1025.850 |
| Fp6 multiply | 1.158 | 0.377 | 1.131 | 0.373 |
| Fp6 square | 1.007 | 0.273 | 1.015 | 0.272 |
| Fp12 sparse multiply | 2.310 | 0.756 | 2.296 | 0.754 |
| G1 add+affine | 3.887 | 1.779 | 3.842 | 1.745 |
| G1 subgroup | 119.192 | 24.104 | 116.342 | 23.946 |
| G1 multiply | 177.556 | 45.669 | 174.247 | 45.382 |
| map G1 | 103.717 | 23.055 | 99.713 | 22.823 |
| G1 MSM 32 | 2436.900 | 612.500 | 2433.000 | 606.700 |
| G1 MSM 256 | 9381.400 | 2853.700 | 9202.400 | 2833.100 |
| G2 add+affine | 5.855 | 2.626 | 5.796 | 2.612 |
| G2 subgroup | 96.032 | 28.703 | 94.845 | 28.235 |
| G2 multiply | 225.700 | 93.723 | 222.700 | 92.662 |
| map G2 | 281.710 | 80.900 | 273.920 | 80.150 |
| G2 MSM 32 | 4012.900 | 1753.800 | 3980.000 | 1738.800 |
| G2 MSM 256 | 21681.900 | 8376.400 | 20978.600 | 8116.600 |


## Interleaved patched repeats

A later isolated capture visibly hit contention in G2 rows (both managed and native times rose). Three before/after rounds on processor 11 repeat the squares and complete curve/map/MSM sweep. Each ratio uses the managed/native pair from the same process; the table takes the median of those three ratios. It does not choose a native result separately to improve a ratio. Raw logs are paired-1/2/3-before/after-squares/curve.txt.

| Operation | Median managed/blst before | Median managed/blst after | Relative change |
|---|---:|---:|---:|
| Fp square | 1.0176 | 0.9951 | -2.2% |
| Fp2 square | 1.0590 | 1.0556 | -0.3% |
| G1 add+affine | 0.9995 | 0.9949 | -0.5% |
| G1 subgroup | 1.0638 | 1.0610 | -0.3% |
| G1 multiply | 0.8652 | 0.8508 | -1.7% |
| map G1 | 1.1237 | 1.1144 | -0.8% |
| G1 MSM 32 | 0.8773 | 0.8821 | +0.5% |
| G1 MSM 256 | 0.9625 | 0.9560 | -0.7% |
| G2 add+affine | 0.9767 | 0.9639 | -1.3% |
| G2 subgroup | 1.0956 | 1.0029 | -8.5% |
| G2 multiply | 0.7866 | 0.7391 | -6.0% |
| map G2 | 1.1587 | 1.0855 | -6.3% |
| G2 MSM 32 | 0.6335 | 0.6129 | -3.3% |
| G2 MSM 256 | 0.6561 | 0.6306 | -3.9% |

Repeated final Fp square captures are 18.49/18.49, 18.20/18.29 and 18.20/18.33 ns (managed/blst). This supports near parity, not a strong universal win. Small G1/control differences remain within VM noise.

## Disassembly in composite callers

These are static instructions in representative FullOpts bodies; MULX in a loop executes multiple times. Before profiles use net11.0 and after profiles net10.0 on the same patched runtime. Owned field files have no target-framework selection.

| Method | Bytes before → after | Stack reservation before → after | SETcc before → after | Calls before → after |
|---|---:|---:|---:|---:|
| Fp2:MultiplyExpanded | 366 → 366 | 416 → 416 | 0 → 0 | 6 → 6 |
| Fp2:SquareExpanded | 1150 → 1153 | 168 → 168 | 0 → 0 | 2 → 2 |
| Fp2:Subtract | 66 → 383 | 32 → 0 | 0 → 0 | 2 → 0 |
| Fp2:MulBy3 | 1636 → 724 | 120 → 72 | 44 → 0 | 0 → 0 |
| Fp2:MulBy4 | 1503 → 641 | 24 → 0 | 44 → 2 | 0 → 0 |
| Fp2:MulBy8 | 2177 → 895 | 24 → 0 | 66 → 4 | 0 → 0 |
| Fp2:Inverse | 862 → 862 | 232 → 232 | 10 → 10 | 5 → 5 |
| WideTower:Multiply2 | 376 → 376 | 416 → 416 | 0 → 0 | 6 → 6 |
| Pairing.Pairing:LineDouble | 763 → 763 | 992 → 992 | 0 → 0 | 30 → 30 |
| Pairing.Pairing:LineAdd | 862 → 862 | 1088 → 1088 | 0 → 0 | 30 → 30 |
| ``Curves.Jacobian`1[Nethermind.Crypto.BlsNet.Arithmetic.Fp2]:Double`` | 792 → 792 | 1472 → 1472 | 0 → 0 | 19 → 19 |
| ``Curves.CurveField`1[Nethermind.Crypto.BlsNet.Arithmetic.Fp]:Square`` | 946 → 957 | 40 → 136 | 0 → 1 | 0 → 0 |

Program:RunFpSquare: 1171 bytes, 200 stack bytes, 1 SETcc, 11 static MULX, 16 static MUL, 8 saved-register pushes.

Fp:MultiplyWideNoAliasExpanded: 969 bytes, 136 stack bytes, 0 SETcc, 36 static MULX, 0 static MUL, 8 saved-register pushes.

## Retained kernels and JIT changes

Expanded square computes fifteen off-diagonal products once, doubles them, adds six diagonal products through a bit-carry chain, then runs six reduction rows. It uses **57 dynamic widening products**, versus CIOS square's 72. Inputs are consumed before the public output, so aliases remain supported. Canonical inputs give a raw square below p² < pR and a Montgomery quotient below 2p. Compact mode retains CIOS square on stock .NET 10.

The old approximately 32 ns experiment ran in a large mixed benchmark method that exhausted carry analysis. This round's isolated triangular prototype initially took approximately 25.1 ns, with ordinary MUL, spills and materialized corrections. Enlarging scan allowances enabled its rows and correction chains (approximately 18.7 ns); the diagonal bit-carry chain and scalar MULX then brought it near native parity. Pinned blst's ADX sqrx_mont_384 calls __mulx_mont_384 and labels that as fast as dedicated squaring; its raw sqr_384 routine computes cross products once.

MultiplyWideNoAliasExpanded writes the low words directly to distinct output scratch and avoids the private twelve-word buffer and its final vector copy. Its output must be disjoint from both inputs; inputs may coincide. MultiplyWide retains output-overlap support. Fp2 multiply computes all three products before writing either public coordinate, preserving public left/right aliases. The caller still reserves 416 bytes and makes six calls. The helper is available to G's WideTower.Multiply2 or fused helpers, whose scratch contract is already disjoint; algorithm-owned files were left unchanged.

The JIT patch still applies to runtime PR #134039 at 331fa2b3, excludes eng/build.ps1, and contains no CR bytes:

- Full-adder allowance: 2,048 to 8,192 method nodes; 32 to 128 candidates.
- Current-IR liveness scans: 65,536 to 262,144 visits; extended carry-use scans: 16,384 to 65,536. Exhaustion still declines optimization.
- IsCarryFlagsCrossing selects MULX for unsigned BigMul with two local operands and accepts its exact multi-register result stores. Values and operand-read positions stay in place. New conversions require the runtime AVX2 ISA group, which includes BMI2. Signed BigMul stays excluded. Local operands prevent clearing containment from exposing a zero constant as a flag-writing XOR. Existing MULX register constraints remain.

The bit-carry, dead-on-exit, alias, exception-order and mask-select proofs are unchanged. These are larger bounded searches, not unbounded optimization. Returning the liveness allowance to 65,536 regressed the triangular square to approximately 23.6 ns.

## Remaining costs and removed experiments

Miller line functions and G2 doubling still have many field calls and substantial scratch frames. Their boundaries preserve the arithmetic values; the missed carries were inside Fp2 scale helpers. Fp6/Fp12 raw products still call the ordinary alias-safe multiplier, with multiple wide-helper boundaries and struct copies. Cyclotomic square retains early narrow reductions. Captured Fp2 inverse still has ten SETcc operations. One carry materializes at the final doubled cross-term word in symmetric square, whose expression becomes LEA. This report does not claim zero materialization in every inline context or universal native parity.

- A separate diagonal struct and twelve-word addition increased liveness/scratch costs (approximately 19.3–19.4 ns); the interleaved bit chain won.
- Inlining subtraction in Fp2 square retained another SETcc and gave no reliable speedup (approximately 42.5 versus 42.1 ns). Inlining both chains in public Fp2 subtraction did pay.
- Three CIOS rows per loop gave approximately 43.1 ns Fp2 square versus 42.1 with two.
- All-word shift doubling removed SETcc but took approximately 20.0 ns. Broad scalar-add operand hoisting did not establish a retained benefit and was removed. The faster carry chain keeps one terminal materialization.
- Unconditional symmetric square on stock .NET 10 improved the isolated kernel (approximately 47.6 versus 50.6 ns) but did not confirm a curve gain. G1 rows were 1.5–3% slower in that capture, while untouched controls also drifted. The established compact default remains. Preserved source timestamps initially retained a trial binary through incremental rebuilding; both targets were explicitly rebuilt and the final suites, matrices and captures repeated from restored source.

## Validation

Both targets build with zero warnings/errors using the .NET 11 SDK. Final verification explicitly rebuilds to avoid stale trial binaries. Native blst is available. All three console suites pass for five supported runtime/target combinations and both modes: thirty full-suite runs. Each pairing suite reports 995,273 checks, each curve suite 80,374 checks, and arithmetic passes all seven groups. Each kernel-only run reports 841,109 checks, including disjoint raw products, doubled unreduced inputs, preserved inputs, coincident inputs, square edges, native differential comparisons and public aliases. Cold Fp2 and cold Fp square each pass three checks in all ten combinations.

The final checked JIT passes eighteen regression entry points under default, JitStress=2, JitStressRegs=1/3/8, with tiering off/on: 180 entry-point passes. AVX2-disabled and ADX-disabled runs each pass all eighteen with tiering off/on (72 more). Both disabled-feature field runs pass 841,109 checks. The checked field matrix passes forty 841,109-check runs across both targets/modes and ten stress/tiering configurations, plus twelve full-suite runs. No test/build processes run concurrently.

CompositeCarry checks sixteen unrolled six-word additions/subtractions against BigInteger, with observed carry/borrow totals, full-word edges and output aliases. Each body changes from 96 SETcc to one. ScalarProductCarry checks scalar products between carry limbs under register pressure, signed products, observed carries, output aliases with either input, and independent/dependent doubled tails for 32/64-bit words. The patch passes an isolated-index applicability check against the exact base.

## Reproduction and handoff

```powershell
& C:\Temp\blst-speedy\dotnet11\dotnet.exe build Nethermind.Crypto.BlsNet.slnx -c Release -t:Rebuild -m:1 -nodeReuse:false
./scripts/build-native-blst.ps1 -TowerBenchmarks
./scripts/test-field-runtime-matrix.ps1 `
  -SdkDotnet C:\Temp\blst-speedy\dotnet11\dotnet.exe `
  -Stock10Dotnet 'C:\Program Files\dotnet\dotnet.exe' `
  -Stock11Dotnet C:\Temp\blst-speedy\dotnet11\dotnet.exe `
  -PatchedDotnet C:\Temp\blst-speedy\th-F\dotnet.exe -Cpu 11 -Timings
$env:DOTNET_ROLL_FORWARD='Major'
$env:DOTNET_TieredCompilation='0'
$env:BLST_BENCH_PROCESSOR='11'
$env:BLST_BENCH_ROUND_MS='3'
$env:BLST_MSM_SIZES='32,256'
& C:\Temp\blst-speedy\th-F\dotnet.exe tests\Nethermind.Crypto.BlsNet.PairingTests\bin\Release\net10.0\Nethermind.Crypto.BlsNet.PairingTests.dll --perf
& C:\Temp\blst-speedy\th-F\dotnet.exe tests\Nethermind.Crypto.BlsNet.CurveTests\bin\Release\net10.0\Nethermind.Crypto.BlsNet.CurveTests.dll --bench
```

Ignored artifacts/field-study/round3 retains baseline binaries, before/final JITs, sequential capture/matrix scripts, logs, disassembly and removed source variants. The report is also copied to C:\Temp\blst-speedy\agentF-final.md before replacing the shared testhost JIT. The final JIT is installed there only at the end, following validation and publication of the handoff.

Final release JIT SHA-256: `4B1D6CDA360E374663015C2401C1AD1115706989A1BB5E10807EB1CE96CB7279`.
