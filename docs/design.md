# Design

Nethermind.Crypto.BlsNet implements BLS12-381 arithmetic in C#. Production code uses fixed-width value types, spans and limb arithmetic. It has no native runtime dependency and does not use `BigInteger`.

## Layout and namespaces

The library lives in `src/Nethermind.Crypto.BlsNet/` and targets `net10.0` and `net11.0`.

| Namespace | Contents |
|---|---|
| `Nethermind.Crypto.BlsNet.Arithmetic` | Fp, Fr, Fp2, Fp6, Fp12, limb operations, inversion and wide intermediates |
| `Nethermind.Crypto.BlsNet.Curves` | Affine and projective G1/G2 points, generic formulas, subgroup checks, scalar multiplication and MSM |
| `Nethermind.Crypto.BlsNet.Hashing` | Field roots, SSWU maps, isogenies and cofactor clearing |
| `Nethermind.Crypto.BlsNet.Pairing` | Miller loops, final exponentiation and pairing checks |

`tests/` contains three console runners. `benchmarks/` contains the field benchmark runner. The optional `external/blst` submodule supplies the native reference and constants for `scripts/port_constants.py`. `jit/` contains the experimental carry-aware runtime patch. Build outputs belong under ignored `artifacts/`.

## Field representation

Fp stores a canonical Montgomery residue in six 64-bit limbs, least-significant limb first. Its modulus p has 381 bits, and its Montgomery radix is R = 2^384. The stored residue for x is xR mod p. Fr uses four 64-bit limbs with radix 2^256 for the scalar order r. Multiplication uses coarsely integrated operand scanning (CIOS) and a final conditional reduction.

Fp occupies 48 bytes; Fr occupies 32 bytes. Extension fields and public points follow blst's coefficient and coordinate order. Differential tests verify their memory layout. This compatibility is for in-memory arithmetic values; blst's compressed and uncompressed wire formats remain open.

Fp parsing accepts 48-byte canonical big-endian values and rejects values at or above p. Raw G1 coordinates are X followed by Y. Raw G2 coordinates are X.C0, X.C1, Y.C0, Y.C1. Affine infinity uses zero coordinates; projective infinity has Z = 0. EIP-2537's 64-byte padded field encoding and big-endian scalars need conversion by the caller.

Negation expresses p-a as p+~a+1 modulo 2^384. The first word absorbs the initial one into the modulus limb and explicitly computes its carry; the remaining words use `AddCarry`. This avoids constant-left subtractions becoming NEG/ADD and per-word comparisons in inline callers. A nonzero-input mask still maps zero to zero. The patched JIT keeps CF across integer NOT and moves a complement's closed invariant operand tree using the same alias and exception-order checks as masked additions. It can also select MULX for unsigned scalar BigMul products between carry limbs, leaving the products and their operand reads in place. New conversions require local operands and the runtime AVX2 ISA group, which includes BMI2. Signed products and zero-constant materialization retain their existing handling.

## Extension tower

The tower uses the same basis as blst:

| Type | Definition | Coefficient order | Size |
|---|---|---|---:|
| Fp2 | Fp[u] / (u² + 1) | C0 + C1 u | 96 bytes |
| Fp6 | Fp2[v] / (v³ - (1 + u)) | C0 + C1 v + C2 v² | 288 bytes |
| Fp12 | Fp6[w] / (w² - v) | C0 + C1 w | 576 bytes |

Arithmetic generally uses `out result, in input...` and permits output/input aliases. Curve and pairing APIs place the output last. Fp6 and Fp12 expose addition, subtraction, negation, multiplication, squaring, inversion and Frobenius maps. Frobenius maps support powers 1, 2 and 3.

Fp6 has `MultiplyByV`, `MultiplyBy0Y0` and `MultiplyByXY0`; the last ignores C2 of its second input. Fp12 has conjugation, cyclotomic squaring and sparse multiplication. The three packed Fp2 coefficients in `MultiplyBySparse` occupy positions 0, 1 and 4 in the Fp12 product. `CyclotomicSquare` matches blst's formula on arbitrary inputs, but equals ordinary squaring only within the cyclotomic subgroup.

## Inversion

