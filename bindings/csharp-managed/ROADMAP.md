# Managed C# BLST Roadmap

This area is a staged managed rewrite of BLST for high-performance C# crypto code. The
existing `bindings/c#` directory remains the generated P/Invoke proof of concept over
`blst.dll`; new managed code lives here and is split into separate projects so each
cryptographic layer can be tested and benchmarked before the next one depends on it.

## Engineering Rules

- Use fixed-width value types backed by `ulong` limbs.
- Keep hot arithmetic APIs allocation-free: prefer `in`, `out`, `Span<T>`, and
  `stackalloc`.
- Store field elements in Montgomery form internally.
- Keep parsing and serialization explicit about byte order and canonical range checks.
- Validate each layer against independent tests and against the native C BLST library
  before building dependent layers.
- Keep constant-time behavior in scope for secret-dependent operations; do not replace
  production arithmetic with `BigInteger`.

## Milestones

1. Arithmetic foundation
   - Implement `Fr` and `Fp` limb arithmetic in managed C#.
   - Support zero, one, canonical big/little-endian parsing, serialization, add, sub,
     neg, multiply, and square.
   - Add allocation checks and native-comparison benchmark hooks.

2. Extension fields
   - Implement `Fp2`, then `Fp6` and `Fp12` on top of the `Fp` kernel.
   - Add Frobenius, conjugation, inversion, cyclotomic square, and final exponent
     helpers as needed by pairings.

3. G1/G2 curve operations
   - Implement affine and Jacobian point types for G1 and G2.
   - Add point addition, doubling, scalar multiplication, generator constants, equality,
     infinity handling, curve checks, and subgroup checks.

4. Serialization compatibility
   - Match BLST compressed and uncompressed encodings.
   - Add strict error reporting compatible with the current C API surface.

5. Hash-to-field and hash-to-curve
   - Implement expand-message XMD SHA-256, hash-to-field, SSWU map, isogeny maps, and
     cofactor clearing.
   - Validate with the existing hash-to-curve vectors under `bindings/vectors`.

6. Pairing and BLS API
   - Implement Miller loop, final exponentiation, pairing aggregation, core verify,
     sign, aggregate, and the min-pk/min-sig convenience layers.

7. Packaging and performance hardening
   - Add NuGet-ready packaging, CI tests, C-vs-managed benchmark baselines, no-allocation
     regression checks, and platform-specific optimization notes.

## Current Status

Milestone 1 has an initial managed arithmetic kernel for `Fr` and `Fp`, a no-package
test runner, and a benchmark runner under this directory. The benchmark runner can
compare against native BLST when `blst.dll` is built in the repository root.

## Local Commands

```powershell
./bindings/csharp-managed/build-native-blst.ps1
dotnet run --project bindings/csharp-managed/Blst.Managed.Tests/Blst.Managed.Tests.csproj -c Release
dotnet run --project bindings/csharp-managed/Blst.Managed.Benchmarks/Blst.Managed.Benchmarks.csproj -c Release
```
