param(
    [string]$Dotnet,
    [ValidateSet('net10.0','net11.0')][string]$TargetFramework = 'net11.0',
    [ValidateSet('compact','expanded')][string]$KernelPolicy = 'expanded',
    [switch]$Tiered,
    [string]$Arm64AltJit,
    [string]$Crossgen2,
    [string]$RuntimeDirectory,
    [ValidateSet('linux','osx','windows')][string]$TargetOS = 'linux',
    [string]$OutputDirectory = 'artifacts/constant-time-audit'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot
$output = [IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
New-Item -ItemType Directory -Force -Path $output | Out-Null
if (Test-Path -LiteralPath (Join-Path $output 'disassembly.txt')) { Remove-Item -LiteralPath (Join-Path $output 'disassembly.txt') }
if ($Crossgen2) {
    if (!$RuntimeDirectory) { throw '-RuntimeDirectory must name the matching runtime managed DLL directory.' }
    if ($Tiered) { throw 'Crossgen2 produces ReadyToRun code, not tiered runtime code.' }
    $coreStream = [IO.File]::OpenRead((Join-Path $RuntimeDirectory 'System.Private.CoreLib.dll'))
    $corePe = [System.Reflection.PortableExecutable.PEReader]::new($coreStream)
    try {
        # R2R encodes its OS by XORing the COFF architecture (runtime's
        # PETargetExtensions.MachineOSOverride); Unix CoreLib is not plain ARM64.
        $osOverride = switch ($TargetOS) { 'linux' { 0x7b79 } 'osx' { 0x4644 } 'windows' { 0 } }
        $expectedMachine = [int][System.Reflection.PortableExecutable.Machine]::Arm64 -bxor $osOverride
        if ([int]$corePe.PEHeaders.CoffHeader.Machine -ne $expectedMachine) {
            throw 'Use ARM64 runtime-pack managed DLLs for the matching target OS, not host x64 CoreLib (which changes intrinsic expansion).'
        }
    } finally { $corePe.Dispose(); $coreStream.Dispose() }
    $library = Join-Path $repo "src/Nethermind.Crypto.BlsNet/bin/Release/$TargetFramework/Nethermind.Crypto.BlsNet.dll"
    $driver = Join-Path $repo "tests/Nethermind.Crypto.BlsNet.Tests/bin/Release/$TargetFramework/Nethermind.Crypto.BlsNet.Tests.dll"
    # Crossgen2 deliberately skips AggressiveOptimization methods. Clear ONLY
    # that metadata hint in disposable copies so the actual hot/secret IL is
    # compiled too. The production binaries and method bodies stay untouched.
    $inputs = Join-Path $output 'inputs'
    New-Item -ItemType Directory -Force $inputs | Out-Null
    $copies = foreach ($inputFile in @($library, $driver)) {
        $bytes = [IO.File]::ReadAllBytes($inputFile)
        $stream = [IO.MemoryStream]::new($bytes, $false)
        $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
        try {
            $metadata = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
            $table = [System.Reflection.Metadata.Ecma335.TableIndex]::MethodDef
            $start = $pe.PEHeaders.MetadataStartOffset + [System.Reflection.Metadata.Ecma335.MetadataReaderExtensions]::GetTableMetadataOffset($metadata, $table)
            $rowSize = [System.Reflection.Metadata.Ecma335.MetadataReaderExtensions]::GetTableRowSize($metadata, $table)
            $rows = [System.Reflection.Metadata.Ecma335.MetadataReaderExtensions]::GetTableRowCount($metadata, $table)
            for ($row = 0; $row -lt $rows; $row++) {
                $offset = $start + $row * $rowSize + 4 # RVA followed by ImplFlags
                $flags = [BitConverter]::ToUInt16($bytes, $offset) -band 0xfdff
                $bytes[$offset] = $flags -band 255
                $bytes[$offset + 1] = $flags -shr 8
            }
        } finally { $pe.Dispose(); $stream.Dispose() }
        $copy = Join-Path $inputs ([IO.Path]::GetFileName($inputFile))
        [IO.File]::WriteAllBytes($copy, $bytes)
        $copy
    }
    & $Crossgen2 @copies --composite --inputbubble -O --parallelism:1 `
        "--targetos:$TargetOS" --targetarch:arm64 "-r:$RuntimeDirectory/*.dll" `
        "--out:$output/audit-r2r.dll" '--codegenopt:JitDisasm=*' `
        "--codegenopt:JitStdOutFile=$output/disassembly.txt" | Tee-Object -FilePath (Join-Path $output 'run.txt')
    if ($LASTEXITCODE) { throw "Crossgen2 failed ($LASTEXITCODE)" }
    "ReadyToRun ARM64 $TargetOS; compiler: $Crossgen2; references: $RuntimeDirectory; both runtime policy paths retained; AggressiveOptimization cleared in disposable input copies" |
        Set-Content (Join-Path $output 'scope.txt')
    & python (Join-Path $PSScriptRoot 'list-constant-time-jumps.py') (Join-Path $output 'disassembly.txt') --all-methods
    if ($LASTEXITCODE) { throw 'Disassembly inventory failed' }
    return
}
if (!$Dotnet) { throw 'Specify -Dotnet for runtime captures, or -Crossgen2 and -RuntimeDirectory for ARM64 R2R.' }
$saved = @{}
$settings = @{
    DOTNET_TieredCompilation = $(if ($Tiered) { '1' } else { '0' }); DOTNET_ReadyToRun = '0'
    DOTNET_JitDisasm = '*'; DOTNET_JitDisasmAssemblies = 'Nethermind.Crypto.BlsNet;Nethermind.Crypto.BlsNet.Tests'
    DOTNET_JitStdOutFile = (Join-Path $output 'disassembly.txt')
    DOTNET_ROOT = (Split-Path $Dotnet); DOTNET_ROLL_FORWARD = 'Major'
}
if ($Arm64AltJit) {
    if ($Tiered) { throw 'Use an untiered private host for cross-architecture AltJit captures.' }
    $settings.DOTNET_AltJit = '*'
    $settings.DOTNET_AltJitName = $Arm64AltJit
    $settings.DOTNET_EnableArm64Sve = '0'
    $settings.DOTNET_EnableArm64Sve2 = '0'
    $settings.DOTNET_EnableArm64Cssc = '0'
    "ARM64 AltJit on an x64 private host; compiler: $Arm64AltJit; executed code is the x64 fallback. Windows ABI; validate Linux hardware separately." |
        Set-Content (Join-Path $output 'scope.txt')
}
foreach ($key in $settings.Keys) { $saved[$key] = [Environment]::GetEnvironmentVariable($key); [Environment]::SetEnvironmentVariable($key, $settings[$key]) }
try {
    $testDll = Join-Path $repo "tests/Nethermind.Crypto.BlsNet.Tests/bin/Release/$TargetFramework/Nethermind.Crypto.BlsNet.Tests.dll"
    $driver = if ($Tiered) { '--constant-time-audit-warm' } else { '--constant-time-audit' }
    & $Dotnet $testDll $driver --no-native "--$KernelPolicy-kernels" | Tee-Object -FilePath (Join-Path $output 'run.txt')
    if ($LASTEXITCODE) { throw "Audit driver failed ($LASTEXITCODE)" }
    & python (Join-Path $PSScriptRoot 'list-constant-time-jumps.py') (Join-Path $output 'disassembly.txt')
    if ($LASTEXITCODE) { throw 'Disassembly inventory failed' }
} finally {
    foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key]) }
}
