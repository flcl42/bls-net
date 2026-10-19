# Runtime patches

Place optional `git diff` patches here and list them in `RUNTIME_PATCHES` in
`perf/runtime.env`. They are applied to the chosen runtime checkout in order.

`cache-probe.patch` adds only a comment in `src/coreclr/jit/lowerarmarch.cpp`.
It is a diagnostic for proving that a changed JIT source tree reuses the cached
testhost. It is not an optimization and is disabled in the baseline configuration.
The patched runtime source is MIT licensed; see [the runtime patch license](../../jit/LICENSE).
