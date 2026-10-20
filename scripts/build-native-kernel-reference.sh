#!/bin/sh
# Optional benchmark-only exports; build-native-blst.sh must have completed first.
set -eu
repo=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
native=${1:-"$repo/artifacts/native"}
test -f "$native/libblst.a" || { echo 'Build the pinned native blst archive first.' >&2; exit 1; }
cc=${CC:-cc}
symbol_reader=${NM:-nm}
variant=''
case $(uname -m) in
    aarch64|arm64) ;;
    x86_64)
        # Follow the archive's actual variant, including explicit upstream
        # build options. ADX-only archives omit the generic raw-product/REDC
        # symbols; detecting CPU flags again can disagree with that build.
        if "$symbol_reader" "$native/libblst.a" 2>/dev/null | grep -Eq ' [Tt] _?mulx_384$'; then variant=-D__ADX__;
        elif ! "$symbol_reader" "$native/libblst.a" 2>/dev/null | grep -Eq ' [Tt] _?mul_384$'; then
            echo 'The pinned archive has no supported raw-product assembly.' >&2; exit 1
        fi ;;
    *) echo 'Only x64 and ARM64 references are supported.' >&2; exit 1 ;;
esac
case $(uname -s) in
    Linux) "$cc" -O2 -fPIC -shared $variant -Wl,-Bsymbolic -o "$native/libblst-kernels.so" "$repo/shared/NativeKernelReference.c" "$native/libblst.a" ;;
    Darwin) "$cc" -O2 -fPIC -dynamiclib $variant -o "$native/libblst-kernels.dylib" "$repo/shared/NativeKernelReference.c" "$native/libblst.a" ;;
    *) echo 'Use a native Linux/macOS compiler.' >&2; exit 1 ;;
esac
