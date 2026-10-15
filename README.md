# Nethermind.Crypto.BlsNet

Pure C# BLS12-381 for .NET 10 and .NET 11, with no native runtime dependencies. The library provides field arithmetic (Fp, Fr and extension fields), G1/G2 addition, curve and subgroup checks, scalar multiplication, MSM, map-to-curve and pairing checks for EIP-2537 integration.

APIs are variable-time and intended for public inputs. They are not suitable for secret keys yet. Scalar multiplication and MSM require subgroup points; validate that points are on the curve before checking their subgroup. Pairing checks require callers to validate points and subgroups.

## Usage

This example decodes raw G1 coordinates, computes a two-point MSM and checks a cancelling pairing product. Coordinates contain 48-byte canonical big-endian field elements; the point at infinity is all-zero coordinates. Scalars contain exactly 32 little-endian bytes per point and are reduced modulo the scalar order. EIP-2537's padded coordinates and big-endian scalar wire encoding need conversion by the caller.

```csharp
using Nethermind.Crypto.BlsNet.Arithmetic;
using Nethermind.Crypto.BlsNet.Curves;
using Pair = Nethermind.Crypto.BlsNet.Pairing.Pairing;

static G1Affine DecodeG1(ReadOnlySpan<byte> raw)
{
    if (raw.Length != 96 ||
        !Fp.TryFromBigEndian(raw[..48], out Fp x) ||
        !Fp.TryFromBigEndian(raw[48..], out Fp y))
        throw new ArgumentException("Invalid raw coordinates.");
    G1Affine point = new(x, y);
    if (!point.IsOnCurve() || !point.IsInSubgroup())
        throw new ArgumentException("Point must be in G1.");
    return point;
}

G1Affine p = DecodeG1(Convert.FromHexString(
    "17f1d3a73197d7942695638c4fa9ac0fc3688c4f9774b905a14e3a3f171bac586c55e83ff97a1aeffb3af00adb22c6bb" +
    "08b3f481e3aaa0f1a09e30ed741d8ae4fcf5e095d5d00af600db18cb2c04b3edd03cc744a2888ae40caa232946c5e7e1"));
byte[] scalars = new byte[64];
scalars[0] = scalars[32] = 1;
G1Projective.MultiScalarMultiply([p, p], scalars, out G1Projective sum);
G1Projective.Negate(sum, out G1Projective negative);
G2Affine q = G2Affine.Generator;
bool valid = Pair.PairingCheck([sum.ToAffine(), negative.ToAffine()], [q, q]);
Console.WriteLine(valid); // True
```

G2 raw coordinates consist of four field elements: X.C0, X.C1, Y.C0, Y.C1. Construct `G2Affine` with two `Fp2` values after canonical parsing and the same curve/subgroup validation. `MapToCurve.MapToG1` and `MapToG2` accept Fp/Fp2 inputs and return subgroup points; these map operations do not hash messages.

## Status

Field arithmetic, extension fields, G1/G2, MSM, map-to-curve and pairing are implemented and covered by reference, algebraic and optional native differential checks. This is a development port; release packaging and broader platform validation are open. See the [design](docs/design.md) and [roadmap](docs/roadmap.md).

Not implemented: hash-to-field and message hash-to-curve with DST, a BLS signature API, constant-time paths, and serialization formats beyond raw coordinates (including blst compressed/uncompressed encodings).

## Build and test

Install the .NET 11 SDK selected by `global.json` (`11.0.100-rc.1.26425.128`, with `latestFeature` roll-forward enabled). The library targets `net10.0;net11.0`. Test runners default to `net11.0`; build them for .NET 10 with `-p:BlsNetTestTargetFramework=net10.0`. The field benchmark runner targets `net11.0`. Run these commands from the repository root:

```powershell
dotnet build Nethermind.Crypto.BlsNet.slnx -c Release
dotnet run --project tests/Nethermind.Crypto.BlsNet.Tests -c Release -f net11.0
dotnet run --project tests/Nethermind.Crypto.BlsNet.CurveTests -c Release -f net11.0
dotnet run --project tests/Nethermind.Crypto.BlsNet.PairingTests -c Release -f net11.0
dotnet pack src/Nethermind.Crypto.BlsNet -c Release -o artifacts/packages
```

These are console test runners: use `dotnet run`, not `dotnet test`. Building and running managed tests does not require the optional submodule. Curve and pairing runners print `SKIP` for native comparisons when blst is missing and still run their managed checks. To run native comparisons on Windows x64:

```powershell
git submodule update --init --depth 1 external/blst
./scripts/build-native-blst.ps1
dotnet run --project tests/Nethermind.Crypto.BlsNet.CurveTests -c Release -f net11.0
dotnet run --project tests/Nethermind.Crypto.BlsNet.PairingTests -c Release -f net11.0
```

The script downloads MinGW/w64devkit when needed, detects ADX/BMI2, and builds `artifacts/native/blst.dll` from the pinned upstream submodule. Native loaders search `artifacts/native/` while walking parent directories, then support the old parent-directory DLL location. The native build script targets Windows x64.

## Benchmarks

```powershell
dotnet run --project benchmarks/Nethermind.Crypto.BlsNet.Benchmarks -c Release
dotnet run --project tests/Nethermind.Crypto.BlsNet.CurveTests -c Release -f net11.0 -- --bench
dotnet run --project tests/Nethermind.Crypto.BlsNet.PairingTests -c Release -f net11.0 -- --timing
```

All runners measure managed operations without native blst; native columns are skipped when it is missing. The EIP-2537 precompile results below use Nethermind's BenchmarkDotNet in-process Short job on a Ryzen 9 9950X3D VM with Windows 11. The native baseline is Nethermind.Crypto.Bls 0.3.17.15; managed uses the same precompile code over this library, compiled for net10.0. Stock is .NET 10.0.12; the patched JIT is .NET 12.0.0-dev at runtime PR #134039 revision `331fa2b3` plus the carry-row patch.

| Precompile | blst | Managed .NET 10 | Managed patched JIT |
|---|---:|---:|---:|
| G1ADD | 2.7 µs | 4.9 µs | 2.2 µs |
| G1MSM, 128 points | 2.73 ms | 10.06 ms | 2.22 ms |
| G2MSM, 128 points | 6.45 ms | 14.39 ms | 3.64 ms |
| MAP_FP2_TO_G2 | 102.4 µs | 300.8 µs | 87.3 µs |
| PAIRING_CHECK, 2 pairs | 689.0 µs | 1.76 ms | 595.8 µs |

Stock .NET 10 takes about 1.5–3.8x blst's time in the condensed precompile table; with the carry-aware JIT every measured precompile is faster, at 0.50–0.93x. See [performance](docs/performance.md) for full tables, algorithmic differences, measurement caveats and reproduction steps; the Nethermind integration branch is not published yet.

The optional [carry-aware JIT patch](jit/) and reproduction notes describe the runtime experiment. It is not required to build or use this library.

## Credits and license

Algorithms, formulas and constants derive from [Supranational blst](https://github.com/supranational/blst), pinned at `f62244ef50ad1a603decdb8f215e982d2a467bb6` for tests and benchmarks. The port is distributed under [Apache-2.0](LICENSE); see [NOTICE](NOTICE) for attribution.
