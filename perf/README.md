# Real ARM64 runtime measurements

[ARM64 performance](../.github/workflows/arm64-perf.yml) runs on pushes to
`arm64-perf` and `arm64-perf-*`, and on manual dispatch. Linux uses real
`ubuntu-24.04-arm` hardware. Set `RUN_MACOS=true` for an additional Apple silicon
`macos-15` job. Windows is not currently included. Concurrency is per ref;
superseded runs on that ref are cancelled automatically.

## Configure and run

Edit only [runtime.env](runtime.env) on your experiment branch:

```sh
RUNTIME_REPO=flcl42/runtime
RUNTIME_REF=74ed610683646896bfd9f22bbbcea93124b44f4a
RUNTIME_PATCHES=''
BENCH_REPETITIONS=3
RUN_MACOS=false
```

Prefer a full runtime commit SHA for reproducibility; branch names work too.
For uncommitted runtime changes, check a patch into this library branch and set
`RUNTIME_PATCHES='perf/patches/change.patch'`. Multiple space-separated paths apply
in order with `git apply`. Do not apply patches already in the selected commit.
The artifact records the resolved runtime and library SHAs and patch hashes.

`BENCH_FILTER` uses case-insensitive substrings separated by `|`. The default
covers Fp/Fp2 multiplication and square, Fr/Fp dependent multiply-add loops,
constant/variable-time Fp inversion and G1/G2 multiplication, Fp12 multiplication,
two-pair pairing checks, 128-point MSM, and both maps. An odd number of repetitions
of at least three is required. All runs retain same-process native blst timings.

```sh
git push origin HEAD:arm64-perf-k
# Or dispatch a branch that already contains the workflow:
gh workflow run arm64-perf.yml --repo flcl42/bls-net --ref arm64-perf-k
gh run list --repo flcl42/bls-net --workflow arm64-perf.yml --branch arm64-perf-k
gh run view RUN_ID --repo flcl42/bls-net
gh run download RUN_ID --repo flcl42/bls-net --name arm64-perf-ubuntu-24.04-arm --dir results
```

## Build and cache

The cold build selects `clr.runtime+clr.corelib+host.native+libs.native+libs.sfx+libs.pretest`
in Release configuration, sufficient to create the libraries testhost without
building NativeAOT, runtime packages, or out-of-band libraries. It saves that
testhost and the runtime bootstrap SDK in an exact-key Actions cache. The key
hashes the effective Git index (after patches), excluding only `src/coreclr/jit`
and `src/tests/JIT`, plus the compiler version and build recipe. Interface headers,
CoreLib, libraries, and build infrastructure therefore invalidate the cache.
There are no fallback restore keys that could mix incompatible runtime versions.

Every run builds just `libclrjit` in Release and Checked configurations. Cache
hits skip all CoreCLR/CoreLib/libraries work. The cached testhost's JIT is replaced
with the freshly built JIT; managed execution with ReadyToRun disabled detects
JIT/EE incompatibility. Failed validation cannot silently fall back to stock JIT.
Cache saving occurs before validation, allowing a codegen bug to reuse a valid
testhost in the next iteration. Linux and macOS caches are separate. Caches are
scoped by GitHub to branches: a new experiment branch may need its own cold build
unless its base is a cache-accessible default branch.

Measured durations on hosted ARM64 runners, including all required tests and
captures in the complete-job columns:

| Runner | Cold testhost | Complete cold job | Warm JIT build step | Complete warm job | Cache |
|---|---:|---:|---:|---:|---:|
| Linux, 4-core Neoverse-N2 | 15m04s | 30m35s | 3m12s–3m18s | 17m12s–17m26s | 237 MiB |
| macOS, 3-core Apple M1, 7 GiB | 27m05s | 44m58s | 4m51s | 20m03s | 233 MiB |

