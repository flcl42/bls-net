# Design

The library targets net10.0 and net11.0. Production arithmetic uses fixed-width value types, spans and limb operations, with no BigInteger or native dependency. Original APIs are variable-time for public inputs; explicit constant-time alternatives are described below.

## Layout and contracts

| Namespace | Contents |
|---|---|
| Arithmetic | Fp/Fr, Fp2/Fp6/Fp12, inversion, bounded wide intermediates and fused helpers |
| Curves | Affine/projective G1/G2, generic formulas, subgroup checks, scalar multiplication and MSM |
| Hashing | Field roots, SSWU, isogenies and cofactor clearing |
| Pairing | Miller loops, final exponentiation and pairing checks |

One console test project contains arithmetic, kernels, tower/pairing and curves/maps/MSM sections. One benchmark project contains all timing loops. They compile shared native interop and a single loader from `shared/`. Optional `external/blst` supplies the differential reference; outputs go under ignored `artifacts/`.

Field arithmetic generally uses `out result, in input...`, allowing output/input aliases. Curve and pairing APIs put the output last. Fp is a 48-byte canonical Montgomery residue in six little-endian 64-bit limbs (R = 2^384); Fr uses four limbs and R = 2^256. Public points and tower coefficients follow blst's memory layout, verified by differential tests. This does not imply wire-format compatibility.

Fp parsing accepts exactly 48 canonical big-endian bytes and rejects values at or above p. Raw G1 coordinates are X,Y; raw G2 coordinates are X.C0,X.C1,Y.C0,Y.C1. Affine infinity is all zero; projective infinity has Z = 0. Scalars are exactly 32 little-endian bytes per point, reduced modulo r. EIP-2537 padded fields and big-endian scalars need caller conversion.

Check the curve before checking a subgroup. Scalar multiplication and MSM require subgroup points; pairing callers must validate curves and subgroups. Empty MSM returns infinity; empty/all-infinity pairings return one/true. Mismatched spans and invalid scalar lengths throw ArgumentException. Map APIs accept field elements and return subgroup points; they do not hash messages.

## Fields and extension tower

Multiplication uses two-row CIOS with final conditional reduction. Negation expresses p-a as p+~a+1, exposing carry chains and preserving zero with a mask. Fp inversion uses variable-time binary GCD: 31-bit top/bottom approximations form signed 2×2 matrices, followed by exact six-limb updates and modular coefficient corrections. Zero inverts to zero. Tower inversions reduce through norms to Fp inversion; Fermat inversion is an independent test reference.

| Type | Definition | Coefficient order | Bytes |
|---|---|---|---:|
| Fp2 | Fp[u]/(u²+1) | C0+C1u | 96 |
| Fp6 | Fp2[v]/(v³-(1+u)) | C0+C1v+C2v² | 288 |
| Fp12 | Fp6[w]/(w²-v) | C0+C1w | 576 |

Fp6 sparse helpers multiply by v, 0+yv or x+yv; `MultiplyByXY0` ignores the second input's C2. Fp12 sparse products pack coefficients into positions 0,1,4. Frobenius maps support powers 1–3. CyclotomicSquare matches blst's formula on arbitrary inputs, but equals ordinary square only in the cyclotomic subgroup.

## Runtime kernel policy

`Fp.UseExpandedKernels` is static readonly and follows the executing runtime, independently of the target framework: compact on .NET 10, expanded on .NET 11 and later. Small inlineable dispatchers select isolated optimized kernels. After field initialization, optimized callers fold the policy and remove the branch; cold dispatch also initializes policy before compiling a kernel.

Set AppContext switch `Nethermind.Crypto.BlsNet.UseExpandedKernels` before the first field use, or environment variable `BLSNET_EXPANDED_KERNELS` before process startup (`0`/`1`, or case-insensitive `false`/`true`). AppContext takes precedence; invalid environment values use the runtime default. Selection is fixed for the process. CLI `--compact-kernels`/`--expanded-kernels` switches exercise both formulas; expanded mode is deliberately slower on stock .NET 10.

Expanded symmetric Fp square computes 15 cross products once, doubles them, adds six diagonal products and performs six reduction rows: 57 widening products versus 72 for CIOS. Canonical inputs give a raw square below p² < pR and quotient below 2p. The bit-carry chain and delayed output preserve aliases. Compact square multiplies equal operands with CIOS.

