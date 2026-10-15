# Carry-aware JIT

[`carry-rows.patch`](carry-rows.patch) extends dotnet/runtime PR #134039 at `331fa2b3`. The measured host identifies itself as .NET 12.0.0-dev. The library also builds and runs on stock runtimes. See [performance](../docs/performance.md) for timings and runtime build commands, and [design](../docs/design.md#inlining-and-calls) for arithmetic calling patterns.

The patch preserves CF/OF through Montgomery rows and folds carry-based selections. It closes much of the stock field-arithmetic gap. Complete precompile results also depend on variable-time scalar algorithms, endomorphism splits and fused arithmetic; some individual components still trail native blst.

## Lowering and code generation

- Propagate CF/OF across multiply-accumulate rows, including shorter first CIOS lanes and multiple rows per block. Loads, LEA, nonzero constants and safe casts can remain between a carry and its consumer.
- Accept a scalar first carry/borrow seed after copy propagation, including seeds in different locals, only when every defining input is independently proven to be a bit. Arbitrary words and promoted input fields are excluded.
- Propagate flags across nonvolatile integer stores. GC stores and write barriers remain excluded. Alias and exception-order checks still govern movement of reads.
- Hoist closed invariant AND/OR/XOR and complement operand trees before the flag-chain root. Integer NOT preserves CF/OF on x64; moving an operand only before an ADC/SBB could destroy that instruction's incoming flags.
- Fold a final ADC/SBB in place when moving its load would cross an aliased store, provided the intervening instructions preserve flags. Convert carry/borrow mask selections such as `t ^ ((t ^ s) & (0 - carry))` to CMOV using the chain's flags.
- Orient MULX operands around a shared invariant multiplier in RDX and keep results out of RDX. Recompute containment when multiply/carry rewrites change operand roles; stale containment previously triggered a checked-JIT assertion in `BigIntegerCalculator.MulAdd1` under tiering and register stress.
- Remove redundant 64-bit register-to-RDX copies after MULX. The emitter scans at most 32 instructions in a safe region, through known moves, products and carry instructions. Writes to either register, partial writes, calls, branches and unknown instructions stop the search. Register assignments and flag instructions stay intact.
- Use flags-preserving MULX for unsigned scalar BigMul between carry limbs, including its exact multi-register result stores. New conversions require two local operands and the runtime AVX2 ISA group, which includes BMI2. Operand reads stay in place. Signed BigMul remains excluded; clearing containment must not expose a zero constant that would become a flag-writing XOR.
- Optimize OSR methods without adding carry temporaries to a Tier0 frame that can hold only root-method locals.

## Bounded analysis

Full-adder analysis accepts up to 8,192 method nodes and 128 candidates. Current-IR dead-on-exit proofs allow 262,144 visits; extended carry-use scans allow 65,536. Mask-select rescans have a separate 65,536-visit budget. Exhaustion declines optimization. Bit-carry, alias, liveness and exception-order proofs remain required.

The larger allowances let composite Fp2 scale helpers and symmetric Fp square retain their carries in flags. `CompositeCarry` add/subtract bodies each fall from 96 SETcc instructions to one. This does not imply zero materialization in every method: Fp2 inversion and the symmetric square's final doubled cross-term can still materialize carries. Compact calls and register pressure remain relevant.

## Regression coverage

The patch adds tests under `src/tests/JIT/opt/Add/` in the runtime source tree:

| Test | Coverage |
|---|---|
| `MontgomeryRows` | Multiply/carry rows and row boundaries |
| `MaskSelect` | Carry/borrow selections and output aliases |
| `SeededMaskedCarry` | Bit seeds, non-bit rejection, dependent masks, constant multiply rows, overlapping stores and span loops |
| `MulxMultiplierCopies` | Repeated multipliers, partial-width updates, calls and register pressure |
| `ComplementedCarry` | 32/64-bit complements, dependent operands, exact aliases and shifted overlapping buffers |
| `CompositeCarry` | Sixteen unrolled six-word additions/subtractions, observed carries/borrows, full-word edges and aliases against BigInteger |
| `ScalarProductCarry` | Unsigned/signed products between limbs, observed carries, register pressure, aliases with either input and doubled tails in 32/64-bit words |

Release and checked JIT builds pass. All 18 regression entry points pass with the checked JIT under default settings, `JitStress=2` and `JitStressRegs=1/3/8`, each with `TieredCompilation=0/1`: 180 entry-point runs. AVX2-disabled and ADX-disabled configurations each pass all 18 with tiering off/on, adding 72 runs. Disabled-feature field checks also pass.

Both library targets and both kernel modes pass the checked field matrix across the ten stress/tiering configurations: 40 runs of 841,109 kernel checks, plus 12 full-suite runs. Coverage includes reference/native comparisons, signed bounds, aliases, disjoint raw products, preserved/coincident inputs and cold kernel selection. [`test-field-runtime-matrix.ps1`](../scripts/test-field-runtime-matrix.ps1) reproduces the runtime/target/mode matrix; checked-JIT stress settings are additional runtime test configurations.

The patch is LF-only and passes an isolated-index applicability check against the exact `331fa2b3` base. It excludes unrelated build-script changes. The measured final release `clrjit.dll` SHA-256 is `4B1D6CDA360E374663015C2401C1AD1115706989A1BB5E10807EB1CE96CB7279`.
