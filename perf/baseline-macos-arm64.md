# macOS ARM64 baseline at 74ed6106

Captured 2026-10-19 on GitHub `macos-15`, Apple M1 (Virtual), three cores,
7 GiB RAM, macOS 15.7.9, Apple clang 17.0.0 / Xcode 16.4. Stock is .NET
11.0.0-rc.2.26504.105; the built runtime identifies as .NET 12.0.0-ci.
These tables use the unpatched runtime commit: no additional runtime patches.
Native is pinned blst `f62244ef50ad1a603decdb8f215e982d2a467bb6` with ARM64 assembly.

[Successful final manual dispatch and downloadable artifacts](https://github.com/flcl42/bls-net/actions/runs/39315754704).
The warm measurement job took **20m03s**, including a 7s cache restore and
**4m51s** to rebuild Release and Checked JITs (129s and 162s respectively).
Its functional/JIT validation took 2m58s; benchmark/disassembly captures took
10m19s. All stages execute sequentially within the machine.

The [successful cold build](https://github.com/flcl42/bls-net/actions/runs/39309342059)
took **27m05s** for the testhost, 30m19s including both JITs, and 44m58s for
the full measurement job. That cold run included the comment-only cache probe;
the non-JIT source fingerprint stayed identical when the probe was removed for
this unpatched repeat. The compressed testhost/bootstrap SDK cache was 233 MiB.

Both policies passed 7,595,581 native checks and 7,285,065 managed-only checks.
All 21 opt/Add entry points passed in all 20 Release/Checked, tiering and stress
configurations in both macOS runs. Full-library ARM64 disassembly is retained
for stock and patched hosts with both policies.

**macOS captures are unpinned.** Shared-host clock/load variation is visible in
some native medians. Use each host's own native column and ratio; small timing
differences are weak evidence. Each table uses three alternating repetitions
and medians of the existing quick harness's best-round samples.

Runtime: `flcl42/runtime@74ed610683646896bfd9f22bbbcea93124b44f4a`; library: `1857b51af7414a709bbfebeb3a71b884c1eb3ee7`.

Medians of 3 alternating captures per host/policy; all times in **µs**.
CPU affinity: **unpinned**.
Each host measures native blst in the same process. Ratios use that host’s own native median.
ReadyToRun and tiering are disabled. The harness reports best rounds; shared-runner noise remains.

```text
Release JIT build: 129 seconds
Checked JIT build: 162 seconds
Cold testhost build: 0 seconds; cache hit: true
Runtime setup total: 291 seconds
```

JIT regression matrix: **20/20 passed** (Release + Checked, tiering 0/1, default/stress2/regs1/3/8).

## Compact kernels

| Operation | Stock .NET 11 | Patched | blst (stock / patched) | Stock / blst | Patched / blst |
|---|---:|---:|---:|---:|---:|
| CT Fp inverse | 5.462 | 4.821 | 2.918 / 2.899 | 1.87x | 1.66x |
| Fp inverse (variable, same inputs) | 2.534 | 2.075 | 2.918 / 2.899 | 0.87x | 0.72x |
| CT G1 multiply | 223.261 | 126.994 | 80.837 / 80.595 | 2.76x | 1.58x |
| G1 multiply (variable, same inputs) | 165.043 | 91.046 | 80.837 / 80.595 | 2.04x | 1.13x |
| CT G2 multiply | 470.290 | 256.750 | 151.306 / 150.012 | 3.11x | 1.71x |
| G2 multiply (variable, same inputs) | 304.762 | 157.819 | 151.306 / 150.012 | 2.01x | 1.05x |
| CT map G1 | 103.807 | 51.969 | 40.045 / 39.820 | 2.59x | 1.31x |
| map G1 (variable, same inputs) | 104.357 | 51.099 | 40.045 / 39.820 | 2.61x | 1.28x |
| CT map G2 | 372.369 | 184.136 | 134.551 / 134.407 | 2.77x | 1.37x |
| map G2 (variable, same inputs) | 370.100 | 182.742 | 134.551 / 134.407 | 2.75x | 1.36x |
| Fr mul+add | 0.052 | 0.030 | 0.048 / 0.024 | 1.09x | 1.27x |
| Fp mul+add | 0.103 | 0.053 | 0.067 / 0.048 | 1.55x | 1.11x |
| Fp multiply | 0.082 | 0.039 | 0.031 / 0.031 | 2.66x | 1.26x |
| Fp square | 0.080 | 0.039 | 0.042 / 0.027 | 1.91x | 1.44x |
| Fp2 multiply | 0.289 | 0.148 | 0.105 / 0.094 | 2.74x | 1.58x |
| Fp2 square | 0.177 | 0.090 | 0.081 / 0.064 | 2.19x | 1.42x |
| Fp inverse (kernel) | 2.399 | 1.988 | 2.930 / 2.919 | 0.82x | 0.68x |
| Fp inverse (tower) | 2.411 | 2.001 | 2.927 / 2.899 | 0.82x | 0.69x |
| Fp inverse Fermat | 45.334 | 27.416 | — / — | — | — |
| Fp12 multiply | 4.637 | 2.506 | 1.702 / 1.751 | 2.72x | 1.43x |
| Pairing check 2 | 1886.942 | 972.479 | 731.175 / 741.033 | 2.58x | 1.31x |
| G1 multiply | 164.167 | 91.417 | 80.209 / 79.875 | 2.05x | 1.14x |
| map G1 | 102.250 | 50.500 | 39.292 / 39.334 | 2.60x | 1.28x |
| G1 MSM 128 | 6392.917 | 3437.958 | 2754.250 / 2786.000 | 2.32x | 1.23x |
| G2 multiply | 301.375 | 156.500 | 148.167 / 148.000 | 2.03x | 1.06x |
| map G2 | 364.750 | 182.541 | 132.000 / 132.875 | 2.76x | 1.37x |
| G2 MSM 128 | 16867.208 | 8743.000 | 7389.125 / 7509.500 | 2.28x | 1.16x |

## Expanded kernels

| Operation | Stock .NET 11 | Patched | blst (stock / patched) | Stock / blst | Patched / blst |
|---|---:|---:|---:|---:|---:|
| CT Fp inverse | 5.501 | 4.788 | 2.919 / 2.941 | 1.88x | 1.63x |
| Fp inverse (variable, same inputs) | 2.565 | 2.094 | 2.919 / 2.941 | 0.88x | 0.71x |
| CT G1 multiply | 220.095 | 119.540 | 80.936 / 80.542 | 2.72x | 1.48x |
| G1 multiply (variable, same inputs) | 158.770 | 83.672 | 80.936 / 80.542 | 1.96x | 1.04x |
| CT G2 multiply | 430.269 | 230.776 | 149.954 / 149.679 | 2.87x | 1.54x |
| G2 multiply (variable, same inputs) | 272.086 | 135.050 | 149.954 / 149.679 | 1.81x | 0.90x |
| CT map G1 | 100.882 | 48.893 | 39.801 / 40.002 | 2.53x | 1.22x |
| map G1 (variable, same inputs) | 98.913 | 49.020 | 39.801 / 40.002 | 2.49x | 1.23x |
| CT map G2 | 348.879 | 168.439 | 133.316 / 134.177 | 2.62x | 1.26x |
| map G2 (variable, same inputs) | 345.086 | 166.775 | 133.316 / 134.177 | 2.59x | 1.24x |
| Fr mul+add | 0.052 | 0.029 | 0.024 / 0.024 | 2.23x | 1.22x |
| Fp mul+add | 0.101 | 0.052 | 0.048 / 0.047 | 2.13x | 1.11x |
| Fp multiply | 0.080 | 0.039 | 0.032 / 0.030 | 2.53x | 1.26x |
| Fp square | 0.076 | 0.033 | 0.027 / 0.027 | 2.77x | 1.21x |
| Fp2 multiply | 0.273 | 0.120 | 0.095 / 0.094 | 2.89x | 1.28x |
| Fp2 square | 0.203 | 0.083 | 0.064 / 0.064 | 3.18x | 1.30x |
| Fp inverse (kernel) | 2.512 | 1.988 | 2.917 / 2.914 | 0.86x | 0.68x |
| Fp inverse (tower) | 2.449 | 1.982 | 2.906 / 2.897 | 0.84x | 0.68x |
| Fp inverse Fermat | 47.437 | 27.702 | — / — | — | — |
| Fp12 multiply | 4.342 | 2.400 | 1.690 / 1.735 | 2.57x | 1.38x |
| Pairing check 2 | 1774.950 | 914.942 | 711.471 / 711.408 | 2.49x | 1.29x |
| G1 multiply | 158.709 | 83.167 | 79.542 / 80.417 | 2.00x | 1.03x |
| map G1 | 98.375 | 47.625 | 39.333 / 93.875 | 2.50x | 0.51x |
| G1 MSM 128 | 6130.000 | 3185.542 | 2762.541 / 2754.292 | 2.22x | 1.16x |
| G2 multiply | 270.166 | 134.042 | 148.208 / 148.125 | 1.82x | 0.90x |
| map G2 | 343.958 | 165.167 | 132.167 / 132.458 | 2.60x | 1.25x |
| G2 MSM 128 | 11262.125 | 5905.458 | 7411.417 / 7492.917 | 1.52x | 0.79x |

