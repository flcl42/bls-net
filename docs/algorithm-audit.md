# Algorithm and operation-count audit

This audit compares the Agent G implementation with pinned blst
`f62244ef50ad1a603decdb8f215e982d2a467bb6`, with `__FP2x2__` enabled.
Counts describe source formulas, not CPU instructions. An Fp product means
one six-limb raw product, including products embedded in a Montgomery call.
The ADX `sqr_382x` uses two raw Fp products, just like the managed complex-square
formula; it does not use three separate base-field squares. Reductions count
Montgomery reductions, excluding conditional modular corrections on additions.
Binary-GCD inversion's internal work is excluded and its calls are listed separately.

## Tower and pairing counts

| Operation | Fp products, managed | Fp reductions, managed | Fp products, blst | Fp reductions, blst |
|---|---:|---:|---:|---:|
| Fp2 multiply, narrow | 3 | 3 | 3 | 2 |
| Fp2 square, narrow | 2 | 2 | 2 | 2 |
| Fp6 multiply | 18 | 6 | 18 | 6 |
| Fp6 square | 12 | 12 | 12 | 6 |
| Fp12 multiply | 54 | 12 | 54 | 12 |
| Fp12 square | 36 | 12 | 36 | 12 |
| Fp12 sparse multiply | 39 | 12 | 39 | 12 |
| Fp4 square in cyclotomic formula | 6 | 6 | 6 | 4 |
| Full cyclotomic square | 18 | 18 | 18 | 12 |
| Compressed cyclotomic square, four retained coefficients | 12 | 12 | — | — |
| Karabina reconstruction, nonzero denominator | 19 + 1 inversion | 19 | — | — |
| Fp12 inverse | 97 + 1 inversion | 73 | 97 + 1 inversion | 52 |
| Fp12 Frobenius p or p², before | 19 | 19 | 19 | 14 |
| Fp12 Frobenius p or p², after | 14 | 14 | 19 | 14 |
| Fp12 Frobenius p³, after | 6 | 6 | 19 | 14 |
| psi, before | 6 | 6 | 6 | 4 |
| psi, after | 4 | 4 | 6 | 4 |
| Miller doubling line, including G1 evaluation | 29 | 29 | 29 | 26 |
| Miller addition line, including G1 evaluation | 39 | 39 | 39 | 30 |

Fp6 multiplication has six wide Fp2 products. Sparse Fp12 multiplication has
5 + 3 + 5 wide Fp2 products; full multiplication has 6 + 6 + 6. Both already
match blst's product and final-reduction counts. The remaining difference is
in the wide kernels, signed corrections, scratch traffic and call boundaries.

For an active single pairing, the schedule has 63 doubling lines, five addition
lines, 62 Fp12 squares and 67 sparse multiplications (the first line initializes
the accumulator). That is **6,867 Fp products** in both implementations,
**3,570 managed reductions** versus **3,336 blst reductions**. For eight active
pairs sharing the squarings, the corresponding counts are **39,585 products**,
**23,436 managed reductions** and **21,564 blst reductions**. Infinity pairs
are omitted before these counts. Larger managed batches now share one pass
through up to 64 active pairs, rather than 16.

The fixed final-exponentiation chain retains 35 full Fp12 multiplications
(including the easy part), four Frobenius maps, one Fp12 inverse and 315
cyclotomic squarings. Compression replaces 235 of those squares with the
four-coordinate formula and performs ten reconstructions. Excluding the
base-field inversions themselves:

| Final exponentiation | Fp products | Fp reductions | Base-field inversions |
|---|---:|---:|---:|
| Managed before | 7,733 | 6,239 | 1 |
| Managed after | 6,485 | 4,991 | 11 |
| blst | 7,733 | 4,308 | 1 |

This saves 1,248 products and reductions, trading for ten binary-GCD
inversions. It retains blst's exact final-exponentiation convention and output,
rather than substituting another power that only preserves pairing checks.

## Additions and corrections

An ordinary Fp2 multiplication uses two unreduced Fp sums and three Fp
subtractions. A wide Fp2 multiplication has those same two sums and three
double-width subtractions. A narrow Fp2 square uses two unreduced sums and
one modular difference; blst's wide square uses one sum, one difference and a
double-width doubling instead of doubling a narrow input.

