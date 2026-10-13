# Carry-aware JIT

`carry-rows.patch` extends dotnet/runtime PR #134039 at commit `331fa2b3` with carry propagation across Montgomery rows, flag-based selects and OSR support. It is an experimental runtime patch; the library builds and runs on the stock JIT without it. The measured patched launcher identifies itself as .NET 12.0.0-dev.

The patched Fp multiplier is near native ADX blst parity. The field follow-up removes carry materialization from the captured hot kernels and improves Fp2 multiplication, but Fp2 multiply/square remain slightly slower than blst. Larger formulas still depend on compact arithmetic calls and register pressure. See the [field-kernel report](../docs/field-kernel-report.md) for before/after timings, regression coverage and patch changes, [performance](../docs/performance.md) for historical EIP-2537 measurements and reproduction commands, and [design](../docs/design.md#inlining-and-calls) for the calling pattern.
