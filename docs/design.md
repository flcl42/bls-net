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

Fp2, Fp6 and Fp12 inversion reduce through tower norms to base-field inversion. Fermat inversion supplies a test reference. The measured binary-GCD speedup over Fermat is about 27× on .NET 10, 19× on .NET 11 and 16× on the patched runtime. See [performance](performance.md) for the measurement conditions.

## Wide intermediates and lazy reduction

`FpWide` is a signed 768-bit value with twelve 64-bit limbs. `Fp.MultiplyWide` produces a raw product; `AddWide` and `SubtractWide` combine raw values. These intermediates are internal arithmetic values, not canonical Fp residues.

Let R = 2^384. `ReduceWide` requires |value| < 3pR. Its Montgomery quotient lies in (-3p, 4p); it adds 4p when negative, then reduces by 2p and p. `ReduceWideSmall` requires |value| < pR. Its quotient lies in (-p, 2p), so one sign correction and final reduction suffice. Both return canonical Montgomery values.

`WideTower.Multiply2` forms unreduced Fp2 products. For canonical inputs, the real component has magnitude below p² and the imaginary component lies in [0, 2p²). `Add2`, `Subtract2` and `NonResidue2` combine them. `Multiply6`, `MultiplyBy0Y06` and `MultiplyByXY06` produce wide Fp6 results; `Reduce6` or `Reduce6Small` finishes them.

Interval bounds are 8p² for Fp6 products, 9p² for sparse products and 22p² for full Fp12 products. The first two fit the pR bound; the last fits 3pR < 2^767. The formulas reduce once per output base-field coordinate. Narrow inputs and wide output scratch buffers must be distinct. Any new lazy formula must establish its bounds before selecting a reducer. Fully lazy Fp4 and cyclotomic variants were slower and were discarded.

## Inlining and calls

Small single-field entry points, including `Fp.Multiply`, remain inlineable for isolated operations. Inlining the same carry chains into a large point, tower or square-root method can exceed JIT optimization budgets and increase register pressure.

`CurveField<T>` isolates field operations used by generic point kernels with `NoInlining | AggressiveOptimization` wrappers. Fp2 arithmetic already has isolated entry points. Larger arithmetic formulas use `Fp.MultiplyComposite` to keep the Fp kernel compact. `FpCurveRoots` applies this pattern to the fixed square-root chain. Small coordinate moves and scalar logic can remain inline. Calling policies must be measured on each target runtime; forcing the public multiply to be non-inlined did not improve both stock and patched runtimes.

The Fp row loop processes two rows per iteration. A fully unrolled six-limb loop exceeds the experimental JIT's 2,048-node full-adder budget. The loop trades back-edge register moves for a method small enough to preserve carry chains. [Performance](performance.md#kernel-and-jit-details) describes the carry idioms and runtime patch.

## Curve coordinates and subgroup checks

`Jacobian<T>` implements G1 over Fp and G2 over Fp2 through `IField<T>`. Coordinates represent x = X/Z² and y = Y/Z³. Addition, mixed addition, doubling, negation and equality handle infinity, equal points and inverse points. Equality avoids inversion; affine conversion needs one field inversion. Output operations tolerate aliases.

XYZZ buckets retain X, Y, ZZ = Z² and ZZZ = Z³. This saves work in repeated mixed additions and bucket integration. They support signed affine addition and convert back to Jacobian coordinates.

`IsOnCurve` checks y² = x³ + 4 for G1 and y² = x³ + 4(1 + u) for G2. `IsInSubgroup` uses Scott's endomorphism tests: the G1 relation uses the cube-root endomorphism and two multiplications by the curve parameter; G2 compares psi(P) with [z]P. Here z = -0xd201000000010000. Infinity passes both checks. Subgroup predicates assume an on-curve point, so callers must check the curve first.

## Scalar multiplication

Scalars contain exactly 32 little-endian bytes and are reduced modulo r. G1 uses a GLV split into 128-bit components and its endomorphism. G2 uses four base-|z| GLS digits and successive psi images. Both use joint width-5 wNAF with batch-normalized precomputation tables. These decompositions require subgroup points.

## Multi-scalar multiplication

MSM accepts affine subgroup points and contiguous 32-byte little-endian scalars. Invalid scalar lengths throw `ArgumentException`; an empty input returns infinity. Infinity points contribute zero.

The n = 1 path calls scalar multiplication. For 2 ≤ n < 32, signed width-4 tables are normalized with one batch inversion. Larger inputs use Pippenger with signed Booth windows. A bounded little-endian word read extracts digits. With l = floor(log2(n)), the projective window width is l - 3 when l > 12, l - 2 when l > 8, l - 1 when l > 4, and 2 otherwise. XYZZ buckets accumulate signed points; descending running sums integrate the buckets.

