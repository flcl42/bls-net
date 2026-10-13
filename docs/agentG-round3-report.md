# Algorithm layer, round 3

Round 3 starts at merged commit `75a7ccc`, including Agent F's round 2 field
kernels and runtime-selected policy. Retained implementation commits are
`958780e`, `388bcf6` and `6287023`. Use the complete series: `388bcf6` restores the original
compact root/point calls after stock .NET 10 timing exposed a regression in the
intermediate implementation. No repository was pushed. Agent F's reserved
existing field files and JIT patch were not edited.

The unmerged repeated-point MSM and identity-exponent shortcuts are excluded.
Curve sweeps use distinct random subgroup points, and pairing measurements use
nontrivial products. No matching-point or cancelling-product fast path was added.

## Measurement method

All table values are **microseconds per operation**. Captured 2026-10-13 on the
supplied Ryzen 9 9950X3D Windows VM with 12 exposed logical processors. Final
before/after captures pin processor 3 and run serially. Processor 10 experiments
showed contention outliers, so they are retained as experiment logs rather than
substituted selectively into these final tables.

Before and after use the same harness, inputs and final shared patched JIT.
Managed and native measurements are paired within each process. Both native
columns are retained to expose shared-host clock drift; differences of a few
percent are not decisive. These are best-of-21 warmed samples, not statistical
means or confidence intervals. Warmup is at least 0.5 s per operation. Tower
rounds use 1,000 iterations and pairing rounds 10; curve rounds calibrate toward
3 ms, with at least one iteration. The runners' existing native imports include
their P/Invoke overhead; these tables do not measure bare assembly kernels.
The field-kernel reports use a different microbenchmark harness and should not
be compared as if its import/tiering settings were identical.

The .NET 11 SDK builds both library targets. Console runners default to net11.0;
`-p:BlsNetTestTargetFramework=net10.0` builds the stock .NET 10 runners. Stock
.NET 11 and patched use net11.0. Selection follows the executing runtime:
compact on .NET 10, expanded on .NET 11/patched. Test overrides also passed.

Patched host: .NET 12.0.0-dev in the supplied net11.0 testhost directory. Final
`clrjit.dll` SHA-256, checked before and after the complete capture:
`4B1D6CDA360E374663015C2401C1AD1115706989A1BB5E10807EB1CE96CB7279`.
Agent F updated the shared JIT at the end of his round 3 work. Patched baseline,
final, integration and correctness captures were repeated on that exact JIT.
His round 3 C# field source changes remain unmerged in this clone.
Native components: blst `f62244ef50ad1a603decdb8f215e982d2a467bb6`, ADX/BMI2
build with test-only Fp6 exports. DLL SHA-256:
`C5F850ACD4A57E81151E331AA8B3AF0E6793CEB807CBAE133414170E050723E1`.

## Retained changes and bounds

- Expanded wide Fp2 products reuse output scratch and the three-product fused
  subtraction. Raw product counts are unchanged.
- Expanded Fp6 square keeps products wide until its six final reductions.
  Wide square components lie in [0,2p²), using a modular real difference.
  Final C0/C1 magnitudes stay below 8p² and use the small signed reducer;
  C2 can reach 12p² and uses the full signed reducer. Since 9p² < pR and
  12p² < 3pR, these satisfy their contracts.
- Base-field product sums/differences reduce once; complex sums/differences
  reduce twice. Canonical products give |real| < 2p² and complex sum imaginary
  values in [0,4p²), so the positive reducer applies there. All inputs are
  consumed before output writes. Point completion and map Horner steps use
  these helpers in expanded mode.
- Fused subtraction chains and concrete Fp squaring calls reduce call/scratch
  overhead. Expanded Jacobian point kernels write final coordinates directly
  in an order that preserves aliases. Compact mode retains the old coordinate
  and root-chain calls. Both root specializations execute the same fixed exponent.
- Isogenies generate only their required powers (G2: four slots rather than
  sixteen, eliminating twelve Fp2 multiplies). Monic leading terms omit a
  multiply by one; even powers use squaring where the policy makes it useful.
  G2 SSWU's -2-u, imaginary and equal-component constants use their specialized
  formulas. These are fixed-coefficient formulas for arbitrary field inputs.
