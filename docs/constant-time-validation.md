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

These observations apply to the recorded Windows x64 JITs and captured tiering settings. They do not cover debugger MinOpts, arbitrary stress settings, NativeAOT, ARM64, future JITs, power/EM leakage or memory disclosure. Repeat review after relevant source/runtime changes. Statistical non-detection is supporting evidence for these captures, not proof of absence of leakage.
