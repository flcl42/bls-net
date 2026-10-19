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

The cold runtime/testhost plus both JITs took 998 seconds. All library suites and all twenty Release/Checked stress/tiering configurations passed. Native timings remain specific to each capture; the table uses the patched process's native medians. Candidate hardware measurements follow after pipeline validation.

### ARM64 generated code

Local Windows-ABI ARM64 AltJit captures compare the original library/runtime with the ARM64 kernels and runtime `8a918efe`. Counts include fixed loop repetitions, managed/native wrappers and symbolically resolved arithmetic callees. blst counts use its generated ELF ARMv8 assembly, including bare returns; ABI and scheduling differences remain. The dedicated inverse row counts one loop step, not a complete batch or inversion.

| Kernel | Original managed, runtime 74ed6106 | ARM64 candidate | blst ARM64 |
|---|---:|---:|---:|
| Fp multiply | 567 | 356 | 370 |
| Fp square | 458 | 302 | 313 |
| Fp raw product | 217 | 165 | 180 |
| Fp raw product, preserving aliases | 223 | 166 | 180 |
| Fp positive wide reduction | 341 | 200 | 204 |
| Fp2 multiply | 1664 | 1159 | 1095 |
| Fp2 square | 1178 | 810 | 801 |
| Fp add | 90 | 43 | 45 |
| Fp subtract | 93 | 56 | 44 |
| Inverse inner step | 34 | 18 | 27 |

Fp multiply's isolated arithmetic leaf is 347 branch-free instructions; its managed entry adds nine. The raw product and wide reducer are also branch-free. The inverse uses three CSELs and only its public backedge per step. Managed retains 31-bit batches; blst ARM64 uses 62-bit batches. [JIT reproduction](../jit/README.md#arm64-carry-and-row-follow-up) describes the private-host CoreLib preparation needed for accurate widening-product imports.

The ARM64 audit inventory covers 163 reachable methods and reports no variable-latency candidates. It retains unresolved helpers requiring native ARM64 closure. Reviewed field-kernel branches are absent; inversion branches count fixed batches/steps or clear a public stack-frame range. The capture is generated-code review, not hardware execution.

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
