# Performance

Current component captures use Windows x64 on a Ryzen 9 9950X3D VM, tiering disabled, logical processor 3, expanded kernels and the [combined carry/select JIT patch](../jit/README.md). The host identifies itself as .NET 12.0.0-dev; the executable targets net11.0. Native is pinned ADX/BMI2 blst `f62244ef50ad1a603decdb8f215e982d2a467bb6`.

## Fixed binary-GCD inverses

Three sequential `--quick` runs use the same filter, inputs and native calls. Cells below are medians of the three reported best-round values, in µs. Native results are from the same runs as their managed row. This is a component comparison, not a cross-platform guarantee.

| Operation | Managed µs | blst µs | Managed / blst |
|---|---:|---:|---:|
| CT Fp inverse | 1.701 | 1.350 | 1.26x |
| Fp inverse (variable) | 1.392 | 1.350 | 1.03x |
| CT Fp2 inverse | 1.799 | 1.428 | 1.26x |
| CT Fr inverse | 0.968 | 0.779 | 1.24x |
| CT G1 multiply | 53.197 | 47.521 | 1.12x |
| CT G2 multiply | 118.221 | 95.822 | 1.23x |
| CT G1 to-affine | 1.792 | 1.430 | 1.25x |
| CT G2 to-affine | 2.079 | 1.646 | 1.26x |
| CT map G1 | 23.199 | 23.257 | 1.00x |
| CT map G2 | 82.734 | 82.861 | 1.00x |
| Pairing check 2 | 435.420 | 456.050 | 0.95x |
| G1 multiply (variable) | 37.100 | 45.800 | 0.81x |
| map G1 (variable) | 22.400 | 22.800 | 0.98x |
| G2 multiply (variable) | 66.900 | 94.700 | 0.71x |
| map G2 (variable) | 79.800 | 80.900 | 0.99x |

Both implementations use 31-bit Pornin batches: Fp has 23 batches and a 55-step exact tail; Fr has 15 batches and a 47-step tail. Managed follows blst's public width schedule, including its first two scalar cofactor batches, narrow u and early v, and late three-limb Fp GCD updates. Signed products become conditional negation plus complete unsigned multiply-accumulate rows. The patched JIT recognizes shared masked swaps and low-bit selects, emitting conditional moves while stock runtimes retain branch-free mask arithmetic. The standalone 31-step inner loop has 28 instructions per step versus blst's 22. Inlining removes calls and matrix out-parameter stores; isolated product kernels preserve MULX/ADCX/ADOX recognition.

The remaining gap includes register moves, approximation and helper boundaries, and generic full-range final correction. Product helpers deliberately stay separate: blanket inlining exhausted the JIT budget and left leaf calls instead of carry rows. Production inversions allocate zero warmed bytes and preserve zero, aliases and canonical Montgomery output. Existing variable-time inversion and public point/map implementations retain their semantics; their rows and pairing provide no-regression controls. Small percentage changes in those controls are within the quick harness's noise.

Full-suite wall time on the patched host with expanded kernels and native blst was 13.69 s before and 13.63 s after; the final suite contains 7,595,569 checks, including the extended width model.

## Reproduction

Build both executable targets sequentially with the SDK pinned in [global.json](../global.json):

```powershell
./scripts/build-native-blst.ps1
dotnet build Nethermind.Crypto.BlsNet.slnx -c Release
dotnet build Nethermind.Crypto.BlsNet.slnx -c Release -p:BlsNetTestTargetFramework=net10.0
$env:DOTNET_ROOT = '<testhost>'
$env:DOTNET_ROLL_FORWARD = 'Major'
$env:DOTNET_TieredCompilation = '0'
$env:BLST_BENCH_PROCESSOR = '3'
& "$env:DOTNET_ROOT/dotnet.exe" '<benchmark DLL>' --quick --expanded-kernels `
  --filter 'CT |Fp inverse|G1 multiply|G2 multiply|map G|Pairing check 2'