Expanding all tower helper calls gives the following counts. N is a narrow
Fp addition/subtraction; W is a signed double-width addition/subtraction.
Negations and multiplication by 3/4/8 are separate operations, not expanded
into a fictitious number of modular additions.

| Managed formula | Narrow adds | Narrow subtracts | Wide adds | Wide subtracts |
|---|---:|---:|---:|---:|
| Fp6 multiply | 24 | 0 | 8 | 32 |
| Fp12 multiply | 84 | 0 | 31 | 109 |
| Fp12 sparse multiply | 42 | 0 | 18 | 63 |
| Full cyclotomic square | 64 | 31 | 0 | 0 |
| Compressed square | 43 | 21 | 0 | 0 |

The wide tower's algebraic add/subtract counts match blst; blst applies
high-half modular corrections where the managed code carries signed bounds
until reduction. These corrections are not equivalent instruction sequences.
The line-doubling formula adds four Fp2 values, subtracts eleven and scales
three by small constants. Line addition adds five Fp2 values and subtracts
eleven. The line formulas, `line_by_Px2` scaling and negative-z conjugation
are the same as blst. Redundant clearing of all 288 line bytes was removed.

## Curves, maps and MSM

For a = 0, managed Jacobian doubling uses 5S + 2M; ordinary projective addition
uses 4S + 12M and mixed addition 3S + 8M, including the completion formula.
The managed variable-time exceptional branches avoid blst's unified
addition/doubling overhead; the formulas do not offer a missing multiplication
that could explain the remaining G2 doubling/subgroup gap. Fp2 multiplication
still performs three reductions in this clone, whereas blst performs two.
Wide coordinate multiply/subtract variants would require a measured fused
kernel to make their scratch traffic worthwhile.

The subgroup relations and cofactor chains match blst: G2 subgroup checking
uses psi(P) = [z]P; G2 clearing uses two fixed |z| chains, three psi applications,
one initial doubling and five projective additions. G1 clearing uses [1-z]P.
The map reciprocal-root addition chains were retained. The unused second SSWU
candidate now costs zero multiplications when the first candidate is square:
three Fp multiplies saved for G1, two Fp2 multiplies for G2. Root alignment by
i is a coordinate rotation/negation, and alignment by c-ci uses two Fp
products. Canonical decoding remains in Agent F's field scope; the integration
removes its duplicate range comparison and skips Montgomery conversion of zero.

The MSM gains come from reducing accumulation width, rather than changing the
curve formulas: GLV makes 2n points with 128-bit digits; GLS makes 4n points with
64-bit digits. Small tables normalize the original points' multiples once and
derive the others with endomorphisms. Larger buckets retain the existing
balanced affine rounds and XYZZ integration, with thresholds measured against
the effective point count. G1 at n >= 1,024 keeps the original path. G2 .NET 10
continues using XYZZ; .NET 11 uses batch-affine accumulation from 32 original
points after expansion. No runtime-specific arithmetic selector was introduced.

## Rejected experiments and remaining work

The wide Fp4 square matched native results but increased patched cyclotomic
squaring from about 699 ns to 770 ns. It was removed. Blanket curve-wrapper
inlining did not improve G2 and was removed. Small expanded MSMs without
endomorphism-derived tables regressed; deriving the tables fixed that cost.
Adjacent G1 windows and expanded small-table thresholds were measured; the
final dispatch retains the useful sizes only.

This clone does not achieve universal blst parity. Pairing's remaining gap
tracks wide Fp2 kernel/correction traffic, early Fp4/Fp6 reductions and point
doubling costs. Stock .NET still materializes carry chains. Agent F's reserved
Fp/Fp2/JIT files were not edited, and its eventual changes are not included in
the reported measurements. A fused wide Fp2 square/product helper is the next
place to revisit the rejected widened Fp4 implementation. Further hard-part
chain or torus changes need an independently verified exponent and a measured
benefit, not just a lower abstract multiplication count.

Karabina's formulas: [ePrint 2010/542](https://eprint.iacr.org/2010/542).
The local blst sources used for this audit are `fp12_tower.c`, `pairing.c`,
`ec_ops.h`, `map_to_g2.c`, `multi_scalar.c`, `recip.c` and the ADX
`mulx_mont_384-x86_64.pl` generator.