`Fp.Inverse` uses variable-time binary GCD. It forms 31-bit top/bottom approximations, accumulates signed 2×2 update matrices, then applies exact six-limb updates. Coefficient rows are reduced by adding a multiple of p before division by a power of two. Limb products retain the multiply-add and carry idioms used by the Montgomery kernel. Zero inverts to zero.

Fp2, Fp6 and Fp12 inversion reduce through tower norms to base-field inversion. Fermat inversion supplies an independent test reference. See [performance](performance.md#field-kernels) for inversion timings.

## Runtime kernel policy

Kernel selection follows the **executing runtime**, independently of the library's target framework. `Fp.UseExpandedKernels` is a static readonly Boolean: it defaults to `Environment.Version.Major >= 11`. Stock .NET 10 selects compact kernels; .NET 11 and the patched .NET 12-dev host select expanded kernels, including when they execute a net10.0 build or sources linked into Nethermind's net10.0 project. Once the field type is initialized, optimized JIT code folds the Boolean and removes the unused dispatch branch. Small inlineable dispatchers read the policy before calling distinct `NoInlining | AggressiveOptimization` kernels. This also handles a cold first call from raw Montgomery data: the selected kernel compiles after initialization and contains no policy branch. Tier1 callers inline the dispatcher and call only the selected kernel; a cold Tier0 caller can retain the selection until it is promoted.

For testing, set the AppContext switch `Nethermind.Crypto.BlsNet.UseExpandedKernels` before the first field use, or set `BLSNET_EXPANDED_KERNELS` to `0`/`1` or `false`/`true` before launching the process. An explicitly set AppContext switch takes precedence. Missing or unrecognized environment values use the runtime default. Selection is fixed for the process after initialization; these settings do not change arithmetic results or instruction-set support. Expanded kernels are deliberately slower on stock .NET 10 and remain available there for differential tests.

Expanded Fp square accumulates the fifteen cross products once, doubles them, and adds six diagonal products through one bit-carry chain before six Montgomery reduction rows. This uses 57 widening products, versus 72 for CIOS square. For canonical inputs the raw square is below p² < pR and its Montgomery quotient is below 2p. The doubled cross terms stay below word eleven; the last diagonal addition supplies the top word, including its carry. The output is written only after every input limb is consumed. Compact square retains CIOS multiplication of the input by itself.

## Wide intermediates and lazy reduction

`FpWide` is a signed 768-bit value with twelve 64-bit limbs. `Fp.MultiplyWide` produces a raw product; `AddWide` and `SubtractWide` combine raw values. These intermediates are internal arithmetic values, not canonical Fp residues.

The expanded raw multiplier expands all six rows. Its reducers first reduce the low 384 bits, then add the signed high 384 bits: the cancellation multiplier depends only on the low half, so this is the same Montgomery quotient without a carry bit passing between rows. Compact mode retains the row loops because stock .NET 10 spills the expanded multiplier.

Let R = 2^384. `ReduceWide` requires |value| < 3pR. Its Montgomery quotient lies in (-3p, 4p); it adds 4p when negative, then reduces by 2p and p. `ReduceWideSmall` requires |value| < pR. Its quotient lies in (-p, 2p), so one sign correction and final reduction suffice. Both return canonical Montgomery values.

`ReduceWidePositive` requires 0 ≤ value < pR and omits the sign correction in expanded mode. Expanded Fp2 multiplication uses three raw products and two reductions: the real component has magnitude below p², and the imaginary component is in [0, 2p²), so the small signed and positive reducers apply respectively. Its unreduced input sums are below 2p. `SubtractWideImaginary` subtracts the first two products from the third in place, leaving its two inputs intact. `ReduceWideDifference` combines the real subtraction and signed reduction with the difference kept inside the reducer, avoiding a twelve-word scratch write/read. It requires |a-b| < pR and permits output overlap with either input. The three products use `MultiplyWideNoAlias`, which writes low words directly into distinct output scratch. Its output must be disjoint from both inputs; the inputs may alias each other. Ordinary `MultiplyWide` retains its output-overlap guarantee. All three products are computed before either output coordinate is written, preserving public Fp2 input/output aliases. Compact mode retains three CIOS multiplications.

Fp2 square uses two CIOS multiplications. Expanded mode inlines the imaginary product and isolates the real product, saving one call while staying within the carry-analysis budget. Compact mode isolates both: stock .NET 10 spills the larger inline method. Fully wide and fully inline square formulas did not improve the measured paths.

`WideTower.Multiply2` forms unreduced Fp2 products. For canonical inputs, the real component has magnitude below p² and the imaginary component lies in [0, 2p²). `Add2`, `Subtract2` and `NonResidue2` combine them. `Multiply6`, `MultiplyBy0Y06` and `MultiplyByXY06` produce wide Fp6 results; `Reduce6` or `Reduce6Small` finishes them.

Expanded wide Fp2 multiplication writes its products into the final scratch buffers and uses `SubtractWideProducts` to combine them. For three distinct buffers holding t0 = a0b0, t1 = a1b1 and t2 = (a0+a1)(b0+b1), that helper writes t0-t1 and t2-t0-t1 while preserving t1. `WideTower.Multiply2` retains alias-safe raw multipliers even though its scratch is disjoint.

Expanded Fp6 squaring delays reduction until all six output base-field coordinates are formed, reducing twelve raw products six times. Its wide Fp2 squares use a modular real difference: each component lies in [0, 2p²), and is congruent to the required square. The Fp6 output C0/C1 components have magnitude below 8p² and use the small signed reducer, since 9p² < pR. C2 can reach 12p² < 3pR and uses the full signed reducer. Compact mode retains the narrow Fp6 square.

`FpFused` and `Fp2Fused` combine two canonical products before reduction. Base-field sums use two raw products and one positive reduction, with a value in [0, 2p²); differences have magnitude below p² and use the difference reducer. Complex sums/differences use six raw products and two reductions. Their real magnitudes and both difference components are below 2p². Complex sum imaginary values lie in [0, 4p²), so the positive reducer applies. All inputs are consumed before assigning the output, including when a scalar coefficient aliases it. These formulas are selected only in expanded mode by their algorithm callers; compact mode keeps separate products. Fused subtraction chains also preserve the original compact calls.

Interval bounds are 8p² for Fp6 products, 9p² for sparse products and 22p² for full Fp12 products. The first two fit the pR bound; the last fits 3pR < 2^767. The formulas reduce once per output base-field coordinate. Narrow inputs and wide output scratch buffers must be distinct. Any new lazy formula must establish its bounds before selecting a reducer. Fully lazy Fp4 and cyclotomic variants were slower and were discarded.

## Inlining and calls

Small single-field entry points, including `Fp.Multiply`, remain inlineable for isolated operations. Inlining the same carry chains into a large point, tower or square-root method can exceed JIT optimization budgets and increase register pressure.

`CurveField<T>` preserves isolated Fp arithmetic calls and lets generic Fp2 callers reach their already-isolated kernels directly. Expanded base-field squaring uses the concrete `FpFused.Square` helper; larger products use `Fp.MultiplyComposite`. Fp2 multiplication and square have inlineable policy dispatchers and isolated implementation kernels. Expanded point completion and Horner evaluation select fused product helpers, while compact mode keeps the original calls. `FpCurveRoots` specializes one shared fixed chain with an expanded square helper or compact equal-operand multiplication. Small coordinate moves and scalar logic remain inline. Calling policies must be measured on each target runtime; forcing public multiplication to be non-inlined did not improve both stock and patched runtimes.

The Fp Montgomery row loop processes two rows per iteration. Three-row and fully expanded Montgomery variants increased spills and were discarded. The experimental JIT bounds full-adder analysis at 8,192 nodes and 128 candidates, with 262,144 liveness-scan visits and 65,536 extended carry-use visits. This lets composite small-constant Fp2 operations keep their carries in flags. Large inline kernels still need independent benchmark methods to avoid spending allowances on unrelated cases. Raw multiplication benefits from full expansion behind a compact call. [Performance](performance.md#kernel-and-jit-details) records carry idioms and widening-product counts; the [JIT notes](../jit/README.md) describe lowering and stress coverage.

## Curve coordinates and subgroup checks

`Jacobian<T>` implements G1 over Fp and G2 over Fp2 through `IField<T>`. Coordinates represent x = X/Z² and y = Y/Z³. Addition, mixed addition, doubling, negation and equality handle infinity, equal points and inverse points. Equality avoids inversion; affine conversion needs one field inversion. Output operations tolerate aliases.

XYZZ buckets retain X, Y, ZZ = Z² and ZZZ = Z³. This saves work in repeated mixed additions and bucket integration. They support signed affine addition and convert back to Jacobian coordinates. Jacobian affine conversion returns coordinates directly when Z is already one, avoiding an inversion for normalized points and addition with infinity.

`IsOnCurve` checks y² = x³ + 4 for G1 and y² = x³ + 4(1 + u) for G2. `IsInSubgroup` uses Scott's endomorphism tests: the G1 relation uses the cube-root endomorphism and two multiplications by the curve parameter; G2 compares psi(P) with [z]P. Here z = -0xd201000000010000. Infinity passes both checks. Subgroup predicates assume an on-curve point, so callers must check the curve first.

## Scalar multiplication

Scalars contain exactly 32 little-endian bytes and are reduced modulo r. G1 uses a GLV split into 128-bit components and its endomorphism. G2 uses four base-|z| GLS digits and successive psi images. Both use joint width-5 wNAF. Only the original point's eight odd multiples are built and batch-normalized; the remaining tables are its affine endomorphism images. These decompositions require subgroup points.

## Multi-scalar multiplication

MSM accepts affine subgroup points and contiguous 32-byte little-endian scalars. Invalid scalar lengths throw `ArgumentException`; an empty input returns infinity. Infinity points contribute zero.

The n = 1 path calls scalar multiplication. G1 inputs with 2 ≤ n < 1,024 expand to 2n GLV points with 128-bit scalars. G2 inputs with n ≥ 2 expand to 4n GLS points with 64-bit scalars. G1 at n ≥ 1,024 retains the 256-bit path because its extra decomposition work did not consistently improve large batches. The expanded scalars are reduced first, and the unused bytes in their 32-byte slots are cleared. Window extraction includes a carry window above the highest scalar bit.

For fewer than 32 effective points, and for exactly 32 effective points after decomposition, signed width-4 tables are normalized with one batch inversion. Decomposed small MSMs build tables for the original points and derive the other tables with endomorphisms. Larger inputs use Pippenger with signed Booth windows. A bounded little-endian word read extracts digits. With l = floor(log2(effective point count)), the projective window width is l - 3 when l > 12, l - 2 when l > 8, l - 1 when l > 4, and 2 otherwise. The 128-bit path uses one smaller window for 128–256 effective points. XYZZ buckets accumulate signed points; descending running sums integrate the buckets.

Large inputs group points by signed digit and reduce each bucket in balanced affine-addition rounds. One Montgomery batch inversion supplies all slopes in a round. Repeated points use doubling, inverse points cancel, and infinity is skipped. Batch-affine accumulation uses max(2, projective window width - 1). The thresholds below count effective points after decomposition: split G1 begins at 256 original points, and expanded G2 begins at 32 original points.

A nonempty bucket can reduce to affine infinity. Its final conversion to XYZZ must keep ZZ/ZZZ zero; assigning one to those scales would create the invalid finite point (0,0). Cancellation regressions cover both groups, fully cancelled buckets and surviving points in other buckets.

| Group and runtime policy | Batch-affine threshold |
|---|---:|
| G1, either mode | n ≥ 512 |
| G2, expanded mode | n ≥ 128 |
| G2, compact mode | Disabled; XYZZ remains faster on stock .NET 10 |

The G2 threshold follows the same runtime policy and test overrides as the field kernels, including for a net10.0 assembly hosted by the patched runtime. Storage is bounded stack memory or pooled arrays. Warmed measurements allocate zero heap bytes; first pool use can allocate.

## Map-to-curve

`MapToCurve.MapToG1` and `MapToG2` accept Fp and Fp2 values. They apply optimized simplified SWU (SSWU), homogeneous isogenies to the BLS12-381 curves, and cofactor clearing. G1 uses the 11-isogeny and clearing by 1 - z; G2 uses the 3-isogeny and a psi-based clearing formula.

Fixed reciprocal-square-root addition chains and Fp2 root checks select the SSWU candidate and its sign. `FieldRoots` and `FpCurveRoots` keep these arithmetic chains separate from the larger map formulas. Constants are in `MapConstants1`, `MapConstants2` and `CurveConstants`; the generator reads the pinned optional blst sources.

The fixed root chain selects a concrete squaring helper in expanded mode and the original multiply-with-equal-operands calls in compact mode. Both execute the same exponent. G2 SSWU specializes its fixed -2-u, pure imaginary and equal-component constants. Root alignment by i uses a coordinate rotation/negation; alignment by c-ci uses two base-field products. When the first SSWU candidate is square, the unused second candidate needs no products, saving three Fp products in G1 or two Fp2 products in G2.

Isogenies generate only the powers needed by their polynomial degrees (four entries for G2, sixteen for G1). This removes twelve unused Fp2 multiplications from G2. Expanded Horner steps combine both products before reduction; monic leading terms omit multiplication by one. Expanded point completion also fuses product differences, saving one Montgomery reduction in G1 and two in G2. Native map comparisons and independent affine polynomial identities cover the projective formulas, including rescaled inputs and output aliases.

Each map returns a subgroup point. Mapping field elements does not hash messages. Hash-to-field, expand-message and message hash-to-curve with a domain separation tag (DST) remain open.

## Pairing

The public API separates the Miller loop from final exponentiation:

```csharp
public static void MillerLoop(ReadOnlySpan<G1Affine> p,
    ReadOnlySpan<G2Affine> q, out Fp12 f);
public static void FinalExponentiation(in Fp12 f, out Fp12 result);
public static bool PairingCheck(ReadOnlySpan<G1Affine> p,
    ReadOnlySpan<G2Affine> q);
```

Miller loops use blst's line formulas and negative-z schedule. Up to 64 active pairs share a pass with bounded stack storage and shared accumulator squarings. Scratch spans are sized to min(64, input count); larger inputs multiply the pass results. Infinity pairs are skipped. Sparse Fp12 multiplication evaluates line coefficients at each G1 point. Conjugation accounts for the negative curve parameter. Every line coordinate is assigned before use, without clearing the line buffer first.

Expanded batches with at least two active pairs combine adjacent lines first. Their product has five nonzero Fp2 coefficients (C1.C0 is zero); forming it and multiplying the accumulator uses 69 base-field products and 22 reductions, versus 78 and 24 for two separate sparse multiplications. The first two lines initialize the accumulator directly. Odd remaining lines use the ordinary sparse multiply. The paired loop trims its scratch span to the active count, including partially filled batches and infinity gaps. Compact mode retains the original loop. This optimization depends on batch size and applies to arbitrary line values.

Final exponentiation first uses inversion, conjugation and a Frobenius map to enter the cyclotomic subgroup. The hard part follows blst's fixed chain of powers of z and z/2. Runs of at least 15 squarings use Karabina compression (ePrint 2010/542, Theorems 3.1–3.2), retaining C0.C1, C0.C2, C1.C0 and C1.C2. Two Fp4 squares update them; one Fp2 inversion reconstructs the omitted coefficients at the end of each run. The zero and identity cases are handled explicitly. The internal repeated-square helper requires a cyclotomic input or zero and tolerates output/input aliasing; arbitrary Fp12 inputs must use ordinary squaring.

Compression replaces 235 of the fixed chain's 315 cyclotomic squares and performs ten reconstructions. It saves 1,248 logical Fp products, trading them for ten extra base-field inversions. The result matches blst's exact final exponentiation. The hard chain also runs when the easy part yields one.

Frobenius maps specialize multiplication by real, imaginary, unit and conjugate-pair constants. `PairingCheck` tests whether the final result is one and returns immediately when the Miller product is already one. Affine infinity subgroup checks also return immediately.

Mismatched span lengths throw `ArgumentException`. Empty or all-infinity loops return one, and their checks return true. Callers must validate curves and subgroups; pairing does not perform those checks.

## Source operation counts

These counts use expanded mode. A product is a logical six-limb Fp product, including one embedded in a CIOS call. Reductions count Montgomery reductions; modular add/subtract corrections and binary-GCD work are excluded. These are source-formula counts, not instruction counts.

| Operation | Managed products / reductions | blst products / reductions |
|---|---:|---:|
| Fp2 multiply | 3 / 2 | 3 / 2 |
| Fp2 square | 2 / 2 | 2 / 2 |
| Fp6 multiply | 18 / 6 | 18 / 6 |
| Fp6 square | 12 / 6 | 12 / 6 |
| Fp12 multiply | 54 / 12 | 54 / 12 |
| Fp12 square | 36 / 12 | 36 / 12 |
| One sparse multiply | 39 / 12 | 39 / 12 |
| Two adjacent sparse multiplies | 69 / 22 | 78 / 24 |
| Full cyclotomic square | 18 / 18 | 18 / 12 |
| Compressed cyclotomic square | 12 / 12 | — |
| Fp12 inverse, plus one base inversion | 97 / 52 | 97 / 52 |
| Miller doubling line, including G1 evaluation | 29 / 26 | 29 / 26 |
| Miller addition line, including G1 evaluation | 39 / 28 | 39 / 30 |
| Miller loop, 1 pair | 6,867 / 3,326 | 6,867 / 3,336 |
| Miller loop, 8 pairs | 37,125 / 20,940 | 39,585 / 21,564 |
| General final exponentiation | 6,485 / 4,940 | 7,733 / 4,308 |

General final exponentiation uses 11 managed base inversions versus one native inversion, including the easy part. Each nonzero-denominator Karabina reconstruction uses 19 products, 16 reductions and one base inversion. The fixed chain includes 35 full Fp12 multiplications and four Frobenius maps. Specialized p/p² maps use 14 products/reductions and the p³ map uses six; psi uses four.

The Miller schedule has 63 doubling lines, five addition lines and 62 shared Fp12 squarings. With eight active pairs, combining adjacent lines saves 2,460 products and 544 reductions; fused line-add completion saves another 80 reductions. Forming a line pair costs 18 products/10 reductions, followed by 51/12 for the accumulator multiplication.

Jacobian doubling uses 5S + 2M, projective addition 4S + 12M, and mixed addition 3S + 8M, with S/M in the coordinate field. Fused G2 completion brings projective addition to 30 Montgomery reductions and mixed addition to 20, versus 32 and 22 with separate products. Subgroup checks and cofactor chains retain their deterministic relations; no probabilistic batch validation is used.

## Variable-time scope

These APIs are intended for public inputs such as precompile arguments. Binary GCD, scalar recoding, table lookup, bucket selection and exceptional point handling depend on input values. The library provides no constant-time guarantee for secret data. Secret-key operations need dedicated paths and review before a BLS signing API can use them.

## Testing

Tests combine independent `BigInteger` field references, Fermat inversion, algebraic identities, edge inputs, alias checks and optional native blst differential comparisons. `BigInteger` and native calls occur only in test and benchmark code. Native tests compare affine coordinates, tower values, Miller loops and final exponentiation bit for bit.

Validation covers .NET 10.0.12, .NET 11 RC1 and the patched .NET 12.0.0-dev host. Both library targets pass on compatible runtimes, including forced compact and expanded modes, cold Fp2/square calls and optimized policy folding. The algorithm capture passed 82,198 curve checks and 1,260,011 pairing/tower checks per default runtime, plus 1,106 selected Nethermind integration tests on each runtime. The separate field capture passed 841,109 kernel checks per run across the runtime/target/mode matrix. Counts describe those runner revisions, rather than fixed suite totals; [JIT notes](../jit/README.md#regression-coverage) cover checked-JIT stress validation.

Curve coverage includes 2,000 random maps and 2,000 random single scalars per group, all 256 single-bit scalars, 0, 1, r - 1, r, r + 1 and 2^256 - 1. MSM tests cross the small-table and batch-affine thresholds and cover random sizes through n = 1,024, repeated points, cancellation and infinity. Identities include associativity, commutativity, coordinate rescaling and doubling distributivity.

Tower/pairing coverage includes 10,000 random inversions, 10,000 signed wide products and reductions, 2,500 random Fp6/Fp12 inputs, and 2,000 random G1/G2 pairs. Pairing batches cover 1–20 pairs and multiple passes through 130 pairs, including infinity at batch boundaries, bilinearity, true/false products and mismatched lengths. Root and wide-reduction tests cover boundary values and output aliases. Allocation checks cover warmed hot paths.

The console runners use `dotnet run`, not `dotnet test`. Without native blst, the curve and pairing runners print `SKIP` for native comparisons and continue managed checks. See the [build instructions](../README.md#build-and-test) and [roadmap](roadmap.md).
