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

Final measurements on 2026-10-20 use library **582b229b0131965f76cde4f55c228d78f9e858c3** and runtime **5f087d704e227bd3124287f331187fb0240ff8ee**, with no additional runtime patches. Two separate workflows, [run 39423063991](https://github.com/flcl42/bls-net/actions/runs/39423063991) and [run 39426439922](https://github.com/flcl42/bls-net/actions/runs/39426439922), both completed on Linux ARM64 and Apple silicon with `RUN_MACOS=true`. Each workflow takes three alternating patched/stock captures per policy, giving **six captures per platform** for the final medians below. The final documentation commit changes no compiled source or benchmark settings from 582b229.

Linux is GitHub `ubuntu-24.04-arm`, Neoverse-N2, pinned to CPU 0. Apple is `macos-15`, Apple M1 (Virtual), unpinned. ReadyToRun and tiering are disabled. The quick harness reports the best of seven rounds; shared VM clocks, scheduling and native-call overhead remain limitations. Ratios divide the final managed median by the native median from the same patched-runtime processes. Separate workflows allocate independent runners; these results are measurements, not confidence intervals or a hardware-wide guarantee.

Cells are **stock .NET 11 / patched baseline 74ed6106 / patched final / blst**. Stock is .NET **11.0.0-rc.2.26504.105** running the final library. The historical patched baseline is [run 39315754704](https://github.com/flcl42/bls-net/actions/runs/39315754704), library `1857b51af7414a709bbfebeb3a71b884c1eb3ee7`, runtime `74ed610683646896bfd9f22bbbcea93124b44f4a`. It includes the original x64 carry/select follow-ups but predates the ARM64 work. Baseline cells use its three-capture median, except **†** rows available only in its single quick capture. Thus baseline-to-final changes include both library and JIT improvements. The blst column uses the final runs' pinned ARM64 assembly at `f62244ef50ad1a603decdb8f215e982d2a467bb6`.

The table uses the default **expanded** ARM64 policy and regular operations. All 18 listed targets are faster than blst in each workflow's three-capture median on both platforms. Pooled margins are **1.44–41.80% on Linux** and **4.48–44.81% on Apple silicon**. Linux cyclotomic square remains the narrowest margin: 1.35% in the first workflow and 1.61% in the second. Apple timing is noisier: an individual second-run map G1 capture reaches 2.374x while that workflow's median is 0.939x; individual Fp/Fp2 square, Fp2-times-three, Miller-eight and two-sparse-product captures also cross parity. No samples are discarded. The claim concerns repeated medians; a few percent on shared VMs should be read cautiously.

| Operation | Linux | Apple silicon | After/blst Linux / Apple |
|---|---:|---:|---:|
| Fp multiply (ns) | 111.73 / 50.42 / 35.74 / 39.17 | 97.54 / 38.51 / 27.49 / 30.80 | 0.913x / 0.893x |
| Fp square (ns) | 99.76 / 42.26 / 33.00 / 34.48 | 96.53 / 32.75 / 24.62 / 25.97 | 0.957x / 0.948x |
| Fp2 multiply (ns) | 333.50 / 152.54 / 108.05 / 113.02 | 309.36 / 120.45 / 76.17 / 92.42 | 0.956x / 0.824x |
| Fp2 square (ns) | 243.01 / 109.13 / 78.18 / 84.79 | 215.31 / 83.24 / 59.15 / 61.92 | 0.922x / 0.955x |
| Fr mul+add (ns) | 58.62 / 29.33 / 21.31 / 24.07 | 61.83 / 29.17 / 19.22 / 22.95 | 0.885x / 0.838x |
| Fp mul+add (ns) | 129.28 / 58.61 / 41.29 / 44.84 | 121.60 / 52.41 / 36.95 / 47.91 | 0.921x / 0.771x |
| ReduceWide (ns) | 60.41 / 43.50† / 22.36 / 23.39 | 56.48 / 35.95† / 17.35 / 19.09 | 0.956x / 0.909x |
| Fp inverse (variable, same inputs) (µs) | 3.301 / 3.065 / 2.640 / 2.857 | 2.356 / 2.094 / 1.750 / 2.875 | 0.924x / 0.609x |
| Fp12 multiply (µs) | 5.743 / 2.991 / 2.009 / 2.160 | 4.937 / 2.400 / 1.539 / 1.689 | 0.930x / 0.911x |
| Fp12 square (µs) | 4.394 / 2.172† / 1.490 / 1.545 | 3.742 / 1.705† / 1.152 / 1.240 | 0.964x / 0.929x |
| Cyclotomic square (µs) | 2.790 / 1.481† / 0.929 / 0.942 | 2.529 / 1.152† / 0.697 / 0.749 | 0.986x / 0.931x |
| Pairing check 2 (µs) | 2280.631 / 1167.526 / 786.354 / 853.776 | 1938.858 / 914.942 / 612.000 / 682.260 | 0.921x / 0.897x |
| G1 multiply (µs) | 201.249 / 100.313 / 68.494 / 89.250 | 177.605 / 83.167 / 53.291 / 77.479 | 0.767x / 0.688x |
| G2 multiply (µs) | 367.790 / 174.573 / 123.402 / 183.034 | 328.667 / 134.042 / 89.791 / 142.812 | 0.674x / 0.629x |
| G1 MSM 128 (µs) | 8156.539 / 4033.972 / 2855.327 / 3410.559 | 7289.416 / 3185.542 / 2247.958 / 2656.500 | 0.837x / 0.846x |
| G2 MSM 128 (µs) | 14892.946 / 7328.484 / 5335.443 / 9167.572 | 13210.750 / 5905.458 / 4101.938 / 7431.979 | 0.582x / 0.552x |
| map G1 (µs) | 127.874 / 58.173 / 42.942 / 44.783 | 114.875 / 47.625 / 35.125 / 38.688 | 0.959x / 0.908x |
| map G2 (µs) | 463.442 / 211.099 / 151.474 / 160.493 | 418.646 / 165.167 / 117.730 / 130.250 | 0.944x / 0.904x |

Production retains the best measured small signed correction and removes all experimental reduction leaves, comparison-only benchmark rows and audit calls. Useful pinned-native raw-product/REDC references and signed-input/output-alias tests remain. General signed reduction's quotient shortcuts are confined to variable-time Fp6/Fp12 formulas. Secret small, positive and difference reductions retain branch-free arithmetic. The signed APIs have broader contracts than blst's positive REDC; their benchmark wrappers pass identical valid inputs.

The accepted internal small signed reducer remains **24.12/23.39 ns (1.031x) on Linux**, and 18.54/18.64 ns (0.995x) on Apple silicon. Positive REDC measures 23.08/23.39 ns (0.987x) and 18.23/18.74 ns (0.973x), respectively; near-parity fluctuations in this internal helper do not change the regular-operation results. No further reducer schedules are retained or planned in this checkpoint. The optional compact override is correctness-tested but does not have the expanded policy's parity claim. Constant-time operation speed is outside this target.

Both final workflows pass all **31 JIT fixtures in all 24 named configurations** on each platform: Release/Checked × tiering 0/1 × default, JitStress=1/2 and JitStressRegs=1/3/8. This includes 300 fixed-seed generated carry methods, each invocation checking 120,000 input/alias comparisons against independent MinOpts UInt128 references, plus deterministic stale-flags, containment and prolog regressions. Every platform passes **8,478,707 native-reference checks per policy** and **8,133,079 managed-only checks per policy**. The stock five-platform matrix covers Linux x64/ARM64, Windows x64/ARM64 and Apple silicon; the final-commit run is linked from the final report.

Dedicated native constant-time captures inventory **172 compiled methods**, **13 external/runtime callees** and **zero variable-latency candidates** per platform. The six arithmetic leaves below have zero conditional branches in both native captures; public fixed-count loops, initialization/error paths and unresolved runtime callees still require contextual review. This inventory is not a formal constant-time proof. The final local ARM64 AltJit inventory covers 163 methods, 23 unresolved/runtime callees and zero variable-latency candidates. This continuation validated behavior through managed CLR tests on real ARM64 hardware and used AltJit only for inspection; it did not assemble or execute extracted JIT code.

| Native arithmetic leaf | Linux instructions | Apple silicon instructions | Conditional branches |
|---|---:|---:|---:|
| Fp multiply | 347 | 346 | 0 |
| Fp square | 292 | 291 | 0 |
| Disjoint wide product | 165 | 165 | 0 |
| Alias-preserving wide product | 166 | 166 | 0 |
| Positive REDC | 200 | 199 | 0 |
| Small signed REDC | 213 | 212 | 0 |

### ARM64 generated code

Local Windows-ABI AltJit captures compare the original library/runtime with the retained ARM64 kernels. Counts include fixed loop repetitions, wrappers and symbolically resolved arithmetic callees. blst counts use generated ELF ARMv8 assembly including bare returns, so ABI and scheduling differences remain. The inverse row counts one step rather than a complete inversion. Runtime 5f087d70 retains the ARM64 lowering of 00403488; subsequent runtime changes fix base x64 assertions and strengthen regressions.

| Kernel | Original managed, runtime 74ed6106 | Retained ARM64 kernels | blst ARM64 |
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

Fp multiply's leaf has 347 branch-free instructions; its entry adds nine. Fp2 multiplication totals 93 wrapper instructions, three 165-instruction products, 244 for the real difference/reduction and 253 for the fused imaginary reduction. The inverse uses three CSELs with only its public backedge per step; managed retains 31-bit batches while blst ARM64 uses 62-bit batches. Fused cyclotomic updates use 3t±2a and shared 2p correction limbs; the subtract helper shrinks from 221 to 191 local instructions and its wrapper from 103 to 99. Fp4 square uses wide accumulation with four reductions instead of six. [JIT reproduction](../jit/README.md#arm64-carry-and-row-follow-up) describes the private-host CoreLib preparation needed for widening-product imports.

### x64 regression check

All **171 common x64 library instruction listings** are identical to the 74ed6106 baseline. Dependent Fr/Fp mul+add loops also retain their 243/242 instructions. Eight alternating captures without disassembly give Fr mul+add **11.100 → 10.995 ns** and Fp mul+add **19.910 → 19.915 ns**; these complement the unchanged listings and show no repeatable slowdown. ARM64 feature checks fold away on x64. All six final local suites pass **8,478,707 checks each**: compact/expanded on the private patched x64 host and stock .NET 10/11. The complete local 31-fixture Checked matrix includes tiering 0/1, default, JitStress=1/2, register stress 1/3/8 and forced REVERSE_FLAG/SPLIT_TREES_REMOVE_COMMAS; Release also passes.

Both x64 Checked assertions fixed in a0c28efc originate in the #134039 base **0a303c9**, before b65241d/74ed6106: stale register-optional operands during carry-loop containment, and peepholes starting at the function's final group during earlier prolog emission. An exact base-source Checked JIT reproduces both in normal managed tests; current sources pass. The original handoff's alleged new ARM64 paired-flags failure was captured with the older c318f5ac JIT, not 00403488. Existing pairing guards already fix it; deterministic managed regressions now confirm that on both native CLRs. No new wrong-code finding remains open.

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