- Expanded Miller batches pair adjacent lines. Their product has C1.C0=0;
  forming it costs 18 base-field products/10 reductions, then a five-coefficient
  accumulator multiply costs 51/12. Two separate sparse multiplies cost 78/24.
  The first two lines initialize the accumulator directly. Odd trailing lines
  use the original sparse operation. Scratch spans are trimmed to the active
  count, including infinity gaps and partial batches. Compact mode keeps its
  original loop.
- A pre-existing affine-bucket cancellation bug was found by Nethermind's random
  differential test (G2 MSM, seed 2540, case 2637, 38 points), and reproduced in
  baseline `75a7ccc`. A nonempty bucket could reduce to affine infinity but was
  then assigned projective scale one, producing the invalid finite point (0,0).
  Final bucket conversion now preserves infinity. Algebraic/native regressions
  cover completely cancelled and partially cancelled buckets in both groups.

## Source operation counts

An Fp product is a logical six-limb product, including one embedded in a CIOS
call; it is not a MULX instruction count. Reductions count Montgomery reduction,
excluding modular corrections on additions/subtractions. Inversion's internal
binary-GCD work is listed separately. Counts below use expanded mode.

| Operation | Before products / reductions | After products / reductions | blst products / reductions |
|---|---:|---:|---:|
| Fp6 multiply | 18 / 6 | 18 / 6 | 18 / 6 |
| Fp6 square | 12 / 10 | 12 / 6 | 12 / 6 |
| Fp12 multiply | 54 / 12 | 54 / 12 | 54 / 12 |
| Fp12 square | 36 / 12 | 36 / 12 | 36 / 12 |
| One sparse multiply | 39 / 12 | 39 / 12 | 39 / 12 |
| Two adjacent sparse multiplies | 78 / 24 | 69 / 22 | 78 / 24 |
| Full cyclotomic square | 18 / 18 | 18 / 18 | 18 / 12 |
| Compressed square | 12 / 12 | 12 / 12 | — |
| Fp12 inverse, plus one base inversion | 97 / 60 | 97 / 52 | 97 / 52 |
| Miller doubling line, including G1 evaluation | 29 / 26 | 29 / 26 | 29 / 26 |
| Miller addition line, including G1 evaluation | 39 / 30 | 39 / 28 | 39 / 30 |
| Miller 1 | 6,867 / 3,336 | 6,867 / 3,326 | 6,867 / 3,336 |
| Miller 8 | 39,585 / 21,564 | 37,125 / 20,940 | 39,585 / 21,564 |
| General final exponent, plus 11 managed / 1 native base inversions | 6,485 / 4,948 | 6,485 / 4,940 | 7,733 / 4,308 |

For eight active pairs, paired accumulation saves 2,460 products and 544
reductions. The line-add product difference saves another 80 reductions.
The 63 doubling / five addition schedule and 62 shared squarings remain unchanged.
Karabina decompression retains 19 products/16 reductions and one
base inversion per nonzero-denominator reconstruction; zero/identity cases are
the inherited algorithm's boundary handling.

Curve formulas retain 5S+2M doubling, 4S+12M projective addition and 3S+8M mixed
addition. Their product-difference completion saves one reduction in G1 and two
in G2. Expanded G2 completion therefore uses 30 reductions for projective addition
and 20 for mixed addition, versus 32 and 22 for separate products. Cofactor and
subgroup chains remain unchanged. No probabilistic batch validation was added.
The [historical audit](algorithm-audit.md) records the original formula counts.


## Patched .NET 12.0.0-dev: tower and pairing

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| Fp6 multiply | 0.374 | 0.374 | 0.374 | 0.380 |
| Fp6 square | 0.299 | 0.274 | 0.285 | 0.278 |
| Fp12 multiply | 1.094 | 1.135 | 1.101 | 1.150 |
| Fp12 square | 0.808 | 0.816 | 0.802 | 0.816 |
| Fp12 sparse multiply | 0.752 | 0.754 | 0.764 | 0.757 |
| Fp12 two sparse multiplies | 1.513 | 1.519 | 1.457 | 1.529 |
| Cyclotomic square | 0.515 | 0.498 | 0.512 | 0.498 |
| Miller 1 | 148.580 | 146.010 | 149.190 | 146.880 |
| Miller 8 | 853.960 | 821.950 | 831.410 | 833.620 |
| Final exponentiation | 187.110 | 202.530 | 184.910 | 204.430 |
| Pairing check 1 | 338.520 | 352.490 | 338.460 | 351.010 |
| Pairing check 2 | 441.360 | 446.860 | 435.650 | 453.860 |
| Pairing check 8 | 1051.420 | 1028.380 | 1033.260 | 1034.350 |

