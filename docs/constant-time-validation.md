# Constant-time validation

Captured 2026-10-15 on Windows x64, Ryzen 9 9950X3D. This report covers the fixed binary-GCD inverses and their secret point/map callers. See [contracts and bounds](design.md#fixed-binary-gcd-inversion) and [measurements](performance.md#fixed-binary-gcd-inverses). Generated code is manually audited, not formally verified or independently reviewed.

## Fixed binary-GCD inversion validation

The default net11.0 solution build and the net10.0 executable build (`-p:BlsNetTestTargetFramework=net10.0`) use SDK 11.0.100-rc.1.26425.128; both completed with zero warnings and errors. The library builds both targets in either command. Native reference is ADX/BMI2 blst `f62244ef50ad1a603decdb8f215e982d2a467bb6`.

| Host | Executable | Policies | Native enabled / disabled | Full-suite checks per run |
|---|---|---|---|---:|
| Stock .NET 10.0.12 | net10.0 | compact, expanded | Both passed | 7,595,569 / 7,285,058 |
| Stock .NET 11.0.0-rc.1.26425.128 | net11.0 | compact, expanded | Both passed | 7,595,569 / 7,285,058 |
| Patched .NET 12.0.0-dev host | net11.0 | compact, expanded | Both passed | 7,595,569 / 7,285,058 |

All twelve configurations passed with tiering disabled. They include every existing check (1,592,045 with native), plus 6,003,524 inversion checks with native or 5,988,059 without it. The patched JIT is PR #134039 head `0a303c9` plus the [combined carry/select follow-up](../jit/README.md), SHA-256 `6DE7EEC60C3D117ACC7DB637F5B7D14161E5E488598CA1C2CF7810E82FA70637`. All 21 JIT regression tests passed in Release and in the ten required Checked configurations (tiering 0/1 crossed with default, JitStress=2 and JitStressRegs=1/3/8). Experimental builds used a private testhost; the final Release JIT was then installed in the shared host.

The new inverse corpus has 4,096 deterministic random inputs per prime plus zero, one, two, modulus minus one/two, half the modulus, R modulo the modulus, every bit position, long zero/one runs, alternating patterns and Fibonacci-derived values. Fp/Fr outputs have independent BigInteger references; Fp2 coefficients have an independent norm reference. Optional native comparisons cover all three inverses. Tests check canonical Montgomery limbs, aliases, special complex coefficients and zero warmed allocations.

A branching BigInteger model independently constructs each 31-step transition, checks the production full/191-bit approximation and matrix updates, every narrow/mixed-width cofactor row including its sign/excess word, exact division, determinant, coefficient growth/width bounds, sign corrections, cofactor invariants and the bit-length contraction from Pornin's Appendix A. It verifies the complete 768/512-step schedule for every corpus input, plus raw Montgomery boundary residues and every raw power of two and run of ones. The classic worst cases 2^380 and 2^254 actually reach the 761/509-step bound. Separate signed-wide tests cover full-width random operands, correction endpoints -R²/0/R², and every trial-subtraction threshold. These executable checks supplement the cited proof; sampling is not an exhaustive proof of all field inputs.

## Generated-code audit

`scripts/audit-constant-time.ps1` was run on both policies and all three hosts, with tiering off and on. The warm driver exercises 1,024 iterations and retains every captured version, including Tier0, instrumented Tier0, Tier0-FullOpts and Tier1. Counts include reached runtime helpers.

| Runtime / policy | Tiering off: versions / jumps | Tiering on: versions / jumps | Variable-latency candidates off / on |
|---|---:|---:|---:|
| .NET 10 compact | 151 / 193 | 623 / 341 | 0 / 0 |
| .NET 10 expanded | 170 / 198 | 655 / 352 | 0 / 0 |
| .NET 11 compact | 148 / 185 | 614 / 335 | 0 / 0 |
| .NET 11 expanded | 167 / 191 | 643 / 347 | 0 / 0 |
| Patched compact | 148 / 186 | 739 / 780 | 0 / 12 |
| Patched expanded | 167 / 191 | 768 / 791 | 0 / 12 |

Manual flag and effective-address review found no secret-dependent jumps or dereferenced addresses in the new inverses. The 31-step loop is inlined into each public phase; its only jump is the public counter backedge. Stock runtimes use SETB plus masks; the patched JIT uses eight CMOVs and two TESTs in the standalone loop. Comparisons remain 0/1 before negation because an all-ones/zero ternary, or several ordinary conditional swaps sharing a condition, can introduce secret branches. `Finish` branches only on its public 55/47 count. Full and narrow matrix/cofactor helpers have no conditional jumps or calls.

Approximation scans fixed field offsets with masks. Its secret normalization count feeds LZCNT and SHLX/SHRX, whose latency on the recorded CPU is independent of the operand/count; it never controls indexing or a loop. Secret-valued LEA instructions perform arithmetic without dereferencing their results. Entry-point jumps use fixed batch counts or fixed stack initialization; correction uses four/six REDC rows and existing multiplication loops. Reached external calls initialize public configuration, coordinate GC, copy/zero fixed storage or handle public lengths. Warmed inversions allocate zero bytes.

Conditional negation plus unsigned products exposes complete MULX/ADCX/ADOX rows. For Fp, full GCD updates and narrow cofactor rows contain 12 MULX, 12 ADCX and 8 ADOX; late three-limb updates contain 6/6/5; the wide v row contains 19/19/14. Fr's corresponding full/narrow and wide rows contain 8/8/6 and 13/13/10. Sign handling stays in carries and masks. Final correction/REDC uses the existing carry idioms and direct carry-flag selects. No secret division is introduced.

The twelve patched tiered candidates are unchanged public divisions: eight in `MemoryMarshal.Cast` use public span lengths and fixed type sizes; four in `SecretMultiply.Multiply` compute the public field-specialized bit count divided by five. Opposite-policy/type unresolved calls are covered by the other captures. Other runtime calls initialize public configuration, coordinate GC, copy/zero fixed storage or handle public lengths. No variable-time inversion, `Pow`, wNAF or original variable-time maps enter the secret call graph.

The existing secret scalar/point/map bodies retain their earlier reviewed control flow: public window/component/table scans, fixed exponent/cofactor loops, and masked root/exceptional-point choices. Scalar digits affect masks, with every table entry read. Equality, infinity and map sign decisions feed masks. The changed reciprocal introduces no additional exceptions or length-dependent accesses.

Reproduce after building both executable targets:

```powershell
./scripts/audit-constant-time.ps1 -Dotnet '<host>/dotnet.exe' `
  -TargetFramework net11.0 -KernelPolicy expanded `
  -OutputDirectory artifacts/audit-expanded
# Repeat with -Tiered, compact policy, and each host/target.
```

Use net10.0 for stock .NET 10. The script sets/restores root, roll-forward, ReadyToRun, tiering and diagnostic settings. Review `disassembly.txt`, `jumps.tsv`, `addresses.tsv`, `calls.tsv`, `methods.txt`, `external-calls.txt` and `variable-latency-candidates.txt`; the inventory alone is not a pass/fail security test.

## ARM64 validation

The initial portability captures below use stock .NET 10.0.12 and .NET 11.0.0-rc.1.26425.128 and precede the ARM64 arithmetic kernels. The [ARM64 follow-up](../jit/README.md#arm64-carry-and-row-follow-up) adds separate private-host AltJit captures with the patched runtime and current kernels. This is a manual control-flow/address review, not an independent security audit or a claim about physical ARM CPU latency.

The patched AltJit inventory with runtime `c318f5ac` covers 156 reachable methods, with no variable-latency candidates. The unrolled Fp multiply/square, disjoint raw product, positive wide reducer and fused imaginary-product reducer have no conditional branches. The 31-step inverse kernel uses three CSELs and its public backedge; the Fp inverse wrapper's branches count public batches/steps or clear its fixed stack-frame range. Addressed stack fields and loads/stores preserve NZCV without reordering accesses. Remaining unresolved helpers require capture on native ARM64; the x64 fallback can execute different helper boundaries. Run `scripts/audit-constant-time.ps1 -Arm64AltJit clrjit_universal_arm64_x64.dll -Dotnet <private-host>` for this local inventory, and omit `-Arm64AltJit` on the native ARM64 host.

Runtime `c318f5ac` also fixes two correctness-review findings in mask lowering: TEST comparisons must retain bit-test semantics, and newly inserted TEST/CMP nodes must not overwrite live carry/borrow flags. Rejected mask optimizations retain their branch-free bitwise implementation. The new regressions pass all ten x64 Checked stress/tiering configurations; extracted corrected ARM64 select and carry leaves pass 680,000 comparisons under QEMU. Native CLR stress results remain required before declaring the checkpoint verified on hardware. These changes preserve correctness and constant-time structure; constant-time operation speed is outside the optimization target.

Real Neoverse-N2 listings from [run 39314252575](https://github.com/flcl42/bls-net/actions/runs/39314252575), runtime `8a918efe` and library `2441b94`, close the compiled Fp inverse, secret multiplication and constant-time map call graphs: **100 compiled methods, 14 runtime/helper boundaries, zero unresolved library callees and zero variable-latency candidates**. Manual review attributes conditional branches to fixed schedules, public lengths and polynomial degrees, public type/configuration initialization, stack probes/clearing and GC helpers. Indexed scalar accesses follow the public bit/window counters; point selection scans all sixteen entries. Field arithmetic kernels have no conditional branches. This capture does not cover the standalone Fr inverse or every secret API; retain the broader audit driver captures too.

The inventory accepts repeated `--root` signature substrings to reproduce this review directly from the native benchmark artifact. It rejects a requested root absent from the listing. Comments naming generic methods are excluded from address operands.

```powershell
python scripts/list-constant-time-jumps.py disasm-patched-expanded.asm `
  --root 'Fp:InverseConstantTime(' --root 'SecretMultiply' `
  --root 'MapToG1:MapConstantTime(' --root 'MapToG2:MapConstantTime('
```

### Cross-compilation

The matching `Microsoft.NETCore.App.Crossgen2.win-x64` NuGet packages provide the stock cross-targeting ARM64 JIT. Six optimized composite captures target Linux, macOS and Windows using each target's ARM64 runtime-pack managed assemblies. **Do not use the host x64 CoreLib:** its architecture-specific intrinsic bodies change the result, including widening multiplication. The audit script rejects an x64 CoreLib.

Crossgen2 skips `AggressiveOptimization` methods. The script clears only that MethodDef implementation flag in disposable input copies, preserving IL and other attributes; production binaries are untouched. Without this step, the inverses, hot kernels and audit root would be absent. These outputs are inspection artifacts, not deployment binaries. Both runtime policy paths remain in R2R code. Relocation/static-initialization checks and indirect calls can differ substantially from runtime FullOpts/Tier1, so these captures supplement the actual runtime listings below.

```powershell
./scripts/audit-constant-time.ps1 -Crossgen2 '<crossgen2-package>/tools/crossgen2.exe' `
  -RuntimeDirectory '<target-runtime-package>/runtimes/linux-arm64/lib/net11.0' `
  -TargetFramework net11.0 -TargetOS linux -OutputDirectory artifacts/audit-arm64-r2r
# Repeat with matching 10.0.12/net10.0, osx-arm64/-TargetOS osx,
# and win-arm64/-TargetOS windows runtime packs.
python scripts/test-disassembly-inventory.py
```

The inventory recognizes ARM64 `b.<cond>` and RyuJIT aliases such as `bge`/`blo`, `cbz/cbnz`, `tbz/tbnz`, direct and indirect calls/tail branches/returns, and `udiv/sdiv`. `csel/csinc/cset/csetm/cneg` are selections, not branches. `addresses.tsv` retains every address operand; `indexed-addresses.tsv` highlights register-indexed operands for data-flow review. Fixed stack/relocation operands remain visible too. No mnemonic-only check can determine whether an index is secret. Repeated identically named tiered versions are now retained rather than overwritten.

| Stock crossgen2 | ARM64 target | Compiled versions (whole inventory) | Division candidates |
|---|---|---:|---:|
| .NET 10.0.12 | Linux | 766 | 6, public MSM |
| .NET 10.0.12 | macOS | 766 | 6, public MSM |
| .NET 10.0.12 | Windows | 766 | 6, public MSM |
| .NET 11 RC1 | Linux | 768 | 6, public MSM |
| .NET 11 RC1 | macOS | 768 | 6, public MSM |
| .NET 11 RC1 | Windows | 768 | 6, public MSM |

Review of the secret API bodies and their arithmetic helpers found no secret-dependent conditional branch or dereferenced address in these six targets. All six full inventories contain six division candidates in variable-time public MSM sizing/window code, outside the secret APIs. Policy/type/static initialization, public lengths, fixed schedules, stack probes and coefficient-array bounds explain the remaining branches. R2R unresolved relocation calls are explicitly inventoried with `--all-methods`; that scope includes variable-time APIs and the test runner and must not be mistaken for an automatically proven secret call graph.

### Linux ARM64 runtime code

QEMU AArch64 8.2.2 on Ubuntu 24.04.3 produced actual ARM64 runtime JIT listings with ReadyToRun and tiering disabled. The emulated process reports Arm64 and linux-arm64; the underlying WSL2 host is x64. The launcher uses `QEMU_CPU=cortex-a72`, `DOTNET_EnableWriteXorExecute=0`, normal ARM64 ICU, four reported processors and a 2 GiB GC heap limit. Captures include the original audit driver plus a preparation probe forcing standalone multiply/square and inversion-loop bodies, without changing their attributes. These source kernels are identical to this checkout's production code.

| Runtime / policy | Inventory scope | Compiled versions / branches | Division candidates |
|---|---|---:|---:|
| .NET 10 compact | All captured methods | 440 / 1,869 | 13, runtime startup/formatting |
| .NET 10 expanded | All captured methods | 459 / 1,874 | 13, runtime startup/formatting |
| .NET 11 compact | Reached audit graph | 151 / 194 | 0 |
| .NET 11 expanded | Reached audit graph | 170 / 200 | 0 |

.NET 10's ARM64 indirect calls lack symbolic target annotations. Its graph inventory alone reaches only the root and warns about missing closure; review therefore covered all captured library bodies, with no division in any of them. .NET 11's annotations allow graph traversal. Counts have different scopes and are not comparable security scores.

Four additional 1,024-iteration warm captures from this checkout include Tier0, instrumented Tier0, Tier0-FullOpts and Tier1:

| Runtime / policy | Inventory scope | Versions / branches | Division candidates |
|---|---|---:|---:|
| .NET 10 compact | All captured methods | 1,645 / 2,831 | 12, public runtime initialization/formatting |
| .NET 10 expanded | All captured methods | 1,679 / 2,846 | 12, public runtime initialization/formatting |
| .NET 11 compact | Reached audit graph | 621 / 422 | 0 |
| .NET 11 expanded | Reached audit graph | 659 / 438 | 0 |

No secret-dependent branch or dereferenced address was found in those versions. The .NET 10 whole-capture inventory also includes ordinary variable-time helpers used before the audit root during constant initialization; for example, `Fp2.EqualsZero` short-circuits there and is absent from the annotated .NET 11 secret graph. Tiered `br x0` sites follow `CORINFO_HELP_PATCHPOINT` and use its public OSR target, not a scalar-derived address. Additional serialization bounds checks use public destination lengths; cofactor schedule branches use public fixed step counts.

The provided emulator runner was reused in an isolated ignored directory, with its native-build hook invoking this checkout's shell script. This checkout passed all six full-suite cases below, with tiering disabled and warning-free self-contained publishes. Both processes identify as Arm64 and load an AArch64 blst shared library built from the pinned assembly sources. The independent baseline matrix also passed all 16 runtime × policy × native-on/off × default/disabled-tiering cases on the unchanged production arithmetic.

| Executable/runtime | Native compact | Native expanded | Managed-only default |
|---|---:|---:|---:|
| net10.0 / .NET 10.0.12 | 7,595,581 | 7,595,581 | 7,285,065 |
| net11.0 / .NET 11 RC1 | 7,595,581 | 7,595,581 | 7,285,065 |

These totals include 12 native-enabled / seven managed-only portability checks, covering sizes/offsets, little-endian layout, integer predicate returns and conditional-negation flags 0, 1, 256 and -1. Native-enabled runs use `--require-native`, so a missing reference cannot silently pass.

In both runtimes, inversion comparisons feed `cset`/masks, normalization feeds `clz`/shifts, and update/cofactor leaf kernels have no conditional branches or calls. `Steps31` has one fixed counter backedge; `Finish` checks and decrements its public count. Scalar multiplication scans every table entry. Branches emitted for UInt128 shifts depend on the public window offset, not the scalar; indexed table/digit accesses use component/scan counters. Isogeny branches use fixed polynomial lengths, degree parity and loop counters. Map branches loading polynomial arrays test public references and static initialization. Secret exceptional points, signs and root choices stay in arithmetic masks. Indirect targets are runtime entry cells/type-specialized methods; secret limbs do not form call targets. No source security fix was necessary.

### Hot-kernel findings and policy

Actual runtime preparation captures give the following code sizes; these are static bytes, not timings or dynamic instruction counts.

| Kernel | .NET 10 ARM64 bytes | .NET 11 ARM64 bytes |
|---|---:|---:|
| Fp MultiplyComposite | 1,508 | 1,508 |
| Fp MultiplyWide compact / expanded | 352 / 1,372 | 352 / 1,372 |
| Fp SquareExpanded | 1,984 | 1,984 |
| Fp2 Multiply compact / expanded | 652 / 668 | 652 / 668 |
| Fp2 Square compact / expanded | 588 / 3,620 | 588 / 3,616 |
| InverseBatch Steps31 / Finish | 224 / 172 | 224 / 172 |
| G1 XYZZ Add / AddAffine | 684 / 716 | 684 / 716 |
| G2 XYZZ Add / AddAffine | 852 / 1,112 | 852 / 1,112 |

Widening products use `mul`/`umulh`, with no missing multiplication intrinsic in the hot runtime leaf bodies. Stock carries still materialize through compare/`cset`/add chains. Expanded raw multiplication removes loop control but increases code size; expanded Fp square saves widening products (57 versus 72 dynamically). Expanded Fp2 multiplication reduces raw products before canonicalization, while its square inlines two CIOS products and increases register pressure: 35 stack-referencing instructions versus 12 in the compact wrapper, including saved registers rather than solely spills. Leaf field products/reducers have fixed loop bounds and no range-check failure calls. Inversion orchestration retains intentional helper boundaries. Actual full-suite captures also cover public MSM buckets: G1 Add/AddAffine have 12/11 calls, G2 have 13/13, with no range-check failure calls. These are the intended isolated field/point helpers; exceptional-point branches belong to the variable-time public API.

These tradeoffs alone did not establish a hardware winner. Hosted ARM64 CI benchmarks then showed expanded faster on .NET 10 too, on Linux and macOS, so ARM64 now defaults to expanded on every runtime ([measurements](performance.md#arm64-hardware)). No arithmetic rewrite was justified. Carry-chain improvements remain a JIT opportunity. See the [x64 before/after measurements](performance.md#arm64-portability-regression-capture).

### x64 regression audit

Both policies were re-audited on all three x64 hosts, tiering off/on. Production source is unchanged; the inventory now preserves repeated versions, so counts differ from the historical table.

| Runtime / policy | Tiering off: versions / branches | Tiering on: versions / branches | Latency candidates off / on |
|---|---:|---:|---:|
| .NET 10 compact | 154 / 193 | 636 / 382 | 0 / 0 |
| .NET 10 expanded | 173 / 198 | 668 / 393 | 0 / 0 |
| .NET 11 compact | 151 / 185 | 627 / 376 | 0 / 0 |
| .NET 11 expanded | 170 / 191 | 656 / 388 | 0 / 0 |
| Patched compact | 151 / 186 | 748 / 780 | 0 / 12 |
| Patched expanded | 170 / 191 | 777 / 791 | 0 / 12 |

No new secret-dependent branch or address was found. The twelve patched tiered candidates remain public span-size and window-count divisions, as described above. All twelve full-suite host × policy × native-on/off combinations passed: 7,595,581 checks with native and 7,285,065 without, including the new 12/7 ABI/layout checks. Both framework builds remained warning-free.

Real Linux ARM64 execution/performance and an untiered native review are recorded above and in [performance](performance.md#arm64-hardware). macOS/Windows ARM64 runtime Tier1 review and physical leakage experiments remain open. Crossgen2 and QEMU do not establish processor timing behavior.

## Statistical timing experiment

The opt-in benchmark uses shuffled balanced fixed/random classes, 2,000 warm-up operations, and a Welch t-test on raw timings plus timings with one class-independent upper 99th-percentile cutoff. Input preparation, field parsing, point rescaling and allocations are outside timing. Threshold is |t| > 4.5 in either view. Positive controls are mandatory: the command fails if a variable-time control fails to detect a difference.

Inversion and to-affine inputs compare zero, one, two, modulus minus one, 2^120 and 1+2^120+2^254 against random values. Fp2 uses independent random coefficients; the minus-one class uses both coefficients p-1. To-affine points are rescaled generators with those secret Z values (zero yields infinity). G1/G2 multiplication and maps retain zero, one and low-weight classes. Controls include variable-time Fp inversion and the existing variable-time G1/G2 scalar multiplication.

At **20,000 samples per class**, all 252 constant-time comparisons stayed below |t|=4.5 in both raw and trimmed views. All 72 variable-time controls detected a difference. The largest absolute constant-time statistic was 3.621; the smallest positive-control maximum across the two treatments was 18.063. Each table cell is the maximum across both treatments and every class in that API group.

| Runtime / policy | Fp/Fp2/Fr inverse | G1/G2 affine | G1/G2 multiply | G1/G2 maps | Controls |
|---|---:|---:|---:|---:|---:|
| .NET 10 compact | 3.621 | 2.393 | 1.817 | 1.685 | 12 / 12 |
| .NET 10 expanded | 1.784 | 2.192 | 1.910 | 1.523 | 12 / 12 |
| .NET 11 compact | 2.095 | 1.885 | 1.096 | 3.032 | 12 / 12 |
| .NET 11 expanded | 1.788 | 1.807 | 1.585 | 1.803 | 12 / 12 |
| Patched compact | 1.704 | 1.711 | 2.582 | 1.320 | 12 / 12 |
| Patched expanded | 2.422 | 2.194 | 1.701 | 1.653 | 12 / 12 |

```powershell
$env:DOTNET_ROOT = '<host>'
$env:DOTNET_ROLL_FORWARD = 'Major'
$env:DOTNET_TieredCompilation = '0'
$env:BLST_BENCH_PROCESSOR = '3'
& "$env:DOTNET_ROOT/dotnet.exe" '<benchmark DLL>' --timing-leak `
  --timing-samples 20000 --no-native --expanded-kernels --filter 'CT '
```

Repeat with compact kernels and each host/target. The full suite and benchmark commands are in [README](../README.md#build-and-test). Raw captures are not kept in the repository; the audit script and the benchmark runner's timing-leak mode regenerate them, and the tables retain the results.

The timing experiment applies to the recorded Windows x64 JITs and captured tiering settings. It does not cover debugger MinOpts, arbitrary stress settings, NativeAOT, ARM64 physical timing, future JITs, power/EM leakage or memory disclosure. The separate ARM64 generated-code coverage is described above. Repeat review after relevant source/runtime changes. Statistical non-detection is supporting evidence for these captures, not proof of absence of leakage.
