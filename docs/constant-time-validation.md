# Constant-time validation

Captured 2026-10-15 on Windows x64, Ryzen 9 9950X3D. This report covers the fixed binary-GCD inverses and their secret point/map callers. See [contracts and bounds](design.md#fixed-binary-gcd-inversion) and [measurements](performance.md#fixed-binary-gcd-inverses). Generated code is manually audited, not formally verified or independently reviewed.

## Fixed binary-GCD inversion validation

The default net11.0 solution build and the net10.0 executable build (`-p:BlsNetTestTargetFramework=net10.0`) use SDK 11.0.100-rc.1.26425.128; both completed with zero warnings and errors. The library builds both targets in either command. Native reference is ADX/BMI2 blst `f62244ef50ad1a603decdb8f215e982d2a467bb6`.

| Host | Executable | Policies | Native enabled / disabled | Full-suite checks per run |
|---|---|---|---|---:|
| Stock .NET 10.0.12 | net10.0 | compact, expanded | Both passed | 5,392,057 / 5,081,546 |
| Stock .NET 11.0.0-rc.1.26425.128 | net11.0 | compact, expanded | Both passed | 5,392,057 / 5,081,546 |
| Patched .NET 12.0.0-dev host | net11.0 | compact, expanded | Both passed | 5,392,057 / 5,081,546 |

These twelve sequential full runs use tiering disabled. They include every existing check (1,592,045 with native), plus 3,800,012 inversion checks with native or 3,784,547 without it. The patched JIT is the existing PR #134039 head `0a303c9` plus [follow-up](../jit/README.md), SHA-256 `2E37927C2F21F22923B770DC1F67FF32401CDEC491267BEFB09B6D9F285522D8`. No JIT changes were needed.

The new inverse corpus has 4,096 deterministic random inputs per prime plus zero, one, two, modulus minus one/two, half the modulus, R modulo the modulus, every bit position, long zero/one runs, alternating patterns and Fibonacci-derived values. Fp/Fr outputs have independent BigInteger references; Fp2 coefficients have an independent norm reference. Optional native comparisons cover all three inverses. Tests check canonical Montgomery limbs, aliases, special complex coefficients and zero warmed allocations.

A branching BigInteger model independently constructs each 31-step transition, checks the production approximation and full-width matrix, exact division, determinant, row coefficient bounds, sign corrections, cofactor invariants and the bit-length contraction from Pornin's Appendix A. It verifies the complete 768/512-step schedule for every corpus input, plus raw Montgomery boundary residues and every raw power of two and run of ones. The classic worst cases 2^380 and 2^254 actually reach the 761/509-step bound. Separate signed-wide tests cover full-width random operands, correction endpoints -R²/0/R², and every trial-subtraction threshold. These executable checks supplement the cited proof; sampling is not an exhaustive proof of all field inputs.

## Generated-code audit

`scripts/audit-constant-time.ps1` was run on both policies and all three hosts, with tiering off and on. The warm driver exercises 1,024 iterations and retains every captured version, including Tier0, instrumented Tier0, Tier0-FullOpts and Tier1. Counts include reached runtime helpers.

| Runtime / policy | Tiering off: versions / jumps | Tiering on: versions / jumps | Variable-latency candidates off / on |
|---|---:|---:|---:|
| .NET 10 compact | 149 / 182 | 620 / 330 | 0 / 0 |
| .NET 10 expanded | 168 / 187 | 653 / 341 | 0 / 0 |
| .NET 11 compact | 146 / 174 | 612 / 324 | 0 / 0 |
| .NET 11 expanded | 165 / 180 | 641 / 336 | 0 / 0 |
| Patched compact | 146 / 175 | 737 / 769 | 0 / 12 |
| Patched expanded | 165 / 180 | 766 / 780 | 0 / 12 |

Manual flag and effective-address review found no secret-dependent jumps or dereferenced addresses in the new inverses. `Steps31` has only its fixed 31-step loop jump; its comparison becomes SETB followed by negation and masking. Writing a direct all-ones/zero ternary instead generated a secret-conditioned jump, so the 0/1 carry idiom is deliberate. `Finish` branches on the public count (24 or 47). Full-width matrix and sign-correction helpers have no conditional jumps or calls. Approximation scans fixed field offsets without branches; the normalization uses LZCNT and SHLX/SHRX. Their latency on the recorded CPU does not depend on the operand/shift count. Secret-valued LEA instructions perform arithmetic only; they do not dereference a secret address.

The entry points branch on fixed batch counters and fixed stack initialization. Final correction branches only on the four/six REDC rows and the existing fixed multiplication loops. Masked subtraction/addition uses SETcc or carry flags. The patched JIT emits ADC in signed matrix products, with MULX/ADCX/ADOX/ADC/SBB in correction/REDC and its final Montgomery product. Some signed product pairs remain MUL/ADC; their sign repair prevents a complete multiply-accumulate row. No secret division is introduced.

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

At **20,000 samples per class**, all 252 constant-time comparisons stayed below the threshold in both raw and trimmed views. All 72 variable-time controls detected a difference. The largest absolute t-statistic was 2.916; the smallest positive-control maximum across the two treatments was 20.491. Each cell below is the maximum absolute statistic across both treatments and every class in that API group.

| Runtime / policy | Fp/Fp2/Fr inverse | G1/G2 affine | G1/G2 multiply | G1/G2 maps | Controls |
|---|---:|---:|---:|---:|---:|
| .NET 10 compact | 1.992 | 2.368 | 2.172 | 1.693 | 12 / 12 |
| .NET 10 expanded | 2.034 | 1.901 | 2.648 | 1.122 | 12 / 12 |
| .NET 11 compact | 2.916 | 2.276 | 1.480 | 1.616 | 12 / 12 |
| .NET 11 expanded | 2.182 | 2.346 | 1.662 | 2.242 | 12 / 12 |
| Patched compact | 1.982 | 2.102 | 1.973 | 1.690 | 12 / 12 |
| Patched expanded | 2.707 | 1.737 | 1.863 | 2.399 | 12 / 12 |

```powershell
$env:DOTNET_ROOT = '<host>'
$env:DOTNET_ROLL_FORWARD = 'Major'
$env:DOTNET_TieredCompilation = '0'
$env:BLST_BENCH_PROCESSOR = '3'
& "$env:DOTNET_ROOT/dotnet.exe" '<benchmark DLL>' --timing-leak `
  --timing-samples 20000 --no-native --expanded-kernels --filter 'CT '
```

Repeat with compact kernels and each host/target. The full suite and benchmark commands are in [README](../README.md#build-and-test). Raw captures live under ignored `artifacts/safegcd/` in the validation workspace and can be regenerated; tables retain the results.

These observations apply to the recorded Windows x64 JITs and captured tiering settings. They do not cover debugger MinOpts, arbitrary stress settings, NativeAOT, ARM64, future JITs, power/EM leakage or memory disclosure. Repeat review after relevant source/runtime changes. Statistical non-detection is supporting evidence for these captures, not proof of absence of leakage.