## Patched .NET 12.0.0-dev: curves and maps

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1 add+affine | 1.794 | 1.788 | 1.773 | 1.762 |
| G1 subgroup | 25.943 | 24.410 | 24.394 | 24.187 |
| G1 multiply | 39.697 | 46.280 | 37.992 | 45.815 |
| map G1 | 25.892 | 22.767 | 24.853 | 22.759 |
| G2 add+affine | 2.690 | 2.652 | 2.620 | 2.655 |
| G2 subgroup | 29.902 | 29.013 | 27.942 | 28.832 |
| G2 multiply | 72.510 | 94.662 | 68.247 | 94.170 |
| map G2 | 91.262 | 81.497 | 86.363 | 81.829 |

## Patched .NET 12.0.0-dev: G1 MSM sweep

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1 MSM 1 | 39.068 | 66.956 | 37.451 | 66.657 |
| G1 MSM 2 | 62.264 | 85.482 | 60.768 | 86.240 |
| G1 MSM 4 | 98.055 | 127.862 | 94.723 | 126.810 |
| G1 MSM 8 | 165.489 | 209.744 | 160.111 | 210.267 |
| G1 MSM 16 | 314.050 | 387.850 | 305.617 | 388.650 |
| G1 MSM 32 | 545.820 | 621.900 | 535.100 | 615.460 |
| G1 MSM 64 | 920.767 | 999.200 | 906.300 | 999.633 |
| G1 MSM 128 | 1556.500 | 1645.500 | 1525.400 | 1645.300 |
| G1 MSM 256 | 2701.700 | 2864.800 | 2605.800 | 2812.200 |
| G1 MSM 512 | 4445.800 | 4843.000 | 4276.100 | 4794.900 |
| G1 MSM 1024 | 8180.800 | 8552.400 | 7867.900 | 8471.300 |
| G1 MSM 2048 | 13924.500 | 15203.600 | 13407.800 | 15088.900 |
| G1 MSM 4096 | 24288.500 | 27257.100 | 23650.500 | 27387.300 |

## Patched .NET 12.0.0-dev: G2 MSM sweep

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G2 MSM 1 | 71.395 | 160.488 | 67.175 | 160.173 |
| G2 MSM 2 | 141.695 | 210.315 | 129.859 | 208.405 |
| G2 MSM 4 | 248.318 | 323.609 | 230.775 | 320.692 |
| G2 MSM 8 | 461.740 | 540.680 | 429.233 | 541.483 |
| G2 MSM 16 | 779.967 | 876.967 | 738.400 | 873.333 |
| G2 MSM 32 | 1153.700 | 1765.750 | 1091.300 | 1751.600 |
| G2 MSM 64 | 1786.000 | 2858.400 | 1716.900 | 2836.800 |
| G2 MSM 128 | 3046.700 | 4854.400 | 2951.200 | 4734.800 |
| G2 MSM 256 | 5286.200 | 8192.800 | 5153.300 | 8199.600 |
| G2 MSM 512 | 9078.900 | 13805.700 | 8834.700 | 13889.800 |
| G2 MSM 1024 | 16644.800 | 24205.300 | 15940.000 | 24209.800 |
| G2 MSM 2048 | 30629.100 | 43033.700 | 29340.600 | 43025.700 |
| G2 MSM 4096 | 54958.700 | 77214.900 | 53344.300 | 77442.300 |

## .NET 11 RC1: tower and pairing

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| Fp6 multiply | 0.931 | 0.376 | 0.959 | 0.380 |
| Fp6 square | 0.821 | 0.288 | 0.747 | 0.283 |
| Fp12 multiply | 2.879 | 1.147 | 2.956 | 1.177 |
| Fp12 square | 2.096 | 0.820 | 2.139 | 0.848 |
| Fp12 sparse multiply | 1.922 | 0.755 | 1.966 | 0.759 |
| Fp12 two sparse multiplies | 3.794 | 1.564 | 3.867 | 1.530 |
| Cyclotomic square | 1.493 | 0.506 | 1.489 | 0.497 |
| Miller 1 | 394.540 | 146.890 | 398.230 | 146.760 |
| Miller 8 | 2242.050 | 837.060 | 2244.570 | 831.320 |
| Final exponentiation | 506.090 | 204.270 | 507.130 | 208.340 |
| Pairing check 1 | 892.810 | 350.920 | 899.490 | 361.730 |
| Pairing check 2 | 1166.790 | 452.270 | 1157.450 | 457.340 |
| Pairing check 8 | 2748.380 | 1040.200 | 2734.880 | 1041.830 |