Cold runtime setup including both JITs took 16m38s on Linux and 30m19s on macOS.
The [JIT-only source-change run](https://github.com/flcl42/bls-net/actions/runs/39309342059)
applied the comment-only `cache-probe.patch`, restored the exact Linux baseline
cache in 2s, and built only Release/Checked JITs in 3m18s. The
[final manual dispatch](https://github.com/flcl42/bls-net/actions/runs/39315754704)
removed the probe, hit both caches (3s Linux / 7s macOS), and passed again.
`timings.txt` records script-level seconds; workflow step durations also include
shell startup and teardown. Shared-runner load can change these durations.

## Validation and results

Three consecutive workflows finished green: the [first Linux cold baseline](https://github.com/flcl42/bls-net/actions/runs/39304746175),
the [cache probe and macOS cold run](https://github.com/flcl42/bls-net/actions/runs/39309342059),
and the [unpatched manual repeat on both platforms](https://github.com/flcl42/bls-net/actions/runs/39315754704).
This covers three complete Linux jobs and two complete Apple silicon jobs.

Both policies passed **7,595,581 checks with native blst** and **7,285,065 checks
with `--no-native`** per job. All **21 opt/Add entry points passed in all 20 JIT
configurations** on both platforms (420 successful entry-point executions per job).
The cache fingerprint and report calculation checks also passed in CI.

Full stock / patched `74ed6106` / blst tables for both policies are retained in
the [Linux baseline](baseline-linux-arm64.md) and [macOS baseline](baseline-macos-arm64.md).
The patched host improves the measured stock timings across the selected rows;
many rows still exceed blst. Expanded G2 multiplication and 128-point G2 MSM
are faster than blst in these captures. Read each native column alongside its
managed result, especially on unpinned macOS.

Builds, tests, and captures execute sequentially within each machine. The library
and standalone regression runner target net11.0 using stock .NET 11's SDK. The
patched .NET 12 development testhost runs those same binaries using
`DOTNET_ROOT`, Major roll-forward, and prerelease roll-forward. Native blst is
built with [the ARM64 shell build](../scripts/build-native-blst.sh).

The full BlsNet suite runs with both compact and expanded policies, each with
`--require-native` and `--no-native`. Every C# source in the runtime's `opt/Add`
directory is compiled with a minimal Fact shim and reflection runner, including
new branch-local tests. Return 100 (or 0) means a passing test; the runner itself
returns 100 only with at least one passing test and no failures. Unsupported test
APIs or return types fail visibly rather than being skipped. Release and Checked
JITs each run tiering 0/1 crossed with default, JitStress=2, JitStressRegs=1/3/8.

Each host/policy first runs a full `--quick` sweep, followed by the filtered
repetitions, alternating host order. Linux processes are pinned with `taskset` to
the first allowed CPU. ReadyToRun and tiering are disabled for both hosts.
macOS has no supported equivalent of `taskset`; its captures are unpinned and
should be interpreted separately. Disassembly captures run after timed samples
and include all JIT-compiled methods from the library assembly, covering field,
tower, inverse and curve helpers even when kernel names change.

The summary table and `summary.md` report row medians in µs, with separate native
medians for stock and patched processes and ratios to each host's native median.
The existing harness prints Fp inverse twice with different inputs; these rows
are labelled `(kernel)` and `(tower)`. Its Fermat inverse has no native export and
is shown with a dash. Constant-time rows also retain the variable-time measurement
on identical inputs. Quick sweeps and disassembly runs are excluded from medians.

The `arm64-perf-<runner>` artifact contains `summary.md`, `medians.json`, raw
outputs, `executions.jsonl`, source/machine metadata, build logs, both JIT binaries,
the standalone runner and source manifest, timings, and stock/patched disassembly
for both policies. Artifacts expire after 30 days. Shared VMs and the benchmark
harness's best-round sampling limit statistical precision; small differences
are not reliable speed claims.

The patched host is a full .NET 12 source build, compared with stock .NET 11;
the comparison includes changes throughout the runtime. Scalar and MSM
comparisons also include library algorithm differences from blst. Windows ARM64
has no job in this performance workflow.
