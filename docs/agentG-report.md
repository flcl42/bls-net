# Agent G algorithm-layer report

Historical prototype results: the later merge omits commit `8b06b0a` and its
repeated-point/identity-exponent shortcuts. See the [round 3 report](agentG-round3-report.md)
for the current implementation, normal-input integration profiles and timings
after the field-kernel merge.

Measured on 2026-10-13 in the supplied Windows VM (Ryzen 9 9950X3D,
12 exposed logical processors). All values below are **microseconds per operation**.
The baseline library is `a6d129e`; implementation commits are `19c18df` and
`8b06b0a`. Nethermind decoder changes are committed as `769e5ba`.
No repository was pushed. Agent F's Fp/Fp2 source changes were not merged;
the final patched measurements include his update to the shared JIT.

The changes improve final exponentiation, G1/G2 single multiplication and MSM.
The final exponentiation shortcut also removes the hard chain for cancelling
pairs with the same G2 point. Repeated-point MSMs sum their scalars modulo r
before one multiplication. **Universal blst parity is not achieved**: the
general pairing path, subgroup checks and maps still have gaps; stock runtimes
also retain substantial MSM gaps.

## Measurement method

The .NET 11 SDK built both net10.0 and net11.0 test runners. Stock .NET 10 ran
the net10.0 assembly; stock .NET 11 and the patched host ran the net11.0 assembly.
`DOTNET_ROLL_FORWARD=Major` and the appropriate `DOTNET_ROOT` were set.
Managed and native used identical inputs and loop shapes within each process.
The native component reference is pinned blst `f62244ef`, built with ADX/BMI2.
Test-only exports expose blst's internal Fp6 multiply/square formulas.

Component processes were pinned to logical processor 10, warmed for at least
0.5 seconds per operation, and sampled best-of-21. Tower rounds used 1,000
iterations; pairing rounds used 10. Curve rounds calibrated toward 3 ms, with
at least one iteration. Patched baseline and final rows use the same final JIT.
Stock baseline tower rows use fresh serial captures, replacing earlier captures
affected by concurrent benchmark contention. Final rows use the final source
capture. No native value was selected independently to alter
a ratio. Shared-host contention remains visible, particularly in long MSMs and
some untouched operations; differences of a few percent are not strong evidence.

The final patched `clrjit.dll` SHA-256 is
`CE33640E302FE92C4BD55718EE79D35C444796733F3CA2101C8585F19F8489A2`.
Agent F updated the shared JIT during this work. The patched baseline was
remeasured after that update to isolate these algorithm changes. These results
do not predict timings after his eventual Fp/Fp2 source merge.

The algorithm and reduction counts, rejected experiments, alias/bound invariants,
and remaining bottlenecks are in [algorithm-audit.md](algorithm-audit.md).
The tables retain native values in both captures so host drift can be assessed.

On the matched patched captures, general final exponentiation falls from
230.840 to 209.420 us (9.3%), G1 multiplication from 44.269 to 40.579 us (8.3%)
and G2 multiplication from 112.650 to 80.238 us (28.8%). Distinct-point G2 MSM
32 falls from 2,116.400 to 1,216.700 us (42.5%); all sampled patched MSM sizes
are below their same-process native times. The general Miller loop changes
little. Untouched Fp6/Fp12 arithmetic rows are included as controls; fluctuations
in them, including native Fp6 multiply in the stock .NET 11 baseline, are not
algorithm speedups.


## Patched .NET 12.0.0-dev: tower and pairing

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| Fp6 multiply | 0.420 | 0.377 | 0.434 | 0.382 |
| Fp6 square | 0.330 | 0.275 | 0.342 | 0.274 |
| Fp12 multiply | 1.274 | 1.157 | 1.274 | 1.159 |
| Fp12 square | 0.935 | 0.824 | 0.944 | 0.822 |
| Fp12 sparse multiply | 0.875 | 0.761 | 0.894 | 0.768 |
| Cyclotomic square | 0.579 | 0.508 | 0.566 | 0.502 |
| Miller 1 | 173.950 | 147.570 | 174.150 | 147.120 |
| Miller 8 | 995.760 | 830.350 | 998.570 | 841.560 |
| Final exponentiation | 230.840 | 204.820 | 209.420 | 207.100 |
| Pairing check 1 | 405.790 | 352.590 | 389.740 | 358.580 |
| Pairing check 2 | 523.620 | 449.630 | 517.960 | 461.560 |
| Pairing check 8 | 1226.150 | 1035.680 | 1213.400 | 1041.350 |