## .NET 11 RC1: curves and maps

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1 add+affine | 2.866 | 1.775 | 2.836 | 1.749 |
| G1 subgroup | 71.163 | 24.278 | 68.912 | 23.879 |
| G1 multiply | 104.176 | 46.288 | 100.121 | 45.669 |
| map G1 | 69.883 | 22.938 | 66.451 | 22.528 |
| G2 add+affine | 5.030 | 2.650 | 4.969 | 2.640 |
| G2 subgroup | 78.630 | 28.749 | 76.959 | 28.914 |
| G2 multiply | 179.000 | 94.450 | 173.181 | 92.750 |
| map G2 | 235.958 | 81.092 | 227.142 | 80.825 |

## .NET 11 RC1: G1 MSM sweep

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1 MSM 1 | 102.281 | 66.989 | 99.714 | 66.379 |
| G1 MSM 2 | 163.438 | 84.650 | 160.583 | 85.433 |
| G1 MSM 4 | 258.518 | 126.200 | 247.000 | 126.367 |
| G1 MSM 8 | 432.250 | 210.367 | 414.143 | 207.400 |
| G1 MSM 16 | 834.800 | 390.467 | 790.700 | 386.067 |
| G1 MSM 32 | 1404.050 | 616.550 | 1369.200 | 617.000 |
| G1 MSM 64 | 2371.800 | 1006.300 | 2277.500 | 989.500 |
| G1 MSM 128 | 4045.400 | 1650.700 | 3857.500 | 1648.500 |
| G1 MSM 256 | 5719.100 | 2863.200 | 5631.200 | 2839.500 |
| G1 MSM 512 | 9580.600 | 4859.600 | 9329.100 | 4799.800 |
| G1 MSM 1024 | 18016.200 | 8520.400 | 17934.500 | 8494.700 |
| G1 MSM 2048 | 31282.200 | 15136.500 | 30849.700 | 15071.000 |
| G1 MSM 4096 | 55347.200 | 27367.000 | 54152.200 | 27005.600 |

## .NET 11 RC1: G2 MSM sweep

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G2 MSM 1 | 177.300 | 160.756 | 171.524 | 158.600 |
| G2 MSM 2 | 340.575 | 208.750 | 325.288 | 207.000 |
| G2 MSM 4 | 609.800 | 316.450 | 578.600 | 315.740 |
| G2 MSM 8 | 1121.100 | 535.800 | 1068.600 | 534.150 |
| G2 MSM 16 | 1896.700 | 868.900 | 1792.900 | 864.100 |
| G2 MSM 32 | 2559.900 | 1757.400 | 2505.500 | 1741.000 |
| G2 MSM 64 | 4131.300 | 2850.400 | 4095.500 | 2867.900 |
| G2 MSM 128 | 7075.500 | 4787.500 | 6882.700 | 4719.000 |
| G2 MSM 256 | 12446.200 | 8233.000 | 12098.000 | 8116.700 |
| G2 MSM 512 | 21646.500 | 13920.900 | 21007.000 | 13691.500 |
| G2 MSM 1024 | 39086.000 | 24582.200 | 38191.100 | 24153.700 |
| G2 MSM 2048 | 71679.100 | 43337.400 | 70756.300 | 43312.000 |
| G2 MSM 4096 | 130772.500 | 77843.700 | 128148.900 | 78293.800 |

