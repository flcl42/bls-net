# Roadmap

Nethermind.Crypto.BlsNet is a pure C# BLS12-381 library for public-input operations. The library targets .NET 10 and .NET 11. Release packaging remains open.

## Implemented

| Layer | Scope |
|---|---|
| Base fields | Fixed-width Fp/Fr arithmetic, canonical parsing, raw serialization and inversion |
| Extension tower | Fp2/Fp6/Fp12, bounded wide intermediates, sparse products, Frobenius and cyclotomic helpers |
| Curves | Affine and Jacobian G1/G2, XYZZ buckets, addition/doubling and Scott subgroup checks |
| Scalar multiplication | Scalar reduction, G1 GLV, G2 GLS and joint wNAF |
| MSM | Small signed tables, Pippenger, Booth windows and batch-affine buckets at measured thresholds |
| Map-to-curve | SSWU, isogenies, square-root chains and cofactor clearing from Fp/Fp2 |
| Constant-time building blocks | Fp/Fp2/Fr inversion and mask helpers, secret scalar reduction/serialization, GLV/GLS multiplication, point helpers and maps; recorded x64 JIT audits/timing experiments and stock ARM64 code review |
| Pairing | Batched Miller loops, final exponentiation and product checks |
| Validation | One console suite: BigInteger references, algebraic identities, edge/alias/allocation checks, both kernel policies and optional native blst comparisons |
| Platform infrastructure | Linux/macOS x64/ARM64 and Windows ARM64 native reference builds, ABI/layout checks, five-platform CI, stock ARM64 crossgen2 inventories and Linux ARM64 emulation/JIT listings |

EIP-2537 integration and BenchmarkDotNet precompile comparisons exist in a Nethermind branch that is not published yet. With the carry-aware lowering in [dotnet/runtime PR #134039](https://github.com/dotnet/runtime/pull/134039) (`0a303c9` or later), precompile parity has been reached and exceeded: every measured precompile takes less time than blst, with ratios of 0.50–0.93x. Variable-time scalar algorithms, endomorphism-split MSM and fused arithmetic contribute to the lead. Some individual components still trail native. The stock-JIT gap remains the main open performance item: stock .NET 10 takes about 1.5–3.8x blst's time in the condensed table. See [performance](performance.md) and [design](design.md).

## Open work

| Item | Required work |
|---|---|
| Hash-to-field and message hash-to-curve | Expand-message XMD SHA-256, hash-to-field, DST handling and upstream hash-to-curve vectors. Existing map APIs accept field elements only. |
| BLS signature API | Key generation, signing, verification, aggregation and min-pk/min-sig variants, with explicit validation contracts. |
| Constant-time extensions and review | Independent security review before signing support; repeat JIT audits on additional runtimes/architectures. Benchmark generator tables. Fixed binary-GCD inverses are implemented, with recorded x64 audits and stock ARM64 generated-code review. Existing MSM and pairing APIs remain variable-time. |
| blst-compatible serialization | Compressed and uncompressed point formats, canonical decoding, infinity/sign rules and precise format errors. Raw field/coordinate APIs already exist. |
| Stock-JIT performance | Main open performance item. Track adoption of PR #134039 into released runtimes and measure arithmetic calling patterns on stock hosts. Preserve compact .NET 10 fallbacks and measure .NET 10 and .NET 11 separately. |
| Hosted CI validation | The five-platform workflow is implemented; collect its first hosted results and benchmark artifacts. Both frameworks, both native kernel policies and managed-only runs are configured. |
| Packaging and publishing | Package metadata review, repository URL, version/release policy, publication and release validation. Development package generation already works. |
| ARM64 hardware and broader platforms | Native builds, stock .NET 10/11 ARM64 code review, Linux ARM64 emulation and CI on Linux/macOS/Windows ARM64 hardware are in place; ARM64 defaults to the expanded kernels. Stock ARM64 JITs still materialize carries, leaving managed code about 1.3–3x behind blst's ARM64 assembly: extend the carry-aware JIT work to ARM64. Runtime JIT/tier-1 review on macOS/Windows ARM64 remains open. macOS x64 has a build path but no CI job in the current matrix. |

## Engineering constraints

Keep production arithmetic fixed-width and free of `BigInteger` and native calls. Use `in`, `out`, spans and bounded stack or pooled storage for hot paths. Reject noncanonical coordinates and document byte order. Establish bounds for every wide formula. Validate changes against independent references and optional native blst, then remeasure affected operations. Reported timings describe specific builds and hardware; they are not performance guarantees.