## Patched .NET 12.0.0-dev: curves and maps

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1 add+affine | 1.780 | 1.795 | 1.796 | 1.792 |
| G1 subgroup | 26.203 | 24.586 | 26.642 | 24.682 |
| G1 multiply | 44.269 | 46.413 | 40.579 | 46.633 |
| map G1 | 26.159 | 22.867 | 26.186 | 23.026 |
| G2 add+affine | 2.801 | 2.673 | 2.791 | 2.691 |
| G2 subgroup | 34.194 | 29.401 | 34.007 | 29.270 |
| G2 multiply | 112.650 | 95.046 | 80.238 | 95.351 |
| map G2 | 100.486 | 82.334 | 100.003 | 81.952 |

## Patched .NET 12.0.0-dev: G1 MSM sweep

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1 MSM 1 | 43.604 | 67.147 | 39.696 | 67.610 |
| G1 MSM 2 | 85.094 | 86.452 | 63.676 | 86.007 |
| G1 MSM 4 | 122.977 | 129.627 | 100.900 | 129.734 |
| G1 MSM 8 | 191.713 | 214.707 | 170.882 | 213.800 |
| G1 MSM 16 | 343.238 | 402.137 | 325.033 | 393.722 |
| G1 MSM 32 | 686.375 | 626.800 | 557.760 | 624.880 |
| G1 MSM 64 | 1111.050 | 1014.550 | 948.667 | 1010.500 |
| G1 MSM 128 | 1835.200 | 1673.100 | 1607.200 | 1678.000 |
| G1 MSM 256 | 3152.000 | 2868.500 | 2689.000 | 2861.700 |
| G1 MSM 512 | 5046.000 | 4921.400 | 4385.400 | 4868.000 |
| G1 MSM 1024 | 8124.400 | 8884.300 | 8059.900 | 8610.600 |
| G1 MSM 2048 | 13862.100 | 15495.200 | 13706.200 | 15290.400 |
| G1 MSM 4096 | 23894.000 | 27603.000 | 24155.700 | 27549.800 |

## Patched .NET 12.0.0-dev: G2 MSM sweep

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G2 MSM 1 | 111.900 | 162.496 | 78.827 | 162.219 |
| G2 MSM 2 | 235.650 | 211.175 | 150.684 | 211.621 |
| G2 MSM 4 | 341.100 | 322.688 | 270.510 | 322.790 |
| G2 MSM 8 | 560.060 | 549.200 | 500.020 | 547.960 |
| G2 MSM 16 | 1000.950 | 890.300 | 851.700 | 888.033 |
| G2 MSM 32 | 2116.400 | 1783.400 | 1216.700 | 1774.750 |
| G2 MSM 64 | 3446.000 | 2909.400 | 1920.800 | 2871.300 |
| G2 MSM 128 | 5077.900 | 4867.600 | 3305.800 | 4793.800 |
| G2 MSM 256 | 7928.000 | 8378.100 | 5797.300 | 8234.300 |
| G2 MSM 512 | 13139.700 | 14165.600 | 10077.400 | 14016.600 |
| G2 MSM 1024 | 22226.100 | 25103.400 | 18285.700 | 24594.700 |
| G2 MSM 2048 | 38459.300 | 44283.300 | 33655.500 | 43740.100 |
| G2 MSM 4096 | 68123.500 | 79584.600 | 62523.400 | 78572.300 |

