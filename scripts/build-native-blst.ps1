param(
    [string]$W64DevkitVersion = "2.7.0",
    [ValidateSet('x64','arm64')][string]$Architecture = $(if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x64' })
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$blstRoot = Join-Path $repoRoot "external\blst"
$outputRoot = Join-Path $repoRoot "artifacts\native"
if (!(Test-Path -LiteralPath (Join-Path $blstRoot "src\server.c"))) {
    throw "Initialize native test sources with: git submodule update --init --depth 1 external/blst"
}
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
if ($Architecture -eq 'arm64') {
    # Use upstream's ARM64 assembler and MSVC DLL build. Import the developer
    # environment in the child process; do not change this PowerShell session.
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (!(Test-Path -LiteralPath $vswhere)) { throw 'Install Visual Studio Build Tools with MSVC ARM64 tools and a Windows SDK.' }
    $vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.ARM64 -property installationPath
    if (!$vs) { throw 'No Visual Studio installation with MSVC ARM64 tools found.' }
    $vcvars = Join-Path $vs 'VC/Auxiliary/Build/vcvarsall.bat'
    $build = Join-Path $blstRoot 'build.bat'
    $hostArchitecture = if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'amd64_arm64' }
    Push-Location $outputRoot
    try {
        if (Test-Path -LiteralPath 'blst.dll') { Remove-Item -LiteralPath 'blst.dll' -Force }
        & $env:ComSpec /d /c "`"`"$vcvars`" $hostArchitecture && call `"$build`" -shared`""
        if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath 'blst.dll')) { throw 'MSVC ARM64 blst build failed.' }
        Get-Item -LiteralPath 'blst.dll'
    } finally { Pop-Location }
    return
}
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

    $serverSource = Join-Path $blstRoot "src/server.c"
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