Small public field operations remain inlineable. Composite callers isolate carry-heavy arithmetic to avoid JIT analysis limits and register pressure. `CurveField<T>` isolates Fp calls while Fp2 reaches its isolated kernels. `FpFused.Square` gives isolated squares and point formulas a concrete helper. `SquareNThenMultiply` keeps six scalar limbs across repeated squares and their final CIOS product, with no square calls or canonical correction inside the loop. For inputs below 2p, each raw square is below 4p² < pR, so REDC stays below 2p inductively; the final multiplication returns a canonical value. Sqrt/Fp2-root and Fermat reciprocal chains share this kernel under either policy. Its modulus rows use fixed memory operands and two rows per iteration. Full-word multiplication carries and one-bit addition carries stay in separate locals for the JIT proof. Some inline/non-inline helpers deliberately share formulas because call boundaries change generated code.

Stock RyuJIT materializes carries. [dotnet/runtime PR #134039](https://github.com/dotnet/runtime/pull/134039), head `0a303c9` or later, includes carry-aware lowering that retains them in flags. It also handles flag-preserving complements, multiply/add chains and conditional selections. The current fused-map results also use the bounded [JIT follow-up](../jit/README.md): independent row matching, row-local multiplier copies and carry forwarding after final-limb search exhaustion. See [performance](performance.md#reproduction).

## Wide intermediates and bounds

FpWide is a signed twelve-limb, 768-bit intermediate, not a canonical field value. Expanded raw multiplication unrolls six rows. Reduction cancels the low 384 bits before adding the signed high half, avoiding a carry between rows. Compact mode keeps loops because expansion spills on stock .NET 10.

For R = 2^384, `ReduceWide` requires |x| < 3pR: its quotient lies in (-3p,4p), corrected by adding 4p when negative and reducing by 2p and p. `ReduceWideSmall` requires |x| < pR, giving (-p,2p), with one sign correction and final reduction. `ReduceWidePositive` requires 0 ≤ x < pR and skips sign correction in expanded mode. All return canonical values.

Expanded Fp2 multiplication uses three raw products and two reductions. Unreduced sums are below 2p; the real component has magnitude below p² and the imaginary lies in [0,2p²). The difference reducer avoids a twelve-word scratch round trip. `MultiplyWideNoAlias` requires output disjoint from both inputs; ordinary MultiplyWide preserves output overlap. All three Fp2 products finish before writing coordinates. Compact Fp2 multiplication keeps three CIOS calls. Square uses two CIOS products with policy-specific call boundaries. Expanded square keeps both in one kernel and uses a biased difference a+p-b in [0,2p), avoiding a canonical subtraction before multiplication. The product of its two unreduced factors is below 4p² < pR.

WideTower combines raw Fp2/Fp6 products before final reduction. Product bounds are 8p² for Fp6, 9p² for sparse Fp12 and 22p² for full Fp12; the first two fit pR and the last fits 3pR < 2^767. Expanded Fp6 square uses twelve raw products and six reductions. Its first two Fp2 components fit 8p²; C2 can reach 12p² and needs the full reducer. Narrow inputs and wide scratch must be distinct.

FpFused/Fp2Fused combine canonical product sums/differences before reduction. Base sums are in [0,2p²), differences have magnitude below p². Complex differences and real sums have magnitude below 2p²; imaginary sums lie in [0,4p²). Inputs are consumed before output assignment, including scalar-coefficient aliases. New lazy formulas must establish their bounds before choosing a reducer. Multiplication by eight in the doubling helper shifts a canonical value (8p < R), then conditionally subtracts 4p, 2p and p; no intermediate canonical doubles are needed.

## Curves, scalars and MSM

Generic Jacobian formulas specialize to Fp/Fp2 through IField<T>; its public contract remains stable. Fixed cofactor doubling runs retain local coordinates and split preparation/completion into bounded kernels, inlining linear arithmetic while keeping products isolated. G1 uses 4S+3M, G2 retains 5S+2M; both produce the same Jacobian coordinates as ordinary doubling. Other field types retain the generic loop. XYZZ buckets cache Z²/Z³. CurveConstants contains the endomorphism and subgroup constants. Scott subgroup checks and deterministic cofactor relations avoid probabilistic validation.

G1 scalar multiplication uses a GLV split into two 128-bit components; G2 uses four base-|z| GLS digits and psi images. Joint width-5 wNAF builds eight odd multiples only for the original point, batch-normalizes them, then derives endomorphism images.

MSM uses the single-multiply path at n=1. G1 splits inputs with 2 ≤ n < 1,024 into 2n effective points with 128-bit scalars; larger G1 inputs keep 256-bit scalars. G2 with n ≥ 2 splits into 4n effective points with 64-bit scalars. Reduced scalar slots clear unused bytes; Booth extraction includes a top carry window.

Up to 32 effective points use signed width-4 tables and one batch inversion. Larger MSMs use Pippenger with signed Booth windows and XYZZ buckets. If l=floor(log2(effective points)), projective width is l-3 for l>12, l-2 for l>8, l-1 for l>4, otherwise 2. The 128-bit path uses one smaller window for 128–256 effective points.

Batch-affine buckets group by digit and add in balanced rounds with one batch inversion per round, using width max(2,projective width-1). Thresholds count effective points: G1 starts at 512 in either mode; expanded G2 starts at 128, compact G2 retains XYZZ. Repeated points double, inverse points cancel and infinity is skipped. A cancelled bucket must retain zero ZZ/ZZZ when converted back to XYZZ. Storage is bounded stack or pooled arrays; initial pool growth may allocate.

## Maps and pairing

Maps apply optimized SSWU, homogeneous 11-isogeny (G1) or 3-isogeny (G2), then cofactor clearing by 1-z or a psi formula. Fixed reciprocal-square-root chains select candidates and sign; G2 specializes pure-imaginary/equal constants. Isogenies compute only needed powers (16 for G1, four for G2). Expanded Horner and point completion fuse product differences. Native comparisons and independent affine polynomial identities cover rescaled inputs and aliases. Cofactor clearing uses psi²(X,Y,Z)=(beta*X,-Y,Z). Secret maps omit doubling candidates only at additions proven distinct by group-order gcds or Frobenius norms, retaining masked infinity handling. The complete formula remains at the possible G1 3-/11-torsion and G2 13-torsion doublings. The [proof and full-curve tests](cofactor-schedule.md) do not assume prime-subgroup inputs.

Miller loops use blst's line formulas and negative-z schedule. Up to 64 active pairs share a pass and accumulator squarings; larger inputs multiply pass results. Scratch is sized to min(64,input count). Infinity pairs are skipped. Expanded batches combine adjacent lines into a five-coefficient product: 69 Fp products/22 reductions versus 78/24 for separate sparse products. The first line pair initializes the accumulator; odd lines use ordinary sparse multiplication. Compact mode retains separate lines.

Final exponentiation uses inversion, conjugation and Frobenius for the easy part, followed by blst's fixed z/z÷2 chain. Runs of at least 15 squarings use Karabina compression (ePrint 2010/542, Theorems 3.1–3.2), keeping four Fp2 coordinates. Two Fp4 squares update them; one Fp2 inversion reconstructs omitted coordinates. Zero/identity and aliases are explicit. Compression replaces 235 of 315 squares with ten reconstructions, saving 1,248 logical Fp products at the cost of ten base inversions. Repeated cyclotomic square requires cyclotomic input or zero. PairingCheck returns immediately if the Miller product is one.

## Constant-time APIs

The following paths treat field values, scalar bytes, point coordinates, infinity status, equality results and every intermediate derived from them as secret. The point supplied to multiplication may also be secret. Span lengths, kernel policy, type specialization, curve constants, fixed exponents and window/loop counters are public. Scalar multiplication still requires an on-curve subgroup point; validation is the caller's responsibility and the existing validation methods are variable-time.

| API | Contract |
|---|---|
| `Fp.InverseConstantTime`, `Fp2.InverseConstantTime`, `Fr.InverseConstantTime` | Fixed Fermat chains; inverse of zero is zero; output may alias input. Fp uses blst's 460-operation `recip-addchain.h` chain, Fp2 inverts its norm, Fr uses a fixed base-16 r-2 chain. |
| `Fr.FromLittleEndianConstantTime`, `ToLittleEndianConstantTime` | Reduce exactly 32 secret little-endian bytes modulo r, or serialize 32 canonical little-endian bytes. Two trial subtractions suffice because 2^256 < 3r. Length errors depend only on public lengths. |
| `Fp` / `Fp2` / `Fr`: `IsZeroMask`, `EqualMask`, `Select`, `ConditionalNegate`, `ConditionalSwap` | Masks are `ulong.MaxValue` for true and zero for false. A select takes the set operand first. Callers must supply one of those two masks. Operands and masks may all be secret; aliases, including swapping the same reference, are supported. |
| `Fp` / `Fp2`: `SqrtConstantTime`, `SignMask` | Return a square-status mask or RFC 9380 sign mask. Square roots match the existing helpers, including their output for nonsquares; only use the root when the status mask is all ones. Zero is a square. |
| `G1Projective` / `G2Projective`: `MultiplyConstantTime` | Affine and projective overloads; exactly 32 little-endian bytes, reduced modulo r first. Secret base point and scalar, fixed GLV/GLS split, width-five Booth windows, full-scan tables and select-based exceptional handling. Infinity and scalar zero are supported. Projective output may alias input. |
| `G1Projective` / `G2Projective`: `ToAffineConstantTime`, `AddConstantTime`, `AddAffineConstantTime`, `NegateConstantTime` | Secret points throughout; affine infinity is (0,0), projective infinity has Z=0. Additions cover equal, inverse and infinity operands using blst's dadd/dadd_affine formulas. Conversion always performs the fixed inversion, even for Z=0 or Z=1. |
| Affine and projective G1/G2: `IsInfinityMask`, `EqualMask`, `Select`, `ConditionalNegate`, `ConditionalSwap` | Secret operands and mask; projective equality uses cross products and handles any Z=0 representation. Affine infinity is preserved by negation. |
| `MapToCurve.MapToG1ConstantTime`, `MapToG2ConstantTime` | Secret Fp/Fp2 input; same SSWU, isogeny and cofactor map as the original APIs and blst's single-input maps. Candidate roots, denominator exceptions and signs use masks. These methods do not hash a message. |

The scalar split uses fixed 256-step binary division with explicit limb borrows instead of blst's assembly reciprocal-division kernels. G1 produces two 128-bit components with the existing beta-squared/negation endomorphism; G2 produces four base-|z| components with successive -psi images. The window loop includes the top carry window and scans all 16 table entries even for a zero digit. Tables remain projective, avoiding inversions and secret-dependent normalization. Existing doubling formulas already handle Z=0 algebraically. Scalar reduction and splitting use no division on secret data or variable-time recoding. Some runtime Tier0 versions divide public window counts and type sizes; the generated-code audit classifies those operands explicitly.

The Fermat reciprocal remains the baseline. A faster Bernstein–Yang/safegcd implementation and generator tables are deferred until a separately verified implementation demonstrates a clear benefit. The existing binary-GCD reciprocal is not used for secrets.

Verification combines BigInteger scalar/inverse references, comparison with the original APIs, optional native blst comparisons, aliases, exceptional point cases, scalar boundaries and every single-bit scalar, and thousands of random field/map/point inputs under both kernel policies. [Constant-time validation](constant-time-validation.md) records the runtime matrix, generated-code audit and opt-in Welch timing experiments. The audit inventories conditional jumps, direct callees, memory-address instructions and variable-latency instruction candidates; manual data-flow review is necessary. A timing experiment that fails to detect leakage is evidence for that capture, not a proof.

Only the recorded Windows x64 runtime/JIT configurations have been audited. JIT-generated code is audited, not formally verified; repeat the audit after runtime, architecture, compiler settings or relevant source changes. These APIs do not protect against power/EM side channels, memory disclosure or physical attacks, and do not promise to erase every JIT/register/stack copy. They provide building blocks for a later signing API, which still needs a security review, message hashing, encodings and protocol-level validation. The original variable-time APIs and MSM retain their public-input contracts.

## Verification and limits

BigInteger references, Fermat inversion, signed-wide bounds, algebraic identities, canonical parsing, aliases and warmed allocation checks complement native comparisons. Native coverage includes field/tower results, 2,000 maps and single scalars per group, scalar edges and all single-bit scalars, MSM through 1,024 points, and 2,000 pairing inputs. Fp6 is checked algebraically and through Fp12, without custom native exports. Pairing tests cross the 64-pair batch boundary through 130 pairs with infinity gaps, bilinearity and true/false products. Console options also check cold initialization and optimized policy folding.

The original binary GCD, recoding, table/bucket selection and exceptional point handling depend on inputs. Use the explicit constant-time alternatives for secrets within their audited configurations; a complete secret-key/signature API still needs security review. See [README](../README.md#build-and-test) for commands and [roadmap](roadmap.md) for remaining APIs and platform work.