## .NET 11 RC1: tower and pairing

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| Fp6 multiply | 1.986 | 0.674 | 1.087 | 0.383 |
| Fp6 square | 0.890 | 0.273 | 0.893 | 0.279 |
| Fp12 multiply | 3.218 | 1.153 | 3.324 | 1.149 |
| Fp12 square | 2.337 | 0.825 | 2.412 | 0.823 |
| Fp12 sparse multiply | 2.118 | 0.765 | 2.201 | 0.766 |
| Cyclotomic square | 1.542 | 0.497 | 1.546 | 0.503 |
| Miller 1 | 424.830 | 148.170 | 437.920 | 147.820 |
| Miller 8 | 2407.420 | 835.670 | 2474.550 | 832.930 |
| Final exponentiation | 614.710 | 203.650 | 528.130 | 204.080 |
| Pairing check 1 | 1044.300 | 355.730 | 970.640 | 353.240 |
| Pairing check 2 | 1338.140 | 458.080 | 1265.820 | 454.700 |
| Pairing check 8 | 3202.910 | 1088.750 | 3007.460 | 1038.950 |

## .NET 11 RC1: curves and maps

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1 add+affine | 2.977 | 1.864 | 2.913 | 1.784 |
| G1 subgroup | 72.792 | 25.234 | 69.810 | 24.271 |
| G1 multiply | 116.972 | 48.160 | 103.007 | 46.118 |
| map G1 | 72.722 | 23.651 | 69.273 | 22.737 |
| G2 add+affine | 5.621 | 2.719 | 5.504 | 2.669 |
| G2 subgroup | 86.871 | 29.341 | 83.871 | 28.694 |
| G2 multiply | 293.430 | 95.770 | 197.873 | 94.647 |
| map G2 | 256.527 | 81.500 | 250.855 | 80.182 |

## .NET 11 RC1: G1 MSM sweep

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1 MSM 1 | 114.812 | 69.204 | 101.869 | 67.155 |
| G1 MSM 2 | 230.842 | 86.958 | 162.312 | 86.076 |
| G1 MSM 4 | 326.750 | 130.175 | 258.773 | 129.173 |
| G1 MSM 8 | 510.140 | 215.360 | 433.350 | 212.383 |
| G1 MSM 16 | 896.533 | 402.433 | 825.550 | 388.600 |
| G1 MSM 32 | 1758.000 | 632.000 | 1397.200 | 616.700 |
| G1 MSM 64 | 2901.900 | 1036.800 | 2373.600 | 991.300 |
| G1 MSM 128 | 4865.500 | 1718.400 | 4025.800 | 1666.200 |
| G1 MSM 256 | 8279.600 | 2962.300 | 5656.300 | 2867.200 |
| G1 MSM 512 | 11263.500 | 5045.800 | 9451.400 | 4866.700 |
| G1 MSM 1024 | 19902.000 | 8973.900 | 18193.200 | 8628.500 |
| G1 MSM 2048 | 34332.800 | 15741.000 | 31414.200 | 15288.100 |
| G1 MSM 4096 | 59235.200 | 28373.800 | 55378.700 | 27462.500 |

## .NET 11 RC1: G2 MSM sweep

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G2 MSM 1 | 292.070 | 165.690 | 195.060 | 161.120 |
| G2 MSM 2 | 605.275 | 213.350 | 375.229 | 211.914 |
| G2 MSM 4 | 888.400 | 332.033 | 679.825 | 320.925 |
| G2 MSM 8 | 1461.000 | 550.900 | 1258.600 | 537.150 |
| G2 MSM 16 | 2634.900 | 911.200 | 2161.400 | 870.900 |
| G2 MSM 32 | 5581.900 | 1820.900 | 2924.200 | 1762.100 |
| G2 MSM 64 | 8919.100 | 2924.300 | 4726.600 | 2864.600 |
| G2 MSM 128 | 11960.300 | 4925.900 | 8045.400 | 4805.600 |
| G2 MSM 256 | 19498.000 | 8425.500 | 14121.600 | 8245.600 |
| G2 MSM 512 | 32351.500 | 14164.000 | 24518.300 | 13928.200 |
| G2 MSM 1024 | 55452.300 | 25312.400 | 44935.600 | 24544.800 |
| G2 MSM 2048 | 95798.000 | 45019.100 | 82611.900 | 43703.000 |
| G2 MSM 4096 | 173798.000 | 81648.200 | 151565.800 | 80302.300 |

