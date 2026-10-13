param(
    [Parameter(Mandatory)][string]$SdkDotnet,
    [Parameter(Mandatory)][string]$Stock10Dotnet,
    [Parameter(Mandatory)][string]$Stock11Dotnet,
    [Parameter(Mandatory)][string]$PatchedDotnet,
    [string]$OutputDirectory = "artifacts/field-runtime-matrix",
    [switch]$SkipBuild,
    [switch]$Timings,
    [int]$Cpu = 0
)

$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
New-Item -ItemType Directory -Force -Path $output | Out-Null
$suites = @("Tests", "CurveTests", "PairingTests")
$runtimeCases = @(
    @{ Name = "stock10"; Dotnet = $Stock10Dotnet; Frameworks = @("net10.0") },
    @{ Name = "stock11"; Dotnet = $Stock11Dotnet; Frameworks = @("net10.0", "net11.0") },
    @{ Name = "patched"; Dotnet = $PatchedDotnet; Frameworks = @("net10.0", "net11.0") }
)
$saved = @{}
foreach ($name in @("DOTNET_ROLL_FORWARD", "DOTNET_TieredCompilation", "BLSNET_EXPANDED_KERNELS", "KERNEL_CPU")) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name)
}
try {
    $env:DOTNET_ROLL_FORWARD = "Major"
    $env:DOTNET_TieredCompilation = "0"
    $env:KERNEL_CPU = "$Cpu"
    # Keep every build and process sequential, including the timing sweeps.
    if (!$SkipBuild) {
        foreach ($framework in @("net10.0", "net11.0")) {
            & $SdkDotnet build (Join-Path $repo "Nethermind.Crypto.BlsNet.slnx") -c Release "-p:BlsNetTestTargetFramework=$framework" |
                Tee-Object -FilePath (Join-Path $output "build-$framework.txt")
            if ($LASTEXITCODE -ne 0) { throw "Build failed for $framework" }
        }
    }
    foreach ($runtimeCase in $runtimeCases) {
        foreach ($framework in $runtimeCase.Frameworks) {
            foreach ($mode in @("compact", "expanded")) {
                $env:BLSNET_EXPANDED_KERNELS = if ($mode -eq "expanded") { "1" } else { "0" }
                $prefix = "$($runtimeCase.Name)-$framework-$mode"
                foreach ($suite in $suites) {
                    $assembly = Join-Path $repo "tests/Nethermind.Crypto.BlsNet.$suite/bin/Release/$framework/Nethermind.Crypto.BlsNet.$suite.dll"
                    Write-Output "Running $prefix $suite"
                    & $runtimeCase.Dotnet $assembly | Tee-Object -FilePath (Join-Path $output "$prefix-$suite.txt")
                    if ($LASTEXITCODE -ne 0) { throw "$prefix $suite failed" }
                    if ($suite -eq "PairingTests") {
                        & $runtimeCase.Dotnet $assembly --kernel-cold-start | Tee-Object -FilePath (Join-Path $output "$prefix-cold-start.txt")
                        if ($LASTEXITCODE -ne 0) { throw "$prefix cold-start tests failed" }
                        & $runtimeCase.Dotnet $assembly --kernel-tests | Tee-Object -FilePath (Join-Path $output "$prefix-kernels.txt")
                        if ($LASTEXITCODE -ne 0) { throw "$prefix kernel tests failed" }
                        if ($Timings) {
                            & $runtimeCase.Dotnet $assembly --kernel-timing | Tee-Object -FilePath (Join-Path $output "$prefix-timing.txt")
                            if ($LASTEXITCODE -ne 0) { throw "$prefix timing failed" }
                        }
                    }
                }
            }
        }
    }
} finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
}