## .NET 10.0.12: tower and pairing

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| Fp6 multiply | 1.147 | 0.374 | 1.141 | 0.377 |
| Fp6 square | 0.987 | 0.285 | 1.026 | 0.287 |
| Fp12 multiply | 3.434 | 1.143 | 3.386 | 1.133 |
| Fp12 square | 2.531 | 0.819 | 2.496 | 0.815 |
| Fp12 sparse multiply | 2.287 | 0.752 | 2.330 | 0.761 |
| Fp12 two sparse multiplies | 4.586 | 1.516 | 4.623 | 1.522 |
| Cyclotomic square | 1.644 | 0.493 | 1.777 | 0.499 |
| Miller 1 | 470.780 | 147.180 | 472.380 | 147.730 |
| Miller 8 | 2689.400 | 822.580 | 2708.710 | 824.580 |
| Final exponentiation | 563.810 | 202.730 | 593.630 | 203.070 |
| Pairing check 1 | 1050.600 | 351.680 | 1067.600 | 350.180 |
| Pairing check 2 | 1374.890 | 449.670 | 1385.150 | 449.030 |
| Pairing check 8 | 3271.930 | 1034.390 | 3287.600 | 1032.190 |

## .NET 10.0.12: curves and maps

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1 add+affine | 3.873 | 1.752 | 3.862 | 1.757 |
| G1 subgroup | 116.612 | 23.920 | 119.152 | 23.932 |
| G1 multiply | 175.756 | 45.413 | 175.662 | 45.294 |
| map G1 | 100.190 | 22.483 | 100.513 | 22.453 |
| G2 add+affine | 6.169 | 2.621 | 6.135 | 2.648 |
| G2 subgroup | 98.713 | 28.460 | 96.194 | 28.519 |
| G2 multiply | 229.908 | 93.775 | 224.775 | 93.592 |
| map G2 | 289.350 | 80.620 | 275.290 | 79.920 |

## .NET 10.0.12: G1 MSM sweep

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1 MSM 1 | 171.853 | 65.706 | 173.065 | 66.188 |
| G1 MSM 2 | 283.170 | 84.150 | 282.670 | 83.660 |
| G1 MSM 4 | 453.417 | 125.450 | 451.517 | 124.967 |
| G1 MSM 8 | 770.967 | 207.300 | 767.900 | 207.267 |
| G1 MSM 16 | 1465.200 | 385.800 | 1466.550 | 385.550 |
| G1 MSM 32 | 2428.300 | 608.300 | 2449.400 | 606.400 |
| G1 MSM 64 | 4149.000 | 993.500 | 4186.300 | 981.700 |
| G1 MSM 128 | 7086.800 | 1671.100 | 7227.500 | 1659.600 |
| G1 MSM 256 | 9417.000 | 2839.800 | 9405.100 | 2872.200 |
| G1 MSM 512 | 15947.700 | 4811.400 | 16021.400 | 4815.200 |
| G1 MSM 1024 | 31278.500 | 8621.300 | 30925.000 | 8541.200 |
| G1 MSM 2048 | 53975.700 | 15237.800 | 53270.800 | 14997.800 |
| G1 MSM 4096 | 94851.200 | 27186.800 | 94279.500 | 26949.000 |

## .NET 10.0.12: G2 MSM sweep

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G2 MSM 1 | 228.000 | 160.442 | 219.954 | 159.869 |
| G2 MSM 2 | 437.267 | 207.317 | 422.517 | 204.533 |
| G2 MSM 4 | 788.400 | 318.700 | 760.400 | 312.867 |
| G2 MSM 8 | 1448.400 | 534.650 | 1412.950 | 538.450 |
| G2 MSM 16 | 2454.200 | 862.000 | 2431.300 | 859.100 |
| G2 MSM 32 | 4245.700 | 1794.400 | 4170.500 | 1738.800 |
| G2 MSM 64 | 7362.300 | 2840.700 | 7298.500 | 2815.800 |
| G2 MSM 128 | 12578.500 | 4745.800 | 12502.700 | 4717.500 |
| G2 MSM 256 | 21914.800 | 8181.400 | 21965.400 | 8126.100 |
| G2 MSM 512 | 39638.200 | 13790.100 | 40450.700 | 13891.200 |
| G2 MSM 1024 | 71629.800 | 24339.700 | 71599.100 | 24347.200 |
| G2 MSM 2048 | 132009.900 | 43192.700 | 132420.700 | 43817.700 |
| G2 MSM 4096 | 229661.100 | 77394.700 | 228071.900 | 77861.600 |

## Normal-input precompile profile

