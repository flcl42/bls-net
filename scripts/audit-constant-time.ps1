param(
    [Parameter(Mandatory=$true)][string]$Dotnet,
    [ValidateSet('net10.0','net11.0')][string]$TargetFramework = 'net11.0',
    [ValidateSet('compact','expanded')][string]$KernelPolicy = 'expanded',
    [switch]$Tiered,
    [string]$OutputDirectory = 'artifacts/constant-time-audit'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot
$output = [IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
New-Item -ItemType Directory -Force -Path $output | Out-Null
if (Test-Path -LiteralPath (Join-Path $output 'disassembly.txt')) { Remove-Item -LiteralPath (Join-Path $output 'disassembly.txt') }
$saved = @{}
$settings = @{
    DOTNET_TieredCompilation = $(if ($Tiered) { '1' } else { '0' }); DOTNET_ReadyToRun = '0'
    DOTNET_JitDisasm = '*'; DOTNET_JitDisasmAssemblies = 'Nethermind.Crypto.BlsNet'
    DOTNET_JitStdOutFile = (Join-Path $output 'disassembly.txt')
    DOTNET_ROOT = (Split-Path $Dotnet); DOTNET_ROLL_FORWARD = 'Major'
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
