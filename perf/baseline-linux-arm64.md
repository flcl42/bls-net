# Linux ARM64 baseline at 74ed6106

Captured 2026-10-19 on GitHub `ubuntu-24.04-arm`, four-core ARM Neoverse-N2,
Ubuntu 24.04.5, clang 18.1.3, CPU 0 pinned. Stock is .NET
11.0.0-rc.2.26504.105; the built runtime identifies as .NET 12.0.0-ci.
There are no additional runtime patches in this baseline. Native is pinned blst
`f62244ef50ad1a603decdb8f215e982d2a467bb6`, built with its ARM64 assembly.

[Successful cold run and downloadable artifacts](https://github.com/flcl42/bls-net/actions/runs/39304746175).
This first artifact nests its results under `artifacts/perf/`; subsequent runs
put `summary.md` and the other results directly at the artifact root.
The measurement job took 30m35s: 15m04s cold testhost build, 2s Release JIT
refresh (already built by the cold subset), 1m32s Checked JIT build, 2m43s
functional/JIT validation, and 10m04s benchmark/disassembly captures. Runtime
setup totaled 16m38s. The compressed testhost/bootstrap SDK cache was 237 MiB.

Both policies passed 7,595,581 checks with native blst and 7,285,065 with
`--no-native`. All 21 opt/Add entry points passed in every one of the 20
Release/Checked, tiering and stress configurations (420 successful test entries).
ARM64 disassembly is retained for both hosts and policies, including ADCS/SBCS
in the patched kernels. Three paired repetitions supply each table median.

The [second successful Linux job](https://github.com/flcl42/bls-net/actions/runs/39309342059)
changed only a comment in the runtime JIT through `perf/patches/cache-probe.patch`.
It restored the same non-JIT cache in 2s, rebuilt Release and Checked JITs in
3m18s, and completed the entire measurement job in **17m12s**, with all tests
passing again. This demonstrates a changed JIT source tree taking the warm path.

Runtime: `flcl42/runtime@74ed610683646896bfd9f22bbbcea93124b44f4a`; library: `48bf683b8ab9b58209fe899949f7a176369bd57a`.

Medians of 3 alternating, single-core captures per host/policy; all times in **µs**.
Each host measures native blst in the same process. Ratios use that host’s own native median.
ReadyToRun and tiering are disabled. The harness reports best rounds; shared-runner noise remains.

```text
Release JIT build: 2 seconds
Checked JIT build: 92 seconds
Cold testhost build: 904 seconds; cache hit: false
Runtime setup total: 998 seconds
```

JIT regression matrix: **20/20 passed** (Release + Checked, tiering 0/1, default/stress2/regs1/3/8).

## Compact kernels

| Operation | Stock .NET 11 | Patched | blst (stock / patched) | Stock / blst | Patched / blst |
|---|---:|---:|---:|---:|---:|
| CT Fp inverse | 5.481 | 4.832 | 2.858 / 2.857 | 1.92x | 1.69x |
| Fp inverse (variable, same inputs) | 3.528 | 3.062 | 2.858 / 2.857 | 1.23x | 1.07x |
| CT G1 multiply | 223.111 | 147.452 | 89.235 / 89.234 | 2.50x | 1.65x |
| G1 multiply (variable, same inputs) | 167.468 | 108.722 | 89.235 / 89.234 | 1.88x | 1.22x |
| CT G2 multiply | 501.105 | 330.320 | 183.437 / 183.526 | 2.73x | 1.80x |
| G2 multiply (variable, same inputs) | 322.660 | 204.458 | 183.437 / 183.526 | 1.76x | 1.11x |
| CT map G1 | 102.494 | 62.718 | 44.894 / 44.894 | 2.28x | 1.40x |
| map G1 (variable, same inputs) | 101.930 | 62.299 | 44.894 / 44.894 | 2.27x | 1.39x |
| CT map G2 | 389.688 | 239.129 | 160.867 / 160.863 | 2.42x | 1.49x |
| map G2 (variable, same inputs) | 387.612 | 237.438 | 160.867 / 160.863 | 2.41x | 1.48x |
| Fr mul+add | 0.047 | 0.029 | 0.024 / 0.024 | 1.96x | 1.22x |
| Fp mul+add | 0.096 | 0.059 | 0.045 / 0.045 | 2.13x | 1.31x |
| Fp multiply | 0.080 | 0.050 | 0.039 / 0.039 | 2.03x | 1.29x |
| Fp square | 0.080 | 0.050 | 0.034 / 0.034 | 2.31x | 1.46x |
| Fp2 multiply | 0.296 | 0.188 | 0.113 / 0.113 | 2.62x | 1.66x |
| Fp2 square | 0.187 | 0.119 | 0.085 / 0.085 | 2.20x | 1.41x |
| Fp inverse (kernel) | 3.379 | 2.946 | 2.857 / 2.856 | 1.18x | 1.03x |
| Fp inverse (tower) | 3.384 | 2.950 | 2.861 / 2.860 | 1.18x | 1.03x |
| Fp inverse Fermat | 49.945 | 35.523 | — / — | — | — |
| Fp12 multiply | 5.178 | 3.106 | 2.157 / 2.156 | 2.40x | 1.44x |
| Pairing check 2 | 2016.884 | 1238.459 | 854.086 / 853.489 | 2.36x | 1.45x |
| G1 multiply | 169.004 | 109.771 | 89.146 / 89.346 | 1.90x | 1.23x |
| map G1 | 101.731 | 62.097 | 44.793 / 44.825 | 2.27x | 1.39x |
| G1 MSM 128 | 6577.965 | 4312.097 | 3410.577 / 3410.110 | 1.93x | 1.26x |
| G2 multiply | 323.729 | 205.094 | 183.117 / 183.198 | 1.77x | 1.12x |
| map G2 | 385.820 | 236.255 | 160.493 / 160.509 | 2.40x | 1.47x |
| G2 MSM 128 | 17968.526 | 11458.968 | 9169.428 / 9166.863 | 1.96x | 1.25x |

## Expanded kernels

| Operation | Stock .NET 11 | Patched | blst (stock / patched) | Stock / blst | Patched / blst |
|---|---:|---:|---:|---:|---:|
| CT Fp inverse | 5.480 | 4.832 | 2.858 / 2.859 | 1.92x | 1.69x |
| Fp inverse (variable, same inputs) | 3.528 | 3.064 | 2.858 / 2.859 | 1.23x | 1.07x |
| CT G1 multiply | 218.162 | 138.628 | 89.232 / 89.272 | 2.44x | 1.55x |
| G1 multiply (variable, same inputs) | 160.646 | 99.336 | 89.232 / 89.272 | 1.80x | 1.11x |
| CT G2 multiply | 452.500 | 290.865 | 183.454 / 183.533 | 2.47x | 1.58x |
| G2 multiply (variable, same inputs) | 283.489 | 173.961 | 183.454 / 183.533 | 1.55x | 0.95x |
| CT map G1 | 98.945 | 58.863 | 44.886 / 44.887 | 2.20x | 1.31x |
| map G1 (variable, same inputs) | 98.262 | 58.376 | 44.886 / 44.887 | 2.19x | 1.30x |
| CT map G2 | 359.311 | 213.885 | 160.895 / 160.837 | 2.23x | 1.33x |
| map G2 (variable, same inputs) | 357.215 | 212.174 | 160.895 / 160.837 | 2.22x | 1.32x |
| Fr mul+add | 0.047 | 0.029 | 0.024 / 0.024 | 1.96x | 1.22x |
| Fp mul+add | 0.096 | 0.059 | 0.045 / 0.045 | 2.13x | 1.31x |
| Fp multiply | 0.080 | 0.050 | 0.039 / 0.039 | 2.03x | 1.29x |
| Fp square | 0.076 | 0.042 | 0.034 / 0.034 | 2.20x | 1.23x |
| Fp2 multiply | 0.250 | 0.153 | 0.113 / 0.113 | 2.21x | 1.35x |
| Fp2 square | 0.178 | 0.109 | 0.085 / 0.085 | 2.10x | 1.29x |
| Fp inverse (kernel) | 3.376 | 2.946 | 2.856 / 2.856 | 1.18x | 1.03x |
| Fp inverse (tower) | 3.382 | 2.953 | 2.861 / 2.862 | 1.18x | 1.03x |
| Fp inverse Fermat | 47.004 | 31.397 | — / — | — | — |
| Fp12 multiply | 4.899 | 2.992 | 2.157 / 2.157 | 2.27x | 1.39x |
| Pairing check 2 | 1911.954 | 1167.972 | 853.846 / 853.434 | 2.24x | 1.37x |
| G1 multiply | 162.148 | 100.259 | 89.107 / 89.115 | 1.82x | 1.13x |
| map G1 | 98.043 | 58.226 | 44.770 / 44.777 | 2.19x | 1.30x |
| G1 MSM 128 | 6265.223 | 4034.174 | 3410.140 / 3410.067 | 1.84x | 1.18x |
| G2 multiply | 284.584 | 174.549 | 183.373 / 182.941 | 1.55x | 0.95x |
| map G2 | 355.490 | 211.078 | 160.556 / 160.484 | 2.21x | 1.32x |
| G2 MSM 128 | 11615.947 | 7326.774 | 9167.915 / 9169.268 | 1.27x | 0.80x |