The scratch Nethermind clone remains at decoder commit `769e5ba`. Both profile
phases use that same decoder and differ only in the linked library (merged
`75a7ccc` before, round 3 after). No Nethermind source/project changes were made.
The temporary probe executes all seven precompiles, compares every output with
native before timing, and targets net10.0 on all runtimes. The runtime-selected
expanded kernels/paired loop still run on the patched host.

The dataset uses 32 **distinct random subgroup points** per group and random
full-width scalars; additions use different points; maps use random field inputs;
pairing uses two different random pairs with a verified nontrivial product.
Input hashes match before/after. There are no repeated-generator MSMs or cancelling
pairs in these profiles. Setup/serialization of input is outside the timed loop.
The process pins processor 3, sets `DOTNET_PROCESSOR_COUNT=1`, warms at least
0.5 s and takes the best of 21 rounds calibrated toward 3 ms. This avoids decoder
workers competing on the pinned processor. Returned output allocations are included.

The native integration reference is Nethermind.Crypto.Bls 0.3.17.15 bundled with
the scratch clone. Its columns are separate from the pinned component-native
DLL above and cannot be substituted for those values.

The final patched integration table uses one complete repeated before/after
capture. An earlier final-JIT capture had an isolated managed G1 map outlier
(40 us, versus about 28 us in subsequent captures); the entire paired capture
was repeated, rather than choosing individual rows from different runs.


### Patched .NET 12.0.0-dev: complete precompile calls

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1ADD | 2.582 | 2.783 | 2.612 | 2.779 |
| G2ADD | 4.157 | 4.570 | 4.269 | 4.602 |
| G1MSM 32 | 1440.500 | 1602.200 | 1480.400 | 1605.700 |
| G2MSM 32 | 2175.800 | 3136.400 | 2189.000 | 3122.700 |
| MAP G1 | 27.831 | 27.725 | 27.916 | 27.707 |
| MAP G2 | 94.519 | 96.533 | 94.703 | 95.745 |
| PAIRING 2 | 580.520 | 633.675 | 565.740 | 631.050 |

### .NET 11 RC1: complete precompile calls

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1ADD | 3.968 | 2.758 | 3.929 | 2.812 |
| G2ADD | 7.064 | 4.579 | 6.891 | 4.628 |
| G1MSM 32 | 3728.600 | 1610.200 | 3680.700 | 1603.700 |
| G2MSM 32 | 5195.100 | 3145.300 | 5158.700 | 3138.400 |
| MAP G1 | 73.110 | 27.624 | 69.476 | 27.681 |
| MAP G2 | 238.592 | 96.277 | 235.250 | 97.080 |
| PAIRING 2 | 1464.200 | 632.400 | 1470.550 | 630.775 |

### .NET 10.0.12: complete precompile calls

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1ADD | 6.529 | 2.765 | 6.365 | 2.760 |
| G2ADD | 8.618 | 4.557 | 8.642 | 4.586 |
| G1MSM 32 | 10535.700 | 1633.500 | 10373.100 | 1633.200 |
| G2MSM 32 | 7457.400 | 3124.600 | 7419.300 | 3151.000 |
| MAP G1 | 166.376 | 27.996 | 170.631 | 27.715 |
| MAP G2 | 294.811 | 96.897 | 290.160 | 96.439 |
| PAIRING 2 | 1984.300 | 629.925 | 2027.400 | 637.100 |

## Validation

All arithmetic, curve and pairing console suites passed on .NET 10, stock
.NET 11 and the patched runtime with native available. Each default runtime
passed **82,198 curve checks and 1,260,011 pairing/tower checks**. Full curve and
pairing suites also passed with expanded mode forced on .NET 10 and compact
mode forced on .NET 11/patched. Both target builds reported zero warnings/errors.

New checks include signed bounds, all 6⁴ coefficient-edge combinations and
3,000 random cases for wide squares/product combinations, BigInteger/native
comparisons, every input alias, scalar-coefficient aliases, sparse-line formation
and accumulator aliases, concrete root-chain agreement with a general exponent,
independent affine polynomial identities for rescaled isogeny inputs, and point
output aliases. Existing tests cover 2,000 random native maps/scalars per group,
MSM thresholds, infinity/cancellation, and odd/even/multiple Miller batches
through 130 inputs. Warmed allocation assertions still pass.