```

Build/install the runtime using [jit/README.md](../jit/README.md). A stock .NET 10 launcher needs the net10.0 executable. Record host, target, policy, processor and native revision. Repeat the benchmark three times and take row-wise medians; do not run builds, tests or timing experiments concurrently.

The quick harness warms its loops and reports best-round samples, not means or confidence intervals. Secret and variable-time comparisons use identical inputs and loop shapes. Native calls include ABI overhead; internal native calls may cost less. Shared VM clocks/cache contention makes a few percent weak evidence. See [validation](constant-time-validation.md) for the full host/policy correctness matrix, code review and separate 20,000-sample timing experiment. Statistical non-detection does not prove constant time.

## ARM64 portability regression capture

Captured 2026-10-19 on the same x64 VM, stock .NET 10.0.12 / .NET 11 RC1 and the recorded patched host. These changes add platform infrastructure and correct test interop; production arithmetic is unchanged. There is no ARM64 speed claim or measured arithmetic optimization.

Before is a clean archive of the parent revision; after is this checkout, built with the same SDK. Each row is the median of three alternating before/after `--quick` runs, tiering disabled, processor 3 pinned, native enabled, and the appropriate net10.0/net11.0 executable. Root and Major roll-forward are explicit for every host. Initial unpaired captures had substantial scheduler/native noise; the paired results below replace them. Values are **before / after**; this small sample does not establish statistical significance.

| Host / policy | Fp multiply ns | Fp square ns | Fp2 multiply ns | Fp2 square ns |
|---|---:|---:|---:|---:|
| .NET 10 compact | 55.05 / 52.30 | 53.38 / 54.51 | 194.47 / 196.09 | 128.99 / 121.90 |
| .NET 10 expanded | 52.55 / 52.53 | 48.67 / 48.97 | 266.67 / 265.60 | 209.40 / 199.50 |
| .NET 11 compact | 51.47 / 51.14 | 52.78 / 53.28 | 194.95 / 193.39 | 120.50 / 119.87 |
| .NET 11 expanded | 52.05 / 51.56 | 45.12 / 45.94 | 152.50 / 152.46 | 115.34 / 114.39 |
| Patched compact | 19.21 / 19.29 | 19.03 / 19.29 | 68.84 / 68.85 | 44.38 / 44.83 |
| Patched expanded | 19.56 / 19.52 | 17.77 / 17.84 | 62.64 / 61.75 | 42.34 / 43.02 |

| Host / policy | CT Fp inverse µs | G1 MSM 128 ms | G2 MSM 128 ms |
|---|---:|---:|---:|
| .NET 10 compact | 3.78 / 3.71 | 7.104 / 7.220 | 12.790 / 12.765 |
| .NET 10 expanded | 3.98 / 3.70 | 7.087 / 7.266 | 11.845 / 11.589 |
| .NET 11 compact | 3.11 / 3.10 | 4.227 / 4.165 | 11.284 / 11.269 |
| .NET 11 expanded | 3.07 / 3.14 | 3.933 / 3.923 | 7.158 / 6.956 |
| Patched compact | 1.72 / 1.75 | 1.611 / 1.613 | 4.123 / 4.153 |
| Patched expanded | 1.75 / 1.75 | 1.559 / 1.595 | 2.971 / 3.006 |

Use `--filter 'Fp multiply|Fp square|Fp2 multiply|Fp2 square|CT Fp inverse|G1 MSM 128|G2 MSM 128'` with the reproduction settings above. Emulated timings are excluded.

## ARM64 hardware

Captured 2026-10-19 by CI on GitHub-hosted `ubuntu-24.04-arm` and `macos-15` (Apple silicon) runners: stock .NET 10.0.13 and .NET 11 RC2, `--quick`, tiering disabled, one run per cell on shared VMs, so treat a few percent as noise. blst is the same pinned commit built with its ARM64 assembly. Each cell is managed time, with its ratio to blst; blst's own time is in the last column.

| Linux ARM64 | .NET 10 compact | .NET 10 expanded | .NET 11 expanded | blst |
|---|---:|---:|---:|---:|
| Fp multiply (ns) | 79.7 (2.04x) | 79.7 (2.04x) | 79.7 (2.03x) | 39.1 |
| Fp2 multiply (ns) | 296.5 (2.62x) | 249.3 (2.21x) | 249.4 (2.21x) | 113.0 |
| G1 multiply (µs) | 230.1 (2.58x) | 186.4 (2.09x) | 162.5 (1.82x) | 89.0 |
| G2 multiply (µs) | 323.7 (1.77x) | 284.8 (1.56x) | 284.9 (1.55x) | 183.2 |
| G1 MSM 128 (ms) | 9.16 (2.69x) | 7.87 (2.31x) | 6.27 (1.84x) | 3.41 |
| G2 MSM 128 (ms) | 17.98 (1.96x) | 11.64 (1.27x) | 11.64 (1.27x) | 9.17 |
| Map to G2 (µs) | 385.6 (2.40x) | 355.7 (2.22x) | 355.6 (2.22x) | 160.5 |
| Pairing check 2 (ms) | 2.02 (2.36x) | 1.91 (2.24x) | 1.91 (2.24x) | 0.85 |

| macOS ARM64 | .NET 10 compact | .NET 10 expanded | .NET 11 expanded | blst |
|---|---:|---:|---:|---:|
| Fp multiply (ns) | 80.8 (2.77x) | 80.2 (2.76x) | 79.0 (2.57x) | 29.1 |
| Fp2 multiply (ns) | 300.1 (3.32x) | 275.8 (2.98x) | 245.2 (2.67x) | 90.5 |
| G1 multiply (µs) | 210.6 (2.66x) | 175.8 (2.22x) | 157.9 (2.00x) | 79.2 |
| G2 multiply (µs) | 300.3 (2.02x) | 269.6 (1.82x) | 269.8 (1.82x) | 148.5 |
| G1 MSM 128 (ms) | 8.35 (3.05x) | 7.23 (2.63x) | 6.04 (2.17x) | 2.74 |
| G2 MSM 128 (ms) | 17.83 (2.41x) | 11.15 (1.51x) | 11.16 (1.47x) | 7.40 |
| Map to G2 (µs) | 364.2 (2.76x) | 342.8 (2.59x) | 342.9 (2.60x) | 132.1 |
| Pairing check 2 (ms) | 1.97 (2.81x) | 1.74 (2.45x) | 1.74 (2.49x) | 0.70 |

Expanded beats compact on ARM64 even on .NET 10, unlike x64, so ARM64 defaults to expanded on every runtime. Stock ARM64 JITs still materialize carries with `cmp`/`cset` (see [code-generation findings](constant-time-validation.md#hot-kernel-findings-and-policy)), which leaves managed code about 1.3–3x slower than blst's ARM64 assembly. That is the same kind of gap the carry-aware JIT closes on x64. CI uploads the full output of every run as artifacts.

### Carry-aware runtime baseline

[Run 39304746175](https://github.com/flcl42/bls-net/actions/runs/39304746175) measured runtime `74ed6106` on real Linux ARM64 with three alternating repetitions per host/policy, one pinned core, tiering and ReadyToRun disabled, and native blst in every process. Expanded-policy medians below are in **µs**. The library was `48bf683b`; these are the baseline before the ARM64 changes in [arm64.patch](../jit/arm64.patch).

| Operation | Stock .NET 11 | Patched before | blst |
|---|---:|---:|---:|
| Fp multiply | 0.080 | 0.050 | 0.039 |
| Fp square | 0.076 | 0.042 | 0.034 |
| Fp2 multiply | 0.250 | 0.153 | 0.113 |
| Fp2 square | 0.178 | 0.109 | 0.085 |
| Fr mul+add | 0.047 | 0.029 | 0.024 |
| Fp mul+add | 0.096 | 0.059 | 0.045 |
| CT Fp inverse | 5.480 | 4.832 | 2.859 |
| Fp inverse, same inputs | 3.528 | 3.064 | 2.859 |
| Pairing check 2 | 1911.954 | 1167.972 | 853.434 |
| G1 multiply | 162.148 | 100.259 | 89.115 |
| G2 multiply | 284.584 | 174.549 | 182.941 |
| G1 MSM 128 | 6265.223 | 4034.174 | 3410.067 |
| G2 MSM 128 | 11615.947 | 7326.774 | 9169.268 |
| map G1 | 98.043 | 58.226 | 44.777 |
| map G2 | 355.490 | 211.078 | 160.484 |

The cold runtime/testhost plus both JITs took 998 seconds. All library suites and all twenty Release/Checked stress/tiering configurations passed. Native timings remain specific to each capture; the table uses the patched process's native medians.

### First ARM64 candidate on hardware

[Run 39314252575](https://github.com/flcl42/bls-net/actions/runs/39314252575) measures library `2441b94` with runtime `8a918efe`, three alternating captures per host/policy on a pinned Neoverse-N2 core. All 7,936,156 native-comparison checks pass under both policies; all 23 JIT entry points pass in all twenty Release/Checked stress/tiering configurations. The table covers regular operations using expanded kernels; all values are **µs**. The earlier patched column uses the original library and runtime `74ed6106`. Stock uses the candidate library and .NET 11 RC2. Its ARM64 carry-row formulations are intended for the patched JIT and can cost more on stock ARM64.

| Operation | Stock | Patched before | First candidate | blst |
|---|---:|---:|---:|---:|
| Fp multiply | 0.112 | 0.050 | 0.036 | 0.039 |
| Fp square | 0.093 | 0.042 | 0.053 | 0.034 |
| Fp2 multiply | 0.336 | 0.153 | 0.115 | 0.113 |
| Fp2 square | 0.243 | 0.109 | 0.078 | 0.085 |
| Fr mul+add | 0.047 | 0.029 | 0.024 | 0.024 |
| Fp mul+add | 0.130 | 0.059 | 0.041 | 0.045 |
| Fp inverse, kernel | 3.343 | 2.946 | 2.799 | 2.855 |
| Fp inverse, same inputs | 3.493 | 3.064 | 2.909 | 2.858 |
| Fp12 multiply | 6.033 | 2.992 | 2.136 | 2.157 |
| Pairing check 2 | 2381.059 | 1167.972 | 824.631 | 853.741 |
| G1 multiply | 196.814 | 100.259 | 69.610 | 89.027 |
| G2 multiply | 369.900 | 174.549 | 127.621 | 183.174 |
| G1 MSM 128 | 8096.782 | 4034.174 | 2883.832 | 3409.505 |
| G2 MSM 128 | 14994.903 | 7326.774 | 5566.585 | 9168.354 |
| map G1 | 116.700 | 58.226 | 43.874 | 44.777 |
| map G2 | 446.087 | 211.078 | 156.134 | 160.470 |

This is an intermediate measurement, not a parity claim. Expanded Fp square's loop caller materializes carries despite the efficient isolated arithmetic listing. Fp2 multiply, Fr mul+add and the same-input variable inverse also need further measurements or improvements. The unfiltered quick sweep measures Fp12 square once at 1.548 µs versus blst's 1.544 µs; it needs repeated confirmation. Regular operations are the speed targets; constant-time paths remain correctness and generated-code security requirements. The paired constant-time inversion experiment is shelved locally and is not part of the branch.

### Isolated square and carry rows on hardware

[Run 39319334072](https://github.com/flcl42/bls-net/actions/runs/39319334072) measures library `ec20417`, runtime `8a918efe`, three alternating repetitions per host/policy on Linux ARM64. Both policies pass 8,047,464 checks and all 23 JIT entry points pass in all twenty configurations. Values are **µs**, with native medians from the patched process. These runtime revisions precede the subsequent correctness-review fixes described in [jit/README.md](../jit/README.md); they are measurement checkpoints, not the final runtime recommendation.

| Operation | Stock | Patched before | Patched ec20417 | blst | Patched/blst |
|---|---:|---:|---:|---:|---:|
| Fp multiply | 0.112 | 0.050 | 0.036 | 0.039 | 0.91x |
| Fp square | 0.094 | 0.042 | 0.034 | 0.034 | 0.99x |
| Fp2 multiply | 0.336 | 0.153 | 0.113 | 0.113 | 1.00x |
| Fp2 square | 0.244 | 0.109 | 0.078 | 0.085 | 0.92x |
| Fr mul+add | 0.058 | 0.029 | 0.021 | 0.024 | 0.88x |
| Fp mul+add | 0.129 | 0.059 | 0.041 | 0.045 | 0.92x |
| Fp inverse, same inputs | 3.493 | 3.064 | 2.904 | 2.857 | 1.02x |
| Fp inverse, kernel | 3.343 | 2.946 | 2.799 | 2.855 | 0.98x |
| Fp12 multiply | 6.034 | 2.992 | 2.139 | 2.156 | 0.99x |
| Pairing check 2 | 2380.005 | 1167.972 | 821.095 | 853.603 | 0.96x |
| G1 multiply | 196.989 | 100.259 | 69.404 | 89.043 | 0.78x |
| G2 multiply | 369.356 | 174.549 | 126.537 | 183.158 | 0.69x |
| G1 MSM 128 | 8083.327 | 4034.174 | 2864.050 | 3409.867 | 0.84x |
| G2 MSM 128 | 14965.645 | 7326.774 | 5498.616 | 9167.597 | 0.60x |
| map G1 | 116.794 | 58.226 | 43.918 | 44.798 | 0.98x |
| map G2 | 445.423 | 211.078 | 155.095 | 160.471 | 0.97x |

Isolating ARM64 square removes the caller's carry materialization. Four-limb Fr carry rows improve the mul+add loop. Loading Fp subtraction's modulus and reusing it for signed small-wide correction reduces subtraction to 43 instructions and difference-plus-REDC to 244. The square leaf has 302 instructions; the Fr loop entry has 204. All 171 common x64 library instruction listings remain identical to the original. Fp square, Fp2 multiply, the variable inverse, Fp12 multiply/square, maps and pairing still need stronger margins and repeated confirmation; a ratio near one is not a clear win.

The next candidate reuses the existing signed 31-step update helper in the variable inverse, loads the full signed-wide correction constants, and fuses the imaginary Fp2 product subtraction with REDC. Its optional separate native reference library wraps the pinned assembly's raw product, REDC and subtract-plus-REDC without changing the public blst controls. Local differential checks cover Windows x64 and emulated ARM64; native Linux and Apple silicon measurements are pending. Constant-time inversion remains outside the speed target and its paired experiment remains shelved.

At this checkpoint, stock .NET 10/11 and patched x64 pass 8,137,792 checks under each policy. Fresh x64 captures with runtime `c318f5ac` match all 171 common audit methods and all 147 common regular benchmark/library methods against the original library/runtime; relocation addresses and process stack cookies are normalized, arithmetic constants are retained. The regular capture includes variable inversion, full wide reduction and Fp12 multiply/square. Three alternating x64 quick captures show Fp12 multiply/square and pairing medians within 1.1%; individual kernel-loop samples fluctuate substantially, including an Fp2-square median 12% higher despite identical library instructions. These noisy harness samples do not establish a timing regression or a speed claim. The exact instruction comparisons provide the x64 preservation evidence. `RUN_MACOS=true` is set for native Apple silicon validation, and Fp12 square/raw-product/reduction names are appended to the repeated filter. The native-wrapper workflow build hook awaits authorization under the pipeline ownership rule.

### Fused Fp2 checkpoint on Linux and Apple silicon

[Run 39365488563](https://github.com/flcl42/bls-net/actions/runs/39365488563) measures library `7f94ab9` and runtime `c318f5ac` with three alternating repetitions per host/policy. Linux uses a pinned core; the Apple M1 VM is unpinned and noisier. Both platforms pass 8,107,680 checks under each policy and all 25 JIT fixtures in every one of twenty Release/Checked stress/tiering configurations. The five-platform stock-runtime CI also passes. This runtime predates the further V3/V4/V5/V7 correctness fixes; these are historical measurements, and the current patch is required for general correctness.

Times are **µs**. Each triplet is **stock .NET 11 / patched / blst**, with the native median from the patched process.

| Operation | Linux stock / patched / blst | Patched/blst | Apple stock / patched / blst | Patched/blst |
|---|---:|---:|---:|---:|
| Fp multiply | 0.112 / 0.036 / 0.039 | 0.91x | 0.102 / 0.027 / 0.031 | 0.86x |
| Fp square | 0.094 / 0.034 / 0.034 | 0.99x | 0.087 / 0.028 / 0.027 | 1.04x |
| Fp2 multiply | 0.334 / 0.108 / 0.113 | 0.96x | 0.322 / 0.080 / 0.094 | 0.86x |
| Fp2 square | 0.243 / 0.078 / 0.085 | 0.92x | 0.223 / 0.060 / 0.063 | 0.96x |
| Fr mul+add | 0.059 / 0.021 / 0.024 | 0.88x | 0.061 / 0.021 / 0.024 | 0.89x |
| Fp mul+add | 0.129 / 0.041 / 0.045 | 0.92x | 0.129 / 0.037 / 0.048 | 0.77x |
| Fp inverse (variable, same inputs) | 3.480 / 2.827 / 2.857 | 0.99x | 2.572 / 1.904 / 2.948 | 0.65x |
| Fp12 multiply | 6.030 / 2.145 / 2.157 | 0.99x | 5.186 / 1.670 / 1.819 | 0.92x |
| Fp12 square | 4.413 / 1.532 / 1.543 | 0.99x | 3.825 / 1.218 / 1.235 | 0.99x |
| Pairing check 2 | 2377.722 / 818.457 / 853.399 | 0.96x | 2103.100 / 645.875 / 741.454 | 0.87x |
| G1 multiply | 197.028 / 69.292 / 89.114 | 0.78x | 180.750 / 54.875 / 79.125 | 0.69x |
| G2 multiply | 368.145 / 123.872 / 183.341 | 0.68x | 328.042 / 93.291 / 147.583 | 0.63x |
| G1 MSM 128 | 8084.695 / 2865.264 / 3410.600 | 0.84x | 7451.375 / 2346.791 / 2761.000 | 0.85x |
| G2 MSM 128 | 14890.741 / 5339.568 / 9167.165 | 0.58x | 13429.375 / 4159.667 / 7422.500 | 0.56x |
| map G1 | 116.808 / 43.926 / 44.781 | 0.98x | 112.041 / 36.625 / 39.291 | 0.93x |
| map G2 | 444.484 / 153.006 / 160.550 | 0.95x | 405.292 / 121.500 / 132.125 | 0.92x |

Fp2 multiplication improves from parity to 0.96x blst on Linux. The same-input variable inverse, Fp square and Fp12 multiply/square remain too close to parity for a clear-win claim. Apple silicon confirms most gains, but its Fp square is 1.04x blst. Raw product and reduction samples are now repeated; native columns remain unavailable until the optional reference-library build hook is authorized. The original public blst controls remain unchanged.

The next candidate uses whole cross-product carry rows in square, keeps the wide Karatsuba sum in registers, uses disjoint raw products in the tower, and unrolls the regular inverse's modular coefficient update. Local source tests pass 8,348,548 checks under both policies on stock .NET 10/11 and patched x64. Fresh captures retain identical instructions for all 171 common x64 audit methods and all 147 common regular-operation methods. The paired constant-time inversion experiment remains shelved. Native hardware results for this next candidate and the latest review fixes are pending.

### Reviewed-runtime square and inverse checkpoint

[Run 39373909410](https://github.com/flcl42/bls-net/actions/runs/39373909410) measures library `b6d4827` and runtime `00403488`, with three alternating repetitions on Linux Neoverse N2 and Apple silicon. Both platforms pass **8,318,436 native checks per policy**, **8,007,920 managed-only checks per policy**, and **28 JIT fixtures in all twenty configurations**. The five-platform stock-runtime CI passes. The independent reviewer reports no open wrong-code findings at this runtime revision and independently passes 4.32 million generated ARM64 calls under emulation.

Times are **µs**, and each triplet is **stock / patched / blst**. The native median comes from the patched process. These are expanded-policy measurements, which is the default on ARM64.

| Operation | Linux stock / patched / blst | Patched/blst | Apple stock / patched / blst | Patched/blst |
|---|---:|---:|---:|---:|
| Fp multiply | 0.112 / 0.036 / 0.039 | 0.91x | 0.114 / 0.028 / 0.033 | 0.85x |
| Fp square | 0.100 / 0.033 / 0.034 | 0.96x | 0.097 / 0.027 / 0.028 | 0.98x |
| Fp2 multiply | 0.334 / 0.108 / 0.113 | 0.96x | 0.303 / 0.087 / 0.094 | 0.93x |
| Fp2 square | 0.244 / 0.078 / 0.085 | 0.92x | 0.238 / 0.062 / 0.073 | 0.85x |
| Fr mul+add | 0.059 / 0.021 / 0.024 | 0.88x | 0.063 / 0.021 / 0.024 | 0.86x |
| Fp mul+add | 0.130 / 0.041 / 0.045 | 0.92x | 0.125 / 0.037 / 0.048 | 0.78x |
| Fp inverse (variable, same inputs) | 3.300 / 2.639 / 2.856 | 0.92x | 2.539 / 1.760 / 2.969 | 0.59x |
| Fp12 multiply | 6.005 / 2.100 / 2.157 | 0.97x | 5.306 / 1.598 / 1.780 | 0.90x |
| Fp12 square | 4.395 / 1.495 / 1.544 | 0.97x | 3.823 / 1.171 / 1.240 | 0.94x |
| Pairing check 2 | 2370.969 / 807.887 / 853.662 | 0.95x | 2289.162 / 653.263 / 714.237 | 0.91x |
| G1 multiply | 201.272 / 68.426 / 89.323 | 0.77x | 184.791 / 54.250 / 79.167 | 0.69x |
| G2 multiply | 367.846 / 123.396 / 183.175 | 0.67x | 334.583 / 94.667 / 150.417 | 0.63x |
| G1 MSM 128 | 8158.411 / 2853.294 / 3409.203 | 0.84x | 7462.084 / 2396.000 / 2760.416 | 0.87x |
| G2 MSM 128 | 14883.574 / 5328.110 / 9166.586 | 0.58x | 14093.084 / 4252.041 / 7469.958 | 0.57x |
| map G1 | 118.421 / 43.633 / 44.762 | 0.97x | 112.667 / 36.417 / 39.375 | 0.92x |
| map G2 | 444.497 / 152.870 / 160.542 | 0.95x | 419.416 / 121.791 / 132.583 | 0.92x |

Variable inversion now has a clear Linux margin at 0.92x blst, and square improves to 0.96x. Fp12 multiply/square reach 0.97x; pairing reaches 0.95x. Linux map G1 and Fp12 margins remain narrow and need independent repetitions. Apple square is 0.98x, with more timing variation on its unpinned VM. Explicit compact-policy overrides remain slower for some operations (Linux Fp2 multiply 1.11x, Fp12 multiply/square 1.04x/1.05x, map G2 1.02x); they are correctness-tested, and these measurements do not claim compact-policy parity. Raw-product and reduction native comparisons still await the optional reference build hook.

The next map candidate shares the improved cross-product square core, retains lazy outputs below 2p, and isolates its arithmetic leaf from the public-count loop. Its local listing takes 285 instructions per square iteration including calls/loop, versus 358 previously (353 on the final old iteration). The new leaf has 276 branch-free instructions; canonical square retains 292. Emulated extracted ARM64 code passes 8,036 exact-output checks, including inputs approaching 2p, overlapping output and both incoming carry states. A second candidate keeps Fp4 squares wide until four final reductions instead of six CIOS reductions; its private biased-real formula has the proven bound |x|<8p²<pR. It targets cyclotomic square (986.84ns versus blst 940.31ns in the previous quick sweep) and final exponentiation. Both candidates pass 8,353,308 checks per policy on patched x64 and stock .NET 10/11. Captures retain identical instructions for 171 common x64 audit methods and 154 common regular methods, including cyclotomic square and final exponentiation. The local ARM64 inventory covers 160 reachable methods, with no variable-latency candidates; both new arithmetic leaves are branch-free, and square-run branches depend only on the public count. The expanded repeated filter now also covers the remaining regular tower/pairing/curve operations. Hardware timing is pending.

### Fused coordinates and signed reductions

[Run 39394780548](https://github.com/flcl42/bls-net/actions/runs/39394780548) measures library `4249de6` and runtime `a0c28efc`: all 31 JIT fixtures pass in all **24 Release/Checked configurations**, now including JitStress=1, on Linux ARM64 and Apple silicon. Both policies pass 8,388,412 native checks and 8,072,896 managed-only checks per platform. Five-platform stock .NET 10/11 CI also passes. With three alternating captures per host/policy, fused 3t±2a coordinate updates move Linux cyclotomic square from **950 to 936 ns** versus blst **943 ns** (0.993x), and Apple silicon from **749 to 724 ns** versus **752 ns** (0.962x). Linux pairing check 2 moves from 799.735 to 795.714 µs versus 852.936 µs; Apple measures 625.317 versus 695.842 µs. Other Linux field medians remain effectively unchanged. Apple raw times vary more, so compare same-process native ratios. The Linux cyclotomic lead remains too small to claim a clear margin from this run alone.

The next coordinate checkpoint pins runtime `5f087d70`, which strengthens the containment regression without changing generated runtime code. Its subtract update uses the tighter (−2p,3p) bound to reuse 2p limbs, reducing the Fp2 helper from 221 to 191 local ARM64 instructions. Finishing the independent first coordinate pair earlier reduces the cyclotomic wrapper from 103 to 99 instructions; output/input aliases still pass. Local suites pass 8,418,524 checks per policy on patched x64 and stock .NET 10/11. Fresh x64 captures retain all 171 common library instruction listings and both mul+add loops (243/242 instructions). Eight alternating captures give Fr mul+add 11.100→10.995 ns and Fp mul+add 19.910→19.915 ns. The ARM64 audit inventories 162 methods with no variable-latency candidates. Native raw-product/reduction leaf imports now suppress the GC transition; the next pipeline builds those optional exports from the pinned blst archive and captures the constant-time witness on the native ARM64 CLR. [Run 39398839856](https://github.com/flcl42/bls-net/actions/runs/39398839856), library `6369b5e`, passes all 31 fixtures in all 24 configurations and both full library policies on each platform. Linux cyclotomic square reaches **927.97 ns versus 942.50 ns** (0.985x), final exponentiation 339.554 versus 379.605 µs (0.895x), and pairing check 2 792.866 versus 853.933 µs (0.929x). Apple silicon measures cyclotomic square **716.08 versus 751.67 ns** (0.953x) and pairing check 2 617.650 versus 688.133 µs (0.898x). Native constant-time inventories cover 171 methods per platform, with 13 unresolved/runtime callees and no variable-latency candidates. Both policies pass 8,418,524 native checks and 8,072,896 managed-only checks per platform; five-platform stock CI is green.

The native reference hook exposes remaining signed-reducer gaps on Linux: general ReduceWide is 29.29 ns versus a 23.38 ns positive REDC, and ReduceWideSmall is 24.12 versus 23.39 ns. Positive reduction is 23.08 versus 23.38 ns; difference reduction is 26.07 versus 30.89 ns. These wrappers compare identical measured inputs, while the managed signed APIs retain broader input contracts. The next candidate preserves those contracts: the full signed reducer, used only by variable-time Fp6/Fp12 formulas, tests strict quotient high-word bounds before its general correction; the secret small reducer normalizes a negative input by adding pR to its high half before the multiply rows. This moves a carry dependency away from the final quotient without adding a secret-dependent branch. Local suites pass 8,448,686 checks per policy on patched x64 and stock .NET 10/11, including exact high-word equality cases and aliases; all 171 common x64 listings remain unchanged. The new small leaf has 216 branch-free local ARM64 instructions. [Run 39404089742](https://github.com/flcl42/bls-net/actions/runs/39404089742), library `3b89478`, confirms the variable general-reduction shortcut: Linux moves from 29.29 to **22.35 ns versus 23.40 ns** (0.955x), and Apple silicon from 22.21 to **17.13 versus 19.39 ns** (0.883x). Linux Fp12 multiply reaches 2.011 versus 2.161 microseconds (0.931x), and pairing check 2 reaches 786.905 versus 853.840 microseconds (0.922x). However, putting small-input normalization before every row regresses Linux ReduceWideSmall from **24.12 to 24.87 ns**, versus blst 23.39 ns; that schedule is superseded. Apple small reduction remains ahead at 19.23 versus 21.22 ns. Apple square varies to 26.91 versus 26.58 ns in this run despite the same 291-instruction leaf as the earlier faster capture, so its margin needs an unchanged-source repeat. Both platforms pass all 31 fixtures in all 24 configurations, 8,448,686 native checks per policy and the managed-only suites. Native constant-time inventories cover 172 methods with no variable-latency candidates, and five-platform stock CI is green.

The next small-reducer schedule moves normalization between the third and fourth unrolled rows: local codegen confirms 36 of 72 widening-product instructions now precede it, rather than zero. The leaf remains 216 instructions and branch-free. All six local suites pass 8,448,686 checks and all 171 common x64 listings remain identical. This changes scheduling only; the signed bound, pR normalization and final subtraction are unchanged. Hardware timing of this schedule is pending.

### ARM64 generated code

Local Windows-ABI ARM64 AltJit captures compare the original library/runtime with the ARM64 kernels and runtime `00403488`. Counts include fixed loop repetitions, managed/native wrappers and symbolically resolved arithmetic callees. blst counts use its generated ELF ARMv8 assembly, including bare returns; ABI and scheduling differences remain. The dedicated inverse row counts one loop step, not a complete batch or inversion.

| Kernel | Original managed, runtime 74ed6106 | ARM64 candidate | blst ARM64 |
|---|---:|---:|---:|
| Fp multiply | 567 | 356 | 370 |
| Fp square | 458 | 292 | 313 |
| Fp raw product | 217 | 165 | 180 |
| Fp raw product, preserving aliases | 223 | 166 | 180 |
| Fp positive wide reduction | 341 | 200 | 204 |
| Fp2 multiply | 1664 | 1085 | 1095 |
| Fp2 square | 1178 | 810 | 801 |
| Fp add | 90 | 43 | 45 |
| Fp subtract | 93 | 43 | 44 |
| Inverse inner step | 34 | 18 | 27 |

Fp multiply's isolated arithmetic leaf is 347 branch-free instructions; its managed entry adds nine. The raw product and wide reducer are also branch-free. Fp2 multiplication totals 93 wrapper instructions, three 165-instruction products, 244 for the real difference/reduction and 253 for the fused imaginary reduction. The inverse uses three CSELs and only its public backedge per step. Managed retains 31-bit batches; blst ARM64 uses 62-bit batches. [JIT reproduction](../jit/README.md#arm64-carry-and-row-follow-up) describes the private-host CoreLib preparation needed for accurate widening-product imports.

The ARM64 audit inventory covers 156 reachable methods and reports no variable-latency candidates. It retains unresolved helpers requiring native ARM64 closure. Reviewed field-kernel branches are absent; inversion branches count fixed batches/steps or clear a public stack-frame range. The capture is generated-code review, not hardware execution.

### x64 regression check for the ARM64 candidate

All 171 common x64 library instruction listings match the original binary with the baseline JIT. Three alternating baseline/candidate expanded-policy `--quick` captures use private patched hosts, Major roll-forward, ReadyToRun/tiering disabled and processor 3. Medians below complement that exact instruction comparison; changes under 0.6% are within the harness's shared-VM noise.

| Operation | Before | After |
|---|---:|---:|
| Fp multiply, ns | 18.620 | 18.560 |
| Fp square, ns | 16.960 | 16.910 |
| Fp2 multiply, ns | 59.110 | 59.160 |
| Fp2 square, ns | 40.360 | 40.180 |
| Fr mul+add, ns | 10.850 | 10.790 |
| Fp mul+add, ns | 19.640 | 19.640 |
| CT Fp inverse, µs | 1.663 | 1.660 |
| Pairing check 2, µs | 423.860 | 424.320 |
| G1 MSM 128, µs | 1513.200 | 1504.900 |
| G2 MSM 128, µs | 2868.600 | 2864.700 |

The initial candidate's full native-comparison suite passes **7,896,012 checks per run** under both policies on stock .NET 10.0.12, stock .NET 11 RC1 and the private patched .NET 12 host. The alias-preserving extension passes **7,936,156 checks** on the patched host, including both input halves overlapping the output. The 23 JIT regressions also pass with the Release JIT and all ten Checked stress/tiering configurations. The two new fixtures compile successfully through the ARM64 Checked AltJit; their executed fallback checks are x64 checks.

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


The EIP-2537 table uses BenchmarkDotNet in-process Short means from the Nethermind integration tree, separately from the component best-round measurements. Its public-input kernels are unchanged. Managed MSM uses GLV/GLS and batch-affine buckets, while blst's single-point multiplication is constant-time; these precompile comparisons do not isolate the JIT's contribution. The integration branch is not published yet.

In that tree, reproduce with `dotnet run --project src/Nethermind/Nethermind.Benchmark.Runner -c Release -- --filter '*Bls12381*' --job Short --inProcess`, using the desired launcher runtime and native benchmarks in each capture. The original carry-aware PR suffices for those retained precompile results.
