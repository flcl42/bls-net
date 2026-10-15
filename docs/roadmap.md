# Roadmap

Nethermind.Crypto.BlsNet is a pure C# BLS12-381 library for public-input operations. The library targets .NET 10 and .NET 11. Release packaging and broader platform validation remain open.

## Implemented

| Layer | Scope |
|---|---|
| Base fields | Fixed-width Fp/Fr arithmetic, canonical parsing, raw serialization and inversion |
| Extension tower | Fp2/Fp6/Fp12, bounded wide intermediates, sparse products, Frobenius and cyclotomic helpers |
| Curves | Affine and Jacobian G1/G2, XYZZ buckets, addition/doubling and Scott subgroup checks |
| Scalar multiplication | Scalar reduction, G1 GLV, G2 GLS and joint wNAF |
| MSM | Small signed tables, Pippenger, Booth windows and batch-affine buckets at measured thresholds |
| Map-to-curve | SSWU, isogenies, square-root chains and cofactor clearing from Fp/Fp2 |
| Pairing | Batched Miller loops, final exponentiation and product checks |
| Validation | BigInteger references, algebraic identities, edge/alias/allocation tests and optional native blst comparisons |

EIP-2537 integration and BenchmarkDotNet precompile comparisons exist in a Nethermind branch that is not published yet. With the experimental carry-aware JIT, precompile parity has been reached and exceeded: every measured precompile takes less time than blst, with ratios of 0.50–0.93x. Variable-time scalar algorithms, endomorphism-split MSM and fused arithmetic contribute to the lead. Some individual components still trail native. The stock-JIT gap remains the main open performance item: stock .NET 10 takes about 1.5–3.8x blst's time in the condensed table. See [performance](performance.md) and [design](design.md).

## Open work

| Item | Required work |
|---|---|
| Hash-to-field and message hash-to-curve | Expand-message XMD SHA-256, hash-to-field, DST handling and upstream hash-to-curve vectors. Existing map APIs accept field elements only. |
| BLS signature API | Key generation, signing, verification, aggregation and min-pk/min-sig variants, with explicit validation contracts. |
| Constant-time secret-data paths | Secret scalar operations and inversion, removal of input-dependent branches and lookups where required, and security review before secret-key support. |
| blst-compatible serialization | Compressed and uncompressed point formats, canonical decoding, infinity/sign rules and precise format errors. Raw field/coordinate APIs already exist. |
| Stock-JIT performance | Main open performance item. Improve carry lowering and arithmetic calling patterns without requiring a custom runtime. Preserve compact .NET 10 fallbacks and measure .NET 10 and .NET 11 separately. |
| CI | Automated builds for both target frameworks, managed tests, native differential jobs and reproducible platform checks. |
| Packaging and publishing | Package metadata review, repository URL, version/release policy, publication and release validation. Development package generation already works. |
| ARM64 and broader platforms | Validate arithmetic, JIT behavior and performance on ARM64 and additional operating systems. The native reference build script currently targets Windows x64. |

## Engineering constraints

Keep production arithmetic fixed-width and free of `BigInteger` and native calls. Use `in`, `out`, spans and bounded stack or pooled storage for hot paths. Reject noncanonical coordinates and document byte order. Establish bounds for every wide formula. Validate changes against independent references and optional native blst, then remeasure affected operations. Historical timings describe specific builds and hardware; they are not performance guarantees.
