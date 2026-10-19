#!/bin/sh
# Build for the current Linux/macOS process architecture. Forward upstream options.
set -eu
repo=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
case $(uname -s) in
    Linux|Darwin) ;;
    *) echo 'Use build-native-blst.ps1 on Windows.' >&2; exit 1 ;;
esac
case $(uname -m) in
    x86_64|aarch64|arm64) ;;
    *) echo 'Supported native reference architectures: x64 and arm64.' >&2; exit 1 ;;
esac
if [ ! -f "$repo/external/blst/src/server.c" ]; then
    echo 'Run: git submodule update --init --depth 1 external/blst' >&2
    exit 1
fi
mkdir -p "$repo/artifacts/native"
cd "$repo/artifacts/native"
# A Windows checkout used from WSL may have CRLF in the submodule. Normalize
# only the script text. A relative $0 also keeps upstream's unquoted source
# paths free of spaces even when the checkout's absolute path contains them.
exec sh -c "$(tr -d '\r' < "$repo/external/blst/build.sh")" ../../external/blst/build.sh -shared "$@"