## .NET 10.0.12: tower and pairing

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| Fp6 multiply | 1.174 | 0.389 | 1.247 | 0.380 |
| Fp6 square | 1.022 | 0.280 | 1.016 | 0.276 |
| Fp12 multiply | 3.498 | 1.159 | 3.733 | 1.170 |
| Fp12 square | 2.579 | 0.827 | 2.712 | 0.830 |
| Fp12 sparse multiply | 2.367 | 0.763 | 2.493 | 0.762 |
| Cyclotomic square | 1.726 | 0.499 | 1.731 | 0.504 |
| Miller 1 | 474.330 | 148.160 | 494.190 | 148.650 |
| Miller 8 | 2693.860 | 831.580 | 2821.390 | 837.940 |
| Final exponentiation | 683.660 | 206.290 | 596.930 | 206.340 |
| Pairing check 1 | 1182.910 | 355.740 | 1096.960 | 353.720 |
| Pairing check 2 | 1485.830 | 454.050 | 1425.440 | 452.060 |
| Pairing check 8 | 3406.960 | 1058.830 | 3432.330 | 1048.500 |

## .NET 10.0.12: curves and maps

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1 add+affine | 3.982 | 1.876 | 4.009 | 1.784 |
| G1 subgroup | 122.039 | 25.409 | 122.675 | 24.312 |
| G1 multiply | 199.021 | 47.707 | 184.544 | 46.556 |
| map G1 | 104.243 | 23.889 | 104.768 | 23.011 |
| G2 add+affine | 6.353 | 2.765 | 6.178 | 2.685 |
| G2 subgroup | 104.400 | 29.457 | 100.021 | 29.403 |
| G2 multiply | 336.188 | 96.225 | 231.942 | 95.908 |
| map G2 | 297.520 | 82.440 | 289.730 | 81.530 |

## .NET 10.0.12: G1 MSM sweep

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1 MSM 1 | 197.207 | 70.367 | 180.613 | 67.475 |
| G1 MSM 2 | 389.886 | 87.600 | 289.467 | 84.667 |
| G1 MSM 4 | 572.325 | 131.725 | 458.217 | 126.083 |
| G1 MSM 8 | 900.000 | 217.250 | 774.667 | 209.167 |
| G1 MSM 16 | 1594.100 | 402.200 | 1463.200 | 386.700 |
| G1 MSM 32 | 3090.400 | 637.400 | 2474.700 | 617.700 |
| G1 MSM 64 | 5006.900 | 1015.400 | 4167.200 | 984.700 |
| G1 MSM 128 | 8374.300 | 1737.600 | 7054.300 | 1640.300 |
| G1 MSM 256 | 14347.100 | 2985.300 | 9729.600 | 2859.200 |
| G1 MSM 512 | 18882.100 | 5051.900 | 16408.200 | 4824.800 |
| G1 MSM 1024 | 31495.200 | 8891.600 | 31507.600 | 8525.600 |
| G1 MSM 2048 | 54756.100 | 15776.600 | 54868.300 | 15071.600 |
| G1 MSM 4096 | 97717.100 | 28562.000 | 97025.600 | 27941.200 |

## .NET 10.0.12: G2 MSM sweep

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G2 MSM 1 | 337.625 | 166.225 | 231.558 | 164.258 |
| G2 MSM 2 | 722.950 | 215.575 | 443.450 | 211.933 |
| G2 MSM 4 | 1045.850 | 330.050 | 794.433 | 326.033 |
| G2 MSM 8 | 1696.400 | 559.500 | 1463.500 | 546.800 |
| G2 MSM 16 | 3050.100 | 907.800 | 2496.600 | 884.500 |
| G2 MSM 32 | 6182.600 | 1820.800 | 4276.200 | 1805.200 |
| G2 MSM 64 | 10061.700 | 2979.300 | 7477.000 | 2895.000 |
| G2 MSM 128 | 16821.100 | 4958.300 | 12838.800 | 4863.400 |
| G2 MSM 256 | 28887.200 | 8456.100 | 22558.300 | 8364.400 |
| G2 MSM 512 | 49084.200 | 14377.200 | 40879.900 | 14067.500 |
| G2 MSM 1024 | 85697.000 | 24996.600 | 73368.200 | 24820.700 |
| G2 MSM 2048 | 152734.700 | 45188.500 | 137212.300 | 44803.000 |
| G2 MSM 4096 | 273956.400 | 80108.900 | 234458.400 | 79236.100 |

## Integration profile

