# Carry-aware JIT

`carry-rows.patch` extends dotnet/runtime PR #134039 at commit `331fa2b3` with carry propagation across Montgomery rows, flag-based selects and OSR support. It is an experimental runtime patch; the library builds and runs on the stock JIT without it. The measured patched launcher identifies itself as .NET 12.0.0-dev.

The patched field kernels reach native ADX blst parity. Most measured EIP-2537 precompiles also reach or approach parity; pairing checks take 1.15–1.19× blst's time. Larger formulas still depend on compact arithmetic calls and register pressure. See [performance](../docs/performance.md) for all tables, caveats, carry idioms and reproduction commands, and [design](../docs/design.md#inlining-and-calls) for the calling pattern.
