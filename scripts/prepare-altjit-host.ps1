param(
    [Parameter(Mandatory)][string]$SourceHost,
    [Parameter(Mandatory)][string]$OutputHost
)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath($SourceHost).TrimEnd('\','/')
$output = [IO.Path]::GetFullPath($OutputHost).TrimEnd('\','/')
if ($source -eq $output -or $source.StartsWith($output + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $output.StartsWith($source + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The AltJit host must be a separate private copy.'
}
New-Item -ItemType Directory -Force $output | Out-Null
& robocopy $source $output /E /NFL /NDL /NJH /NJS /NP
if ($LASTEXITCODE -ge 8) { throw "Host copy failed: $LASTEXITCODE" }
$cores = @(Get-ChildItem (Join-Path $output 'shared/Microsoft.NETCore.App') -Filter System.Private.CoreLib.dll -Recurse)
if ($cores.Count -ne 1) { throw 'Expected one CoreLib in the private host.' }
$path = $cores[0].FullName
$bytes = [IO.File]::ReadAllBytes($path)
$stream = [IO.MemoryStream]::new($bytes, $false)
$pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
try {
    $md = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
    $bigMul = $null
    $multiplyHigh = $null
    foreach ($handle in $md.TypeDefinitions) {
        $type = $md.GetTypeDefinition($handle)
        $name = $md.GetString($type.Name)
        if ($name -eq 'Math' -and $md.GetString($type.Namespace) -eq 'System') {
            foreach ($methodHandle in $type.GetMethods()) {
                $method = $md.GetMethodDefinition($methodHandle)
                if ($md.GetString($method.Name) -eq 'BigMul' -and
                    [BitConverter]::ToString($md.GetBlobBytes($method.Signature)) -eq '00-03-0B-0B-0B-10-0B') {
                    $bigMul = $method
                }
            }
        }
        if ($name -eq 'Arm64' -and !$type.GetDeclaringType().IsNil -and
            $md.GetString($md.GetTypeDefinition($type.GetDeclaringType()).Name) -eq 'ArmBase') {
            foreach ($methodHandle in $type.GetMethods()) {
                $method = $md.GetMethodDefinition($methodHandle)
                if ($md.GetString($method.Name) -eq 'MultiplyHigh' -and
                    [BitConverter]::ToString($md.GetBlobBytes($method.Signature)) -eq '00-02-0B-0B-0B') {
                    $multiplyHigh = [System.Reflection.Metadata.Ecma335.MetadataTokens]::GetToken([System.Reflection.Metadata.EntityHandle]$methodHandle)
                }
            }
        }
    }
    if (!$bigMul -or !$multiplyHigh) { throw 'Missing expected arithmetic methods.' }
    $body = [System.Reflection.Metadata.PEReaderExtensions]::GetMethodBody($pe, $bigMul.RelativeVirtualAddress)
    $il = $body.GetILBytes()
    # The host CoreLib was linked for x64: the ArmBase support check in Math.BigMul
    # became ldc.i4.0. Restore ONLY that unreachable x64 fallback in this copy.
    # The original X86Base path remains byte-for-byte intact for actual execution.
    if ($il.Length -ne 42 -or $il[0] -ne 0x28 -or $il[5] -ne 0x2c -or $il[6] -ne 23 -or
        $il[29] -ne 0x2a -or $il[30] -ne 0x16 -or $il[31] -ne 0x2c -or $il[32] -ne 0) {
        throw 'Unexpected Math.BigMul IL; this utility only supports the recorded 74ed6106 x64 testhost.'
    }
    $offset = $null
    foreach ($section in $pe.PEHeaders.SectionHeaders) {
        if ($bigMul.RelativeVirtualAddress -ge $section.VirtualAddress -and
            $bigMul.RelativeVirtualAddress -lt $section.VirtualAddress + $section.SizeOfRawData) {
            $offset = $section.PointerToRawData + $bigMul.RelativeVirtualAddress - $section.VirtualAddress
        }
    }
    if ($null -eq $offset -or ($bytes[$offset] -band 3) -ne 3) { throw 'Expected a fat IL header.' }
    $headerSize = ($bytes[$offset + 1] -shr 4) * 4
    $codeOffset = $offset + $headerSize
    if ($headerSize -ne 12 -or $bytes[$codeOffset + 42] -notin 0,0x90 -or
        $bytes[$codeOffset + 43] -notin 0,0x90 -or $body.ExceptionRegions.Length -ne 0) {
        throw 'Expected two alignment bytes after the method, with no exception regions.'
    }
    # ldarg.2; ldarg.0; ldarg.1; mul; stind.i8;
    # ldarg.0; ldarg.1; call ArmBase.Arm64.MultiplyHigh; ret
    [byte[]]$tail = @(4,2,3,0x5a,0x55,2,3,0x28) + [BitConverter]::GetBytes([int]$multiplyHigh) + @(0x2a)
    [Array]::Copy($tail, 0, $bytes, $codeOffset + 30, $tail.Length)
    [Array]::Copy([BitConverter]::GetBytes([int]43), 0, $bytes, $offset + 4, 4)
} finally { $pe.Dispose(); $stream.Dispose() }
[IO.File]::WriteAllBytes($path, $bytes)
Write-Output "Prepared private AltJit CoreLib: $path"
Write-Output 'Use only for cross-codegen captures; set DOTNET_ReadyToRun=0 and retain the x64 X86Base feature.'