Large inputs group points by signed digit and reduce each bucket in balanced affine-addition rounds. One Montgomery batch inversion supplies all slopes in a round. Repeated points use doubling, inverse points cancel, and infinity is skipped. Batch-affine accumulation uses max(2, projective window width - 1).

| Group and target assembly | Batch-affine threshold |
|---|---:|
| G1, net10.0 or net11.0 | n ≥ 512 |
| G2, net11.0 | n ≥ 128 |
| G2, net10.0 | Disabled; XYZZ remains faster in the measured runs |

The G2 choice is compiled for the target framework; it is not selected by inspecting the running JIT. A patched runtime executing the net11.0 assembly therefore uses the net11.0 threshold. Storage is bounded stack memory or pooled arrays. Warmed measurements allocate zero heap bytes; first pool use can allocate.

## Map-to-curve

`MapToCurve.MapToG1` and `MapToG2` accept Fp and Fp2 values. They apply optimized simplified SWU (SSWU), homogeneous isogenies to the BLS12-381 curves, and cofactor clearing. G1 uses the 11-isogeny and clearing by 1 - z; G2 uses the 3-isogeny and a psi-based clearing formula.

Fixed reciprocal-square-root addition chains and Fp2 root checks select the SSWU candidate and its sign. `FieldRoots` and `FpCurveRoots` keep these arithmetic chains separate from the larger map formulas. Constants are in `MapConstants1`, `MapConstants2` and `CurveConstants`; the generator reads the pinned optional blst sources.

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

Miller loops use blst's line formulas and negative-z schedule. Up to 16 active pairs share a pass with bounded stack storage and shared accumulator squarings. Larger inputs multiply the pass results. Infinity pairs are skipped. Sparse Fp12 multiplication evaluates line coefficients at each G1 point. Conjugation accounts for the negative curve parameter.

Final exponentiation first uses inversion, conjugation and a Frobenius map to enter the cyclotomic subgroup. The hard part follows blst's fixed chain of powers of z and z/2, cyclotomic squarings, conjugations and Frobenius maps. `PairingCheck` tests whether the result is one.

Mismatched span lengths throw `ArgumentException`. Empty or all-infinity loops return one, and their checks return true. Callers must validate curves and subgroups; pairing does not perform those checks.

## Variable-time scope

These APIs are intended for public inputs such as precompile arguments. Binary GCD, scalar recoding, table lookup, bucket selection and exceptional point handling depend on input values. The library provides no constant-time guarantee for secret data. Secret-key operations need dedicated paths and review before a BLS signing API can use them.

## Testing

Tests combine independent `BigInteger` field references, Fermat inversion, algebraic identities, edge inputs, alias checks and optional native blst differential comparisons. `BigInteger` and native calls occur only in test and benchmark code. Native tests compare affine coordinates, tower values, Miller loops and final exponentiation bit for bit.

The latest arithmetic/curve implementation reports recorded 74,944 curve checks and 578,137 pairing/tower assertions passing on .NET 10.0.12, .NET 11 RC1 and the patched .NET 12.0.0-dev runtime. Later layout verification recorded 74,860 curve checks and 621,568 pairing checks with native blst, plus 2,100 curve checks and 497,265 pairing checks without it. These are counts from different runner revisions, not fixed suite totals. The Nethermind integration recorded 2,177 selected EIP-2537 tests passing.

Curve coverage includes 2,000 random maps and 2,000 random single scalars per group, all 256 single-bit scalars, 0, 1, r - 1, r, r + 1 and 2^256 - 1. MSM tests cross the small-table and batch-affine thresholds and cover random sizes through n = 1,024, repeated points, cancellation and infinity. Identities include associativity, commutativity, coordinate rescaling and doubling distributivity.

Tower/pairing coverage includes 10,000 random inversions, 10,000 signed wide products and reductions, 2,500 random Fp6/Fp12 inputs, and 2,000 random G1/G2 pairs. Pairing batches cover 1–20 pairs and multiple passes through 130 pairs, including infinity at batch boundaries, bilinearity, true/false products and mismatched lengths. Root and wide-reduction tests cover boundary values and output aliases. Allocation checks cover warmed hot paths.

The console runners use `dotnet run`, not `dotnet test`. Without native blst, the curve and pairing runners print `SKIP` for native comparisons and continue managed checks. See the [build instructions](../README.md#build-and-test) and [roadmap](roadmap.md).
