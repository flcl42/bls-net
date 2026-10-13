param(
    [string]$W64DevkitVersion = "2.7.0",
    [switch]$TowerBenchmarks
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$blstRoot = Join-Path $repoRoot "external\blst"
$outputRoot = Join-Path $repoRoot "artifacts\native"
if (!(Test-Path -LiteralPath (Join-Path $blstRoot "src\server.c"))) {
    throw "Initialize native test sources with: git submodule update --init --depth 1 external/blst"
}
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$toolsRoot = Join-Path $repoRoot "artifacts\native-tools"
$extractRoot = Join-Path $toolsRoot "extract"
$kitRoot = Join-Path $extractRoot "w64devkit"
$kitBin = Join-Path $kitRoot "bin"
$gcc = Join-Path $kitBin "gcc.exe"

New-Item -ItemType Directory -Force -Path $toolsRoot | Out-Null

if (!(Test-Path -LiteralPath $gcc)) {
    $archiveName = "w64devkit-x64-$W64DevkitVersion.7z.exe"
    $archive = Join-Path $toolsRoot $archiveName
    $url = "https://github.com/skeeto/w64devkit/releases/download/v$W64DevkitVersion/$archiveName"

    if (!(Test-Path -LiteralPath $archive)) {
        Write-Host "Downloading $archiveName..."
        Invoke-WebRequest -Uri $url -OutFile $archive
    }

    Write-Host "Extracting w64devkit..."
    New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
    & $archive -y "-o$extractRoot" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "w64devkit extraction failed" }

    if (!(Test-Path -LiteralPath $gcc)) {
        throw "w64devkit extraction did not produce $gcc"
    }
}

$previousPath = $env:PATH
$env:PATH = "$kitBin;$env:PATH"

Push-Location $outputRoot
try {
    Remove-Item -LiteralPath "server.o", "assembly.o", "libblst.a", "blst.dll" -Force -ErrorAction SilentlyContinue

    $cflags = @("-O2", "-fno-builtin", "-fPIC", "-Wall", "-Wextra", "-Werror")

    # Like build.sh, use the MULX/ADCX/ADOX code paths when the build machine supports ADX.
    $macros = "" | & $gcc -march=native -dM -E -x c -
    if ($LASTEXITCODE -ne 0) { throw "gcc CPU feature detection failed" }
    if ($macros -match "^#define __ADX__ " -and $macros -match "^#define __BMI2__ ") {
        Write-Host "Building blst with ADX/BMI2."
        $cflags = @("-D__ADX__") + $cflags
    }

    $serverSource = if ($TowerBenchmarks) { Join-Path $repoRoot "scripts/blst-tower.c" } else { Join-Path $blstRoot "src/server.c" }
    & $gcc @cflags "-D__BLST_DLL_MAIN__" -c $serverSource -o "server.o"
    if ($LASTEXITCODE -ne 0) { throw "gcc failed for src/server.c" }

    & $gcc @cflags -c (Join-Path $blstRoot "build/assembly.S") -o "assembly.o"
    if ($LASTEXITCODE -ne 0) { throw "gcc failed for build/assembly.S" }

    & (Join-Path $kitBin "ar.exe") rc "libblst.a" "server.o" "assembly.o"
    if ($LASTEXITCODE -ne 0) { throw "ar failed" }

    & (Join-Path $kitBin "ranlib.exe") "libblst.a"
    if ($LASTEXITCODE -ne 0) { throw "ranlib failed" }

    $linkArgs = @(
        "-shared",
        "-o", "blst.dll",
        (Join-Path $blstRoot "build/win64/blst.def"),
        "-Wl,--entry=DllMain",
        "-nostartfiles",
        "-Wl,--whole-archive,libblst.a,--no-whole-archive"
    ) + $cflags + @("-D__BLST_DLL_MAIN__")

    & $gcc @linkArgs
    if ($LASTEXITCODE -ne 0) { throw "gcc failed linking blst.dll" }

    Get-Item -LiteralPath "blst.dll"
}
finally {
    Pop-Location
    $env:PATH = $previousPath
}
