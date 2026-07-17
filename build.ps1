param([string]$OutputDirectory = 'build')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $root $OutputDirectory
New-Item -ItemType Directory -Force -Path $out | Out-Null
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) { throw 'The Windows C# compiler was not found.' }

$pdfSharpProbe = Join-Path $root 'deps\pdfsharp-wpf.6.2.4\lib\net462\PdfSharp-wpf.dll'
if (-not (Test-Path -LiteralPath $pdfSharpProbe)) {
    & (Join-Path $root 'restore-dependencies.ps1')
}

& $csc /nologo /target:library /out:"$out\ClairePdfEditor.Core.dll" /reference:System.Web.Extensions.dll "$root\PdfEditor.Core.cs"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$gac = 'C:\Windows\Microsoft.NET\assembly'
$windowsBase = "$gac\GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll"
$presentationCore = "$gac\GAC_64\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll"
$presentationFramework = "$gac\GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll"
$systemXaml = "$gac\GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll"

$pdfSharpDir = Join-Path $root 'deps\pdfsharp-wpf.6.2.4\lib\net462'
if (-not (Test-Path -LiteralPath $pdfSharpDir)) { throw 'PDFsharp-WPF 6.2.4 is missing from deps.' }
Copy-Item -LiteralPath (Join-Path $pdfSharpDir 'PdfSharp-wpf.dll') -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $pdfSharpDir 'PdfSharp.Shared.dll') -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $pdfSharpDir 'PdfSharp.System.dll') -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $root 'deps\microsoft.extensions.logging.abstractions.8.0.3\lib\net462\Microsoft.Extensions.Logging.Abstractions.dll') -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $root 'deps\microsoft.extensions.dependencyinjection.abstractions.8.0.2\lib\net462\Microsoft.Extensions.DependencyInjection.Abstractions.dll') -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $root 'deps\microsoft.bcl.asyncinterfaces.8.0.0\lib\net462\Microsoft.Bcl.AsyncInterfaces.dll') -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $root 'deps\system.security.cryptography.pkcs.8.0.1\lib\net462\System.Security.Cryptography.Pkcs.dll') -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $root 'deps\system.buffers.4.5.1\lib\net461\System.Buffers.dll') -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $root 'deps\system.memory.4.5.5\lib\net461\System.Memory.dll') -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $root 'deps\system.runtime.compilerservices.unsafe.6.0.0\lib\net461\System.Runtime.CompilerServices.Unsafe.dll') -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $root 'deps\system.threading.tasks.extensions.4.5.4\lib\net461\System.Threading.Tasks.Extensions.dll') -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $root 'deps\system.numerics.vectors.4.5.0\lib\net46\System.Numerics.Vectors.dll') -Destination $out -Force
& $csc /nologo /target:library /out:"$out\ClairePdfEditor.PdfExport.dll" /reference:"$out\ClairePdfEditor.Core.dll" /reference:"$out\PdfSharp-wpf.dll" /reference:"$windowsBase" /reference:"$presentationCore" "$root\PdfEditor.PdfExport.cs"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $csc /nologo /target:winexe /out:"$out\ClairePdfEditor.exe" /reference:"$out\ClairePdfEditor.Core.dll" /reference:"$out\ClairePdfEditor.PdfExport.dll" /reference:"$windowsBase" /reference:"$presentationCore" /reference:"$presentationFramework" /reference:"$systemXaml" "$root\PdfEditor.App.cs"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $csc /nologo /target:exe /out:"$out\ClairePdfEditor.Tests.exe" /reference:"$out\ClairePdfEditor.Core.dll" /reference:"$out\ClairePdfEditor.PdfExport.dll" "$root\PdfEditor.Tests.cs"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Copy-Item -LiteralPath (Join-Path $root 'App.config') -Destination (Join-Path $out 'ClairePdfEditor.exe.config') -Force
Copy-Item -LiteralPath (Join-Path $root 'App.config') -Destination (Join-Path $out 'ClairePdfEditor.Tests.exe.config') -Force

Write-Host "Build complete: $out\ClairePdfEditor.exe"
