# Constant-time validation

Original implementation: `e7377cfe49c5e2fa825ec269cad340ea584837aa`. Current optimized-map results are recorded in [fused map and inversion validation](#fused-map-and-inversion-validation). Captured 2026-10-15 on Windows x64, Ryzen 9 9950X3D. [API contracts](design.md#constant-time-apis), and [timings](performance.md#constant-time-components) accompany this report. Raw captures are not kept in the repository; `scripts/audit-constant-time.ps1` and the benchmark runner's timing-leak mode regenerate them. No claim of formal verification or independent security review is made.

## Builds and functional results

Both Release solution builds completed with zero warnings and zero errors using SDK `11.0.100-rc.1.26425.128`: the default net11.0 test/benchmark executables and `-p:BlsNetTestTargetFramework=net10.0`. The library builds both targets in either command.

| Host | Executable target | Kernel policies | Native reference | Result |
|---|---|---|---|---|
| Stock .NET 10.0.12 | net10.0 | compact, expanded | enabled, disabled | All four full regression runs passed |
| Stock .NET 11.0.0-rc.1.26425.128 | net11.0 | compact, expanded | enabled, disabled | All four full regression runs passed |
| Supplied patched .NET 12.0.0-dev host | net11.0 | compact, expanded | enabled, disabled | All four full regression runs passed |

The full regression matrix included the original constant-time corpus: 1,567,179 checks per native-enabled run, 1,275,516 per explicitly managed-only run. After adding dense-scalar, small-scalar, special Fp2 coefficient and additional alias cases, the extended constant-time suite passed in all twelve configurations: 184,551 checks with native, 163,043 without native. The library implementation was the same throughout these runs. A final complete suite with the extended corpus and default tiering on stock .NET 11 passed **1,579,809 checks**.

Each policy/native configuration compares 2,000 random cases plus scalar boundaries and all 256 single-bit scalars. The extension adds 256 scalars with every bit set except one and small scalars 2–31. Boundaries include 0, 1, r-1, r, r+1 and 2^256-1. Random map outputs supply distinct subgroup bases; projective, affine and aliased multiplication results agree with the original path and native blst. Every random case checks field inversion, masks, signs, roots, maps, affine conversion, additions, equality and negation. Field cases include 0, 1 and p-1; point cases include Z=0 with nonzero X/Y, P+P, P-P and mixed/projective infinity. Tests construct the nonzero SSWU denominator exceptions whenever the corresponding constant has a square root. Fr import and inversion have independent BigInteger references. Existing arithmetic, signed-wide bounds, native curve/map/MSM, pairing, alias and allocation suites remain enabled.

The supplied `artifacts/native/blst.dll` was used, with sources pinned to blst `f62244ef50ad1a603decdb8f215e982d2a467bb6`. Native is a test/benchmark dependency only. The patched runtime was built from dotnet/runtime PR #134039 with the carry-aware lowering that is now part of that PR (head `0a303c9`).

## Generated-code audit

The dedicated `ConstantTimeAudit.Exercise` driver invokes only secret-safe operations after public setup. The script captures the executing code with ReadyToRun disabled, follows direct calls, includes inlined operations through their callers, lists conditional jumps and address-bearing instructions, and reports unresolved/runtime callees and variable-latency candidates. Tiered captures retain every compiled version seen, including Tier0, instrumented Tier0, Tier1 and Tier0-FullOpts. Dynamic stack allocation causes the scalar multiplier and split to use Tier0-FullOpts directly. Both cold and warmed paths are represented; the tiered driver performs 1,024 iterations.

| Runtime / policy | Tiering off: compiled versions / conditional jumps | Tiering on: compiled versions / conditional jumps | Secret-dependent jumps found | Variable-latency candidates |
|---|---:|---:|---:|---:|
| .NET 10 compact | 128 / 271 | 580 / 387 | 0 | 0 |
| .NET 10 expanded | 143 / 275 | 600 / 400 | 0 | 0 |
| .NET 11 compact | 125 / 262 | 571 / 380 | 0 | 0 |
| .NET 11 expanded | 140 / 267 | 588 / 394 | 0 | 0 |
| Patched compact | 125 / 264 | 571 / 382 | 0 | 0 |
| Patched expanded | 140 / 268 | 588 / 395 | 0 | 0 |

Counts include reached managed runtime helpers and all captured versions, rather than counting just public entry points. Absolute addresses and tier promotion order can differ on reproduction.

Manual review followed flag producers and effective addresses, with these findings:

| Area | Allowed control flow and address inputs |
|---|---|
| Fp/Fr Add, Subtract, Negate, Multiply, Square; Fp2 kernels in both policies | Carries/borrows use SETcc or carry flags, and correction uses arithmetic masks/conditional moves. No secret-conditioned jumps. CIOS, expanded-square and wide-reduction row loops have fixed counts. Inlined kernels are also visible in the audit driver's body. Cold AddCarry/SubBorrow/MulAdd/reducer helpers have no secret branches. |
| Mask helpers and point equality | Secret equality, zero and sign results feed arithmetic masks. Selects read both operands. Point equality computes both coordinate comparisons and infinity masks without short-circuiting. |
| Reciprocal/root chains | Fixed square-loop counters, literal table indices and stack cookie/initialization checks. No input-dependent exponentiation branches, reciprocal-GCD paths or root-candidate branches. Fr uses literal exponent digits. |
| SecretScalar | The G1/G2 selector is public. The division loop always processes 256 bits; span checks use the public loop index and lengths. Borrow results feed masks and quotient bits, with no secret-controlled branch or division instruction. |
| SecretMultiply | Public table/component/window counters and span bounds. UInt128 shift helper branches depend on the public window offset. The scalar digit affects only the match/sign masks; every one of the 16 table entries is loaded at public scan indices. No wNAF or variable-time scalar-reduction callees appear. |
| Point formulas | dadd/dadd_affine choose doubling, infinity and ordinary addition using masks. Double is algebraic, including infinity. Remaining jumps initialize fixed stack frames or run the fixed cofactor schedule. |
| SSWU/isogeny | Denominator replacement, square/nonsquare choice, all root rotations and sign fixes use masks. Isogeny polynomial lengths, monic flags, parity checks, power indices and loop bounds are public constants. |
| Runtime plumbing | Public span lengths/nulls, kernel-policy/type/class initialization, stack probes/cookies, fixed-size zeroing/copying, tiering counters and GC state. These are additional public/runtime branches, not secret-dependent exceptions to the contract. |

The external callee lists contain zeroing/copying and runtime/error helpers. Tier0 emits references to both public kernel-policy arms; the inactive arm is covered by the opposite-policy capture. Static initialization computes constants or reads public configuration. No secret path calls `InverseBatched`, `Pow`, wNAF, the original maps or variable-time affine normalization. No DIV/IDIV, gather, PDEP or PEXT candidates were found in the captured call graphs. On this CPU the widening integer multiply instructions used by the field kernels have fixed operand latency. Runtime helper lengths and addresses do not derive from secret values.

Reproduce from the repository root after building both executable targets:

```powershell
./scripts/audit-constant-time.ps1 `
  -Dotnet 'C:\Program Files\dotnet\dotnet.exe' `
  -TargetFramework net10.0 -KernelPolicy compact `
  -OutputDirectory artifacts/audit-net10-compact

# Capture cold, instrumented and warmed tiered code as well.
./scripts/audit-constant-time.ps1 `
  -Dotnet 'C:\Program Files\dotnet\dotnet.exe' `
  -TargetFramework net10.0 -KernelPolicy compact -Tiered `
  -OutputDirectory artifacts/audit-tiered-net10-compact
```

Repeat for expanded policy, stock .NET 11 and the supplied patched host, with the correct target. The script sets/restores DOTNET_ROOT, roll-forward, tiering, ReadyToRun and JIT diagnostic variables. It leaves `disassembly.txt`, `methods.txt`, `jumps.tsv`, `addresses.tsv`, `calls.tsv`, `external-calls.txt` and `variable-latency-candidates.txt` under the selected ignored artifact directory. Inspect the complete disassembly around each jump: the immediately preceding instruction may merely move a register, so follow the actual flags producer. Inspect effective-address provenance and all unresolved callees. The inventory script is not an automatic proof or an automatic pass/fail security test.

These observations apply to the recorded x64 JITs, normal optimized and captured tiered compilation settings. They do not establish behavior under a debugger's MinOpts, NativeAOT, another architecture, arbitrary JIT settings or future runtimes. Repeat the audit when relevant source or runtime code changes. JIT-generated code is audited, not formally verified. No protection against power/EM side channels is claimed.

## Statistical timing experiment

The opt-in benchmark runs a dudect-style Welch t-test for G1/G2 multiplication. Each comparison takes 20,000 fixed and 20,000 random secret scalars in a shuffled balanced order. The fixed classes are zero, one, and `1 + 2^120 + 2^254`. The point is the public generator. Both classes use distinct equal-size buffers; generation, label selection and statistics lie outside the timed region. After 2,000 warm-up operations, timing surrounds one multiplication. Tiering is disabled and logical processor 3 selected.

Both raw statistics and statistics with a single class-independent upper 99th-percentile cutoff are retained. The declared detection threshold is |t| > 4.5 in either view. Variable-time multiplication is a required positive control: the command fails if any control fails to cross the threshold. The test deliberately does not fail ordinary CI or claim a security proof from a small t-value.

| Runtime / default policy | Largest CT \|t\|, raw | Largest CT \|t\|, trimmed | VT controls detected | Smallest VT max(\|raw t\|, \|trimmed t\|) |
|---|---:|---:|---:|---:|
| .NET 10 compact | 1.599 | 2.190 | 6 / 6 | 7.205 |
| .NET 11 expanded | 2.379 | 2.417 | 6 / 6 | 4.717 |
| Patched expanded | 1.432 | 1.642 | 6 / 6 | 24.682 |

None of the 18 constant-time comparisons crossed the threshold in either view. All 18 variable-time controls did; some low-weight controls were detectable only after the declared trimming. The .NET 11 G2 low-weight control was marginal at 4.717, while its zero/one controls were strong. Raw means and t-statistics show substantial shared-VM scheduling noise; these captures neither prove absence of leakage nor statistically cover all secret bases, field inputs or maps.

```powershell
$env:DOTNET_TieredCompilation = '0'
$env:BLST_BENCH_PROCESSOR = '3'
dotnet run --project benchmarks/Nethermind.Crypto.BlsNet.Benchmarks -c Release -- `
  --timing-leak --timing-samples 20000 --no-native
```

Use the explicit host/target commands from [performance](performance.md#reproduction) for the other runtimes. These slow, noisy experiments are separate from the default test and benchmark runs. Optional safegcd inversion and fixed generator tables remain future optimization work; all reported constant-time inverses use the fixed Fermat baseline.

## Fused map and inversion validation

The optimized library at `23aae81` passed fourteen complete runs: stock .NET 10.0.12, stock .NET 11.0.0-rc.1.26425.128 and the patched .NET 12-dev host, each with compact/expanded kernels and native enabled/disabled (twelve runs), plus two expanded patched runs with tiering enabled. Native runs passed **1,592,045 checks** each; managed-only runs passed **1,296,999**. Both executable targets built without warnings. The final JIT's twenty regression tests passed all ten Checked tiering/stress configurations described in [jit/README.md](../jit/README.md), plus Release.

New checks cover lazy values immediately below 2p, square counts 0–380, every input/output alias combination, BigInteger powers and native repeated squares/products. Multiplication by eight tests all reduction thresholds and aliases. Doubling runs compare exact coordinates with repeated ordinary doubles, rescaled points, infinity, Y=0 and native operations. Direct psi² checks its constants, exact coordinates and aliases. The [cofactor proof](cofactor-schedule.md) has executable gcd/norm checks, full base/twist points, independent binary scalar multiplication (including the 636-bit effective G2 cofactor), native binary group operations and explicit 3-/11-/13-torsion doubling cases.

The audit script was rerun for both policies on all three hosts, with tiering disabled and enabled. Counts include every reached compiled version, runtime helpers and Tier0/instrumented Tier0 versions:

| Runtime / policy | Tiering off: versions / jumps | Tiering on: versions / jumps | Variable-latency candidates off / on |
|---|---:|---:|---:|
| .NET 10 compact | 143 / 177 | 617 / 325 | 0 / 0 |
| .NET 10 expanded | 162 / 182 | 649 / 336 | 0 / 0 |
| .NET 11 compact | 140 / 168 | 608 / 318 | 0 / 0 |
| .NET 11 expanded | 159 / 174 | 637 / 330 | 0 / 0 |
| Patched compact | 140 / 170 | 733 / 764 | 0 / 12 |
| Patched expanded | 159 / 175 | 762 / 775 | 0 / 12 |

Manual data-flow review found no secret-dependent jumps or effective addresses. The new leaf arithmetic and psi² use fixed field offsets; repeated squaring and doubling branch only on public counts. G1's choice of cofactor addition tests that same public doubling count; optimized fixed callers fold the choice. The addition/clearing loops seen in the inventory initialize or copy fixed stack frames. The fixed root/inverse tables retain literal indices. No variable-time inversion or scalar recoding is reached.

The twelve patched tiered candidates are public divisions: eight `MemoryMarshal.Cast` divisions use span lengths and fixed generic type sizes; four Tier0/instrumented `SecretMultiply.Multiply` divisions compute `bits / 5`, with bits fixed by the public Fp/Fp2 specialization. None divides point, field or scalar data. Opposite-policy kernels and opposite-type casts in the unresolved inventory are behind public policy/type branches and are covered by the other captures. Remaining runtime callees perform GC coordination, fixed copying/zeroing, or public-length exceptions. The changed map and inversion kernels contain no division instructions.

The timing-leak mode now also measures Fp/Fp2 inversion and G1/G2 maps. Inputs are prepared and shuffled before timing; field random inputs cover [0,2^380), with independent Fp2 coefficients. Each API compares zero, one and low-weight fixed inputs against random inputs. The existing G1/G2 variable-time scalar controls remain mandatory; CT scalar rows obey `--filter` too.

At 20,000 samples per class, all 72 field/map comparisons stayed below |t|=4.5 for both raw and common-cutoff trimmed statistics; all 36 positive controls detected leakage. The largest absolute statistic across both treatments and all four APIs was:

| Runtime / policy | Maximum absolute t | Positive controls |
|---|---:|---:|
| .NET 10 compact | 1.753 | 6 / 6 |
| .NET 10 expanded | 1.881 | 6 / 6 |
| .NET 11 compact | 2.202 | 6 / 6 |
| .NET 11 expanded | 3.514 | 6 / 6 |
| Patched compact | 1.809 | 6 / 6 |
| Patched expanded | 2.003 | 6 / 6 |

Reproduce with the corresponding host and executable target, processor 3 and tiering disabled:

```powershell
& "$env:DOTNET_ROOT/dotnet.exe" '<benchmark DLL>' --timing-leak --timing-samples 20000 --no-native --expanded-kernels --filter 'CT map|CT Fp inverse|CT Fp2 inverse'
```

Repeat with `--compact-kernels`. `scripts/audit-constant-time.ps1` regenerates the code inventories, using `-Tiered` for the warmed capture. These statistical results and manual generated-code review are evidence for the recorded configurations, not a formal proof; repeat after runtime or relevant source changes.
