#!/usr/bin/env bash
set -euo pipefail
repo=$(cd "$(dirname "$0")/.." && pwd)
runtime="$repo/.perf-runtime"
out="$repo/artifacts/perf"
cache="$repo/.perf-cache"
mkdir -p "$out" "$cache"
case $(uname -m) in aarch64|arm64) ;; *) echo 'Real ARM64 required'; exit 1 ;; esac
case $(uname -s) in Linux) rid=linux; ext=so ;; Darwin) rid=osx; ext=dylib ;; esac
start=$SECONDS
cold=0
cd "$runtime"
if [[ ${1:-false} != true ]]; then
    cold_start=$SECONDS
    # Keep only the subsets needed for a usable libraries testhost, excluding
    # NativeAOT, crossgen tools, packages, and out-of-band libraries.
    ./build.sh clr.runtime+clr.corelib+host.native+libs.native+libs.sfx+libs.pretest -c Release \
        -ci 2>&1 | tee "$out/cold-build.log"
    hosts=(artifacts/bin/testhost/net*-$rid-Release-arm64)
    [[ ${#hosts[@]} == 1 && -x "${hosts[0]}/dotnet" ]]
    cp -a "${hosts[0]}" "$cache/testhost"
    cold=$((SECONDS - cold_start))
fi
for config in Release Checked; do
    jit_start=$SECONDS
    config_arg=$(echo "$config" | tr '[:upper:]' '[:lower:]')
    ./src/coreclr/build-runtime.sh -arm64 "-$config_arg" -component jit -ninja \
        2>&1 | tee "$out/jit-$config-build.log"
    cp "artifacts/bin/coreclr/$rid.arm64.$config/libclrjit.$ext" "$out/libclrjit-$config.$ext"
    echo "$config JIT build: $((SECONDS - jit_start)) seconds" | tee -a "$out/timings.txt"
done
echo "Cold testhost build: $cold seconds; cache hit: ${1:-false}" | tee -a "$out/timings.txt"
echo "Runtime setup total: $((SECONDS - start)) seconds" | tee -a "$out/timings.txt"
"$cache/testhost/dotnet" --info | tee "$out/testhost-info.txt"