These are warmed full calls to all seven managed/native precompiles through a
temporary probe referencing Nethermind's built assemblies, plus separate
decode/curve-check, subgroup and encoding timings. They are not BenchmarkDotNet
means or flame graphs. All integration assemblies target net10.0 on all runtimes,
so G2 uses the net10.0 bucket policy. The process was pinned to processor 10 and
started with `DOTNET_PROCESSOR_COUNT=1` so decoder workers did not oversubscribe
the one available processor. Native and managed received the same wire input.

G1/G2 addition uses the generator twice; MSM uses 32 identical generator points
and deterministic full-width scalars; maps use 42 and (42,17); pairing uses
(P,Q),(-P,Q). These deliberately expose common repeated/identity cases and must
not be treated as a random-distinct-point MSM or arbitrary pairing benchmark.
The component tables above measure distinct random points and nontrivial final
exponents. The integration baseline uses the old library with the **same new
decoder** as the final run; separate decode rows compare the old duplicate
validation pattern with the new decoder. Native integration uses Nethermind's
bundled Nethermind.Crypto.Bls 0.3.17.15, not the component DLL.


### Patched .NET 12.0.0-dev: complete calls

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1ADD | 2.671 | 2.935 | 2.589 | 2.850 |
| G2ADD | 4.387 | 4.683 | 4.405 | 4.718 |
| G1MSM 32 | 1400.300 | 1417.100 | 923.533 | 1422.800 |
| G2MSM 32 | 2655.400 | 2543.800 | 1227.050 | 2547.000 |
| MAP G1 | 28.540 | 28.264 | 28.408 | 28.336 |
| MAP G2 | 104.689 | 97.590 | 104.561 | 98.566 |
| PAIRING 2 | 666.050 | 640.550 | 429.483 | 642.425 |

### .NET 11 RC1: complete calls

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1ADD | 4.010 | 2.833 | 4.001 | 2.823 |
| G2ADD | 7.371 | 4.695 | 7.351 | 4.653 |
| G1MSM 32 | 3583.100 | 1421.250 | 2425.800 | 1418.200 |
| G2MSM 32 | 6566.300 | 2543.200 | 2969.700 | 2554.000 |
| MAP G1 | 72.190 | 28.132 | 72.656 | 28.075 |
| MAP G2 | 255.300 | 97.245 | 258.782 | 97.603 |
| PAIRING 2 | 1647.400 | 643.025 | 1049.100 | 641.275 |

### .NET 10.0.12: complete calls

| Operation | Managed before | blst before | Managed after | blst after |
|---|---:|---:|---:|---:|
| G1ADD | 6.655 | 2.817 | 6.718 | 2.831 |
| G2ADD | 8.616 | 4.652 | 8.726 | 4.663 |
| G1MSM 32 | 10235.000 | 1435.900 | 6807.800 | 1425.850 |
| G2MSM 32 | 7402.800 | 2548.400 | 3500.500 | 2543.600 |
| MAP G1 | 170.712 | 27.993 | 159.106 | 27.945 |
| MAP G2 | 288.040 | 97.483 | 291.800 | 97.133 |
| PAIRING 2 | 2106.400 | 639.950 | 1432.600 | 637.750 |

### Decode/check/encode costs

| Step | Patched | .NET 11 | .NET 10 |
|---|---:|---:|---:|
| Decode G1 incl curve | 0.440 | 0.525 | 0.683 |
| Decode G2 incl curve | 0.726 | 1.176 | 1.395 |
| Decode Fp before | 0.134 | 0.181 | 0.193 |
| Decode Fp after | 0.125 | 0.169 | 0.200 |
| G1 subgroup | 26.610 | 71.354 | 210.100 |
| G2 subgroup | 34.765 | 85.312 | 99.017 |
| Encode G1 | 0.172 | 0.234 | 0.302 |
| Encode G2 | 0.335 | 0.450 | 0.612 |


Decoding/encoding is a small fraction of these calls. Subgroup validation and
the main curve/pairing arithmetic dominate. The library avoids inversions for
already-normalized points, skips infinity subgroup work, skips an identity hard
exponent, and collapses repeated-point MSMs. The integration removes duplicate
canonical-range checking and Montgomery conversion of zero. It retains all
curve/subgroup checks and their error precedence; no probabilistic batch
subgroup test was substituted for validation.

