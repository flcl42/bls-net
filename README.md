# bls-net

Efficient, pure C# BLS12-381 for .NET 10 and .NET 11, without native runtime dependencies: field arithmetic, G1/G2 operations, MSM, map-to-curve and pairing checks for EIP-2537 integration.

This project is a C# rewrite of [Supranational's blst](https://github.com/supranational/blst), adapting its algorithms, formulas and constants for managed .NET. See [credits and license](#credits-and-license) for attribution and licensing.

Development status: implemented and covered by reference, algebraic, alias/allocation and optional native differential tests. Release packaging remains open. Original APIs are variable-time for public inputs. Explicit constant-time field inversion, scalar multiplication, point helpers and maps are available; see [contracts and limits](docs/design.md#constant-time-apis) and the [runtime audit](docs/constant-time-validation.md). Scalar multiplication and MSM require subgroup points; check the curve before checking the subgroup. Pairing callers must validate both. Complete secret-key support, BLS signatures, message hash-to-curve with DST, and compressed/uncompressed point encodings remain open. See [design](docs/design.md) and [roadmap](docs/roadmap.md).

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

## Build and test

Use the .NET 11 SDK in [global.json](global.json). The library targets `net10.0;net11.0`. The single console test runner defaults to net11.0; use `dotnet run`, not `dotnet test`.

```powershell
dotnet build Nethermind.Crypto.BlsNet.slnx -c Release
dotnet run --project tests/Nethermind.Crypto.BlsNet.Tests -c Release
dotnet run --project tests/Nethermind.Crypto.BlsNet.Tests -c Release -- --compact-kernels
dotnet run --project tests/Nethermind.Crypto.BlsNet.Tests -c Release -- --expanded-kernels
dotnet run --project tests/Nethermind.Crypto.BlsNet.Tests -c Release -p:BlsNetTestTargetFramework=net10.0
dotnet pack src/Nethermind.Crypto.BlsNet -c Release -o artifacts/packages
```

Arithmetic, kernels, tower/pairing, curves/maps/MSM and constant-time functional checks run together and report check counts. `--constant-time-only` selects just the new functional checks. Native comparisons print `SKIP` when blst is absent; `--no-native` explicitly selects managed checks, and `--require-native` fails if the reference is missing. Both runners print the OS, process architecture, runtime and selected policy. Cold-call and policy diagnostics are available as `--kernel-cold-start`, `--square-cold-start` and `--kernel-policy` in separate processes.

To add the optional native reference on Windows x64:

```powershell
git submodule update --init --depth 1 external/blst
./scripts/build-native-blst.ps1
dotnet run --project tests/Nethermind.Crypto.BlsNet.Tests -c Release
```

The x64 script downloads MinGW/w64devkit when needed and detects ADX/BMI2. On Windows ARM64 it instead uses upstream's MSVC/ARM64 assembler build; install Visual Studio Build Tools with the MSVC ARM64 tools and a Windows SDK. `-Architecture arm64` also cross-builds from an x64 developer machine with those tools installed.

On Linux or macOS, install a C compiler and the .NET 10 GA / .NET 11 preview SDKs, then use the same `dotnet build` and `dotnet run` commands above:

```sh
git submodule update --init --depth 1 external/blst
sh scripts/build-native-blst.sh
dotnet build Nethermind.Crypto.BlsNet.slnx -c Release --warnaserror
dotnet run --project tests/Nethermind.Crypto.BlsNet.Tests -c Release -- --compact-kernels --require-native
dotnet run --project tests/Nethermind.Crypto.BlsNet.Tests -c Release -- --expanded-kernels --require-native
dotnet run --project tests/Nethermind.Crypto.BlsNet.Tests -c Release -p:BlsNetTestTargetFramework=net10.0 -- --require-native
dotnet run --project tests/Nethermind.Crypto.BlsNet.Tests -c Release -- --no-native
```

The shell script uses blst's `build.sh` for x64 or ARM64 and produces `artifacts/native/libblst.so` on Linux or `libblst.dylib` on macOS. It forwards upstream options such as `CC=clang`. Build and execute with the same architecture, including when using Rosetta. The shared loader probes each ancestor of the executable directory for `artifacts/native/<platform library>` and then `<platform library>`; a present incompatible binary fails with OS/architecture context. Native blst is only used by tests and benchmarks.

Managed platform targets are Linux, macOS and Windows on little-endian x64 and ARM64. CI runs the full suite on Linux, macOS and Windows ARM64 hardware as well as Linux and Windows x64. Linux ARM64 also has emulated execution and runtime JIT review, and Linux/macOS/Windows ARM64 have stock .NET 10/11 crossgen2 code review. ARM64 defaults to the expanded kernels on every runtime; see [ARM64 hardware results](docs/performance.md#arm64-hardware). See [audit coverage and limits](docs/constant-time-validation.md#arm64-validation).

[CI](.github/workflows/ci.yml) configures Ubuntu x64/ARM64, Windows x64/ARM64 and macOS ARM64 runners. Every runner builds both frameworks with warnings as errors, runs native comparisons under both policies plus a managed-only run, and uploads informational `--quick` benchmarks for both frameworks/policies. macOS x64 has a native build path but no hosted job in this matrix.

## Benchmarks

```powershell
dotnet run --project benchmarks/Nethermind.Crypto.BlsNet.Benchmarks -c Release -- --quick
dotnet run --project benchmarks/Nethermind.Crypto.BlsNet.Benchmarks -c Release -- --filter 'Fp multiply|Fp2 multiply|Pairing check 2'
```

One CLI covers fields, tower/pairing, curves/maps/MSM and the dependent Fr/Fp mul+add loops. `--filter` matches case-insensitive substrings separated by `|`; `--quick` shortens rounds and the default MSM sweep. Kernel policy switches and `--no-native` work here too. On Windows, set `BLST_BENCH_PROCESSOR` to pin a logical processor and disable tiering with `DOTNET_TieredCompilation=0` for measurements. Benchmarks also accept `-p:BlsNetTestTargetFramework=net10.0`.

`--filter 'CT '` measures constant-time / variable-time / blst side by side. The slow timing-leak experiment is opt-in with `--timing-leak --timing-samples 20000`; it includes variable-time multiplication as a required positive control. See [validation](docs/constant-time-validation.md) for its method and interpretation.

The retained EIP-2537 results use Nethermind's BenchmarkDotNet in-process Short job on a Ryzen 9 9950X3D VM, Windows 11. Native is Nethermind.Crypto.Bls 0.3.17.15; managed is compiled for net10.0. Stock is .NET 10.0.12. Fast results use the carry-aware lowering now included in [dotnet/runtime PR #134039](https://github.com/dotnet/runtime/pull/134039), head `0a303c9` or later.

| Precompile | blst | Managed .NET 10 | Managed carry-aware JIT |
|---|---:|---:|---:|
| G1ADD | 2.7 µs | 4.9 µs | 2.2 µs |
| G1MSM, 128 points | 2.73 ms | 10.06 ms | 2.22 ms |
| G2MSM, 128 points | 6.45 ms | 14.39 ms | 3.64 ms |
| MAP_FP2_TO_G2 | 102.4 µs | 300.8 µs | 87.3 µs |
| PAIRING_CHECK, 2 pairs | 689.0 µs | 1.76 ms | 595.8 µs |

Stock RyuJIT materializes carries through comparisons and integer values. PR #134039 keeps carries in flags across arithmetic chains, eliminating much of that overhead. To reproduce these precompile results, build dotnet/runtime from that PR at `0a303c9` or later; these rows need no local patch. These results also benefit from algorithmic differences from blst. See [performance](docs/performance.md) for full precompile results, method, caveats and runtime build commands. Current map and constant-time inverse results use the [JIT follow-up](jit/README.md); see the [component capture](docs/performance.md#fixed-binary-gcd-inverses). The Nethermind integration branch is not published yet.

## Credits and license

This C# rewrite adapts algorithms, formulas and constants from [Supranational blst](https://github.com/supranational/blst), copyright Supranational LLC. The upstream reference is pinned at [`f62244ef50ad1a603decdb8f215e982d2a467bb6`](https://github.com/supranational/blst/tree/f62244ef50ad1a603decdb8f215e982d2a467bb6) for differential tests and benchmarks. The managed implementation and upstream blst are distributed under [Apache-2.0](LICENSE); see [NOTICE](NOTICE). The C# rewrite changes the implementation language, runtime, APIs and optimizations; existing source notices identify specific adaptations.

The optional .NET runtime patches in `jit/` are distributed under their separate [MIT license](jit/LICENSE), with attribution in [NOTICE](NOTICE).
