param(
    [string]$OutputDirectory = 'build-test',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $OutputDirectory
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$output = Join-Path $PSScriptRoot $OutputDirectory
$requiredRuntimeFiles = @(
    'ClairePdfEditor.exe',
    'ClairePdfEditor.exe.config',
    'ClairePdfEditor.Core.dll',
    'ClairePdfEditor.PdfExport.dll',
    'ClairePdfEditor.Tests.exe',
    'ClairePdfEditor.Tests.exe.config',
    'PdfSharp-wpf.dll',
    'PdfSharp.Shared.dll',
    'PdfSharp.System.dll',
    'Microsoft.Bcl.AsyncInterfaces.dll',
    'Microsoft.Extensions.DependencyInjection.Abstractions.dll',
    'Microsoft.Extensions.Logging.Abstractions.dll',
    'System.Buffers.dll',
    'System.Memory.dll',
    'System.Numerics.Vectors.dll',
    'System.Runtime.CompilerServices.Unsafe.dll',
    'System.Security.Cryptography.Pkcs.dll',
    'System.Threading.Tasks.Extensions.dll'
)
$missingRuntimeFiles = @($requiredRuntimeFiles | Where-Object { -not (Test-Path -LiteralPath (Join-Path $output $_)) })
if ($missingRuntimeFiles.Count -gt 0) {
    throw "Incomplete Claire PDF Editor build '$OutputDirectory'. Missing runtime files: $($missingRuntimeFiles -join ', ')"
}

& (Join-Path $output 'ClairePdfEditor.Tests.exe')
exit $LASTEXITCODE