Nethermind linked-source/precompile/test builds reported zero warnings/errors.
Each of the three runtimes passed **1,106 selected managed BLS integration tests**,
including native random differential cases, large MSMs, field boundaries and EIP
vectors. `BlsNetSource` was overridden only on build commands; its original
project default remains unchanged. The scratch clone is clean.

## Reproduction

```powershell
& 'C:\Temp\blst-speedy\dotnet11\dotnet.exe' build Nethermind.Crypto.BlsNet.slnx -c Release
& 'C:\Temp\blst-speedy\dotnet11\dotnet.exe' build Nethermind.Crypto.BlsNet.slnx -c Release -p:BlsNetTestTargetFramework=net10.0
$env:DOTNET_ROOT='C:\Temp\blst-speedy\rpr\artifacts\bin\testhost\net11.0-windows-Release-x64'
$env:DOTNET_ROLL_FORWARD='Major'
$env:BLST_BENCH_PROCESSOR='3'
$env:BLST_BENCH_ROUND_MS='3'
& "$env:DOTNET_ROOT\dotnet.exe" tests/Nethermind.Crypto.BlsNet.PairingTests/bin/Release/net11.0/Nethermind.Crypto.BlsNet.PairingTests.dll --perf
& "$env:DOTNET_ROOT\dotnet.exe" tests/Nethermind.Crypto.BlsNet.CurveTests/bin/Release/net11.0/Nethermind.Crypto.BlsNet.CurveTests.dll --bench
```

Select the appropriate root/target folder for stock runtimes. Unset benchmark
variables for suites; `BLSNET_EXPANDED_KERNELS=0/1` selects the alternate policy
before process initialization. Native Fp6 rows require the optional
`scripts/build-native-blst.ps1 -TowerBenchmarks` build. The ignored local
`artifacts/agentG-round3/` retains raw before/after/test/build logs, the manifest,
baseline worktree, serial scripts and integration probe; the tables here are
the durable capture.

## Rejected experiments and remaining gaps

Wide Fp4 square was revisited after the kernel merge. Three wide complex squares
with four final reductions passed differential/bound/alias checks but did not
beat the narrow formula. Inlining the square helper improved it modestly;
two wide squares plus a wide product did not establish a gain either. Isolated
samples were around 149 ns wide versus 147–149 ns narrow. The extra scratch,
wide-combination and call costs absorbed the reduction saving, so those variants
were removed. The wider Fp6 square did pay off and is retained.

Unconditionally changing root squaring calls regressed .NET 10 maps; measured
compact fallbacks restore the original calls. Compact point formulas were also
restored to protect stock timing. Blanket kernel expansion on .NET 10 remains
an explicit correctness-test override, not its default policy.

The component tables still show slower maps, G1 subgroup checks, the single-pair
Miller loop, cyclotomic square and some small tower operations on the patched
host. General pairing checks 1/2 beat native in this capture; check 8 and
Miller 8 are near parity. All sampled
patched MSM sizes beat their same-process component-native times; individual
small control rows vary with host clock/layout. Stock .NET 10 remains far above
native despite preserving its compact kernels, and stock .NET 11 retains
materialized-carry costs. Universal component blst parity is **not achieved**.

In the normal-input patched precompile profile, addition, MSM and pairing beat
the bundled native reference. G2 mapping is around parity; G1 mapping retains
about a 0.8% gap, within shared-host variation. This does not establish universal
precompile parity across inputs. Compact .NET 10 component maps/multiplication
remain near their baseline and the original algorithms are retained. The full
call table includes small variations in either direction (including roughly
2.6% for its G1 map sample); no claim of a statistically proven zero regression
is made from these best-round samples.

The remaining gap is no longer explained simply by multiplying/reducing more:
paired Miller accumulation and map/point completions now use fewer reductions
than their reference formulas. Full cyclotomic/Fp4 square retains extra reductions
because the wider replacement was slower. Concrete squaring and fewer wrappers
help, but managed calls, scratch traffic and register allocation remain costs;
native inner field calls also avoid the ABI overhead visible in exported-kernel
microbenchmarks. That attribution is an inference from operation counts and
the measured failed/retained variants, not a hardware-counter profile.
Further work should retest after merging Agent F's subsequent square/kernel
source improvements. His final shared JIT is included in both patched phases,
but none of those unmerged C# source changes is included in this capture.

