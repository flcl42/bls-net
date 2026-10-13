# Carry-aware JIT

`carry-rows.patch` extends dotnet/runtime PR #134039 at commit `331fa2b3` with carry propagation across Montgomery rows, flag-based selects and OSR support. It is an experimental runtime patch; the library builds and runs on the stock JIT without it. The measured patched launcher identifies itself as .NET 12.0.0-dev.

The patched Fp multiplier is near native ADX blst parity. The field follow-up removes carry materialization from the captured hot kernels and improves Fp2 multiplication, but Fp2 multiply/square remain slightly slower than blst. Larger formulas still depend on compact arithmetic calls and register pressure. See the [field-kernel report](../docs/field-kernel-report.md) for before/after timings, regression coverage and patch changes, [performance](../docs/performance.md) for historical EIP-2537 measurements and reproduction commands, and [design](../docs/design.md#inlining-and-calls) for the calling pattern.

The [round 2 report](../docs/field-kernel-round2-report.md) records runtime-based selection for net10.0 and net11.0 library builds. This revision also removes repeated 64-bit multiplier copies to RDX across MULX carry rows. Its emitter peephole scans at most 32 instructions within a safe region, accepts only known integer move/carry instructions, and stops on a write to either register or any unknown instruction. The `MulxMultiplierCopies` regression covers repeated multipliers, partial-width updates, calls and register pressure. All 16 JIT regression entry points pass with the checked JIT under the default, `JitStress=2`, `JitStressRegs=1/3/8` matrix and tiering both off and on.

Integer NOT now participates in carry chains: it preserves CF/OF on x64, and a closed invariant complement tree can be moved before the flags producer using the existing side-effect checks. `ComplementedCarry` covers 32/64-bit words, dependent operands, and overlapping buffers whose stores change later reads. This enables the complemented-add negation formula without per-limb carry materialization.

## Composite carry analysis

The round 3 patch raises the bounded full-adder analysis from 2,048 to 8,192
nodes and from 32 to 128 candidates. The carry proof rules are unchanged.
The current-IR dead-on-exit allowance increases from 65,536 to 262,144 visits,
and extended carry-use scans from 16,384 to 65,536. Budget exhaustion still
declines optimization. Larger Fp2 small-constant helpers previously exceeded the
whole-method/candidate limits; symmetric square exhausted liveness/carry-use
scans before its multiply rows and final correction could fold.

`CompositeCarry` covers sixteen unrolled six-word additions and subtractions,
with random/full-word boundary inputs, observed row carries/borrows and aliased
outputs checked against BigInteger. Its add/subtract methods each change from
96 SETcc instructions to one. All eighteen entry points pass the ten checked-JIT
stress/tiering configurations. See [the round 3 report](../docs/field-kernel-round3-report.md)
for composite-path timings, remaining flag materialization and installation hash.

Unsigned scalar BigMul between carry limbs can now use MULX, including its
multi-register result stores. The conversion preserves values and memory-read
positions; it requires two local operands and the runtime AVX2 group (which
includes BMI2). Clearing containment cannot expose a zero constant as XOR.
The ordinary signed BigMul remains excluded. `ScalarProductCarry` covers unsigned
and signed products under register pressure, observed carries, output aliases
with either input, and independent/dependent doubled tails in 32/64-bit words.
The checked matrix additionally runs with AVX2 and ADX disabled, and the field
kernel suite verifies both disabled-feature fallbacks.