The component loops allocated zero managed heap bytes. MSM expansion uses pooled
storage (2n points/scalar slots for G1, 4n for G2), so first use and storage size
increase; warmed allocations remained zero in the curve assertions. Integration
output arrays and decoder worker state still allocate. The probe's per-call byte
counts are current-thread measurements and exclude allocations on helpers; raw
logs retain them. No whole-process zero-allocation claim is made.

## Validation and reproduction

The full arithmetic, curve and pairing console suites passed on all three
runtimes with native blst available. Each runtime passed 80,419 curve checks and
632,908 pairing/tower checks. All three Nethermind runs passed 1,106 selected
managed BLS tests, including seven groups of 3,000 random native differential
inputs, large MSM cases, field boundaries and the existing EIP vectors.

New assertions cover scalar decomposition reconstruction/bounds, repeated-point
scalar sums and MSMs against BigInteger identities/native multiplication,
compressed square runs against ordinary and native squaring (including zero,
one, aliases and the threshold), normalized affine conversion, and 2,500 random
Fp6-subfield final exponents against native blst with aliases. Existing tests
cover all scalar edges/bits, maps, cancellation, infinity, MSM thresholds and
Miller batches through 130 pairs. Both final library and Nethermind builds
reported zero warnings and zero errors. The Nethermind `BlsNetSource` override
was supplied only on build commands; its original project default is unchanged.

From this clone:

```powershell
& 'C:\Temp\blst-speedy\dotnet11\dotnet.exe' build Nethermind.Crypto.BlsNet.slnx -c Release
./scripts/build-native-blst.ps1 -TowerBenchmarks
$env:BLST_BENCH_PROCESSOR = '10'
$env:BLST_BENCH_ROUND_MS = '3'
$env:DOTNET_ROLL_FORWARD = 'Major'
$env:DOTNET_ROOT = 'C:\Temp\blst-speedy\rpr\artifacts\bin\testhost\net11.0-windows-Release-x64'
& "$env:DOTNET_ROOT\dotnet.exe" tests/Nethermind.Crypto.BlsNet.PairingTests/bin/Release/net11.0/Nethermind.Crypto.BlsNet.PairingTests.dll --perf
& "$env:DOTNET_ROOT\dotnet.exe" tests/Nethermind.Crypto.BlsNet.CurveTests/bin/Release/net11.0/Nethermind.Crypto.BlsNet.CurveTests.dll --bench
```

For stock runtimes select their root and the appropriate target-framework folder.
The local `artifacts/agentG/` directory retains raw timing/build/test logs, the
baseline worktree, serial run scripts and the integration probe. It is ignored
build/experiment output; this report contains the durable results.

## What remains slower, and why

The general Fp6/Fp12 and Miller paths retain a gap despite matching blst's main
product counts. Wide Fp2 product/reduction calls, signed correction traffic,
scratch spills and early Fp4/Fp6 square reductions remain. The hard exponent now
uses fewer products than blst but adds ten base-field inversions in its general
compressed path; its residual reduction count is still higher. G2 doubling and
subgroup checking repeatedly pay the three-reduction narrow Fp2 multiplication
cost versus blst's two. Maps retain the same root/cofactor chains and inherit
that cost. Stock runtimes retain the much larger materialized-carry penalty.

In the final patched component capture, managed/native ratios are 1.18 for
Miller 1, 1.01 for final exponentiation, 1.09/1.12/1.17 for pairing checks
1/2/8, 1.08/1.16 for G1/G2 subgroup checks, and 1.14/1.22 for G1/G2 maps.
These are general-input component ratios; the faster cancelling-pair and
repeated-point integration results do not erase these gaps.

All sampled patched MSM sizes beat native in this capture. G1/G2 affine
addition remains near parity; subgroup checks and maps remain slower. The tables
show each case rather than claiming universal wins. Expanded MSMs also use more
pooled memory. The next useful work is to merge/retest Agent F's fused Fp2/wide
kernels and reconsider the widened Fp4 square with those kernels. The widened
square, broad inlining and wide-copy variants were measured and removed because
they did not pay off with the current field layer. The report does not claim
that every difference from blst was closed.

