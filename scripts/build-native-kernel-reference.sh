#!/bin/sh
# Optional benchmark-only exports; build-native-blst.sh must have completed first.
set -eu
repo=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
native=${1:-"$repo/artifacts/native"}
test -f "$native/libblst.a" || { echo 'Build the pinned native blst archive first.' >&2; exit 1; }
cc=${CC:-cc}
variant=''
case $(uname -m) in
    aarch64|arm64) ;;
    x86_64)
        # Match upstream's ADX choice; the archive also retains its generic path.
        if test "$(uname -s)" = Darwin; then
            if test "$(sysctl -n hw.optional.adx 2>/dev/null || true)" = 1; then variant=-D__ADX__; fi
        elif command -v rg >/dev/null 2>&1 && rg -q '\badx\b' /proc/cpuinfo; then variant=-D__ADX__;
        fi ;;
    *) echo 'Only x64 and ARM64 references are supported.' >&2; exit 1 ;;
esac
case $(uname -s) in
    Linux) "$cc" -O2 -fPIC -shared $variant -Wl,-Bsymbolic -o "$native/libblst-kernels.so" "$repo/shared/NativeKernelReference.c" "$native/libblst.a" ;;
    Darwin) "$cc" -O2 -fPIC -dynamiclib $variant -o "$native/libblst-kernels.dylib" "$repo/shared/NativeKernelReference.c" "$native/libblst.a" ;;
    *) echo 'Use a native Linux/macOS compiler.' >&2; exit 1 ;;
esac
