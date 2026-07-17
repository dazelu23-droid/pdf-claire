param([string]$Destination = 'deps')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$deps = Join-Path $root $Destination
New-Item -ItemType Directory -Force -Path $deps | Out-Null

$packages = @(
    @{ Id = 'pdfsharp-wpf'; Version = '6.2.4' },
    @{ Id = 'microsoft.extensions.logging.abstractions'; Version = '8.0.3' },
    @{ Id = 'system.security.cryptography.pkcs'; Version = '8.0.1' },
    @{ Id = 'microsoft.extensions.dependencyinjection.abstractions'; Version = '8.0.2' },
    @{ Id = 'system.buffers'; Version = '4.5.1' },
    @{ Id = 'system.memory'; Version = '4.5.5' },
    @{ Id = 'system.runtime.compilerservices.unsafe'; Version = '6.0.0' },
    @{ Id = 'system.threading.tasks.extensions'; Version = '4.5.4' },
    @{ Id = 'microsoft.bcl.asyncinterfaces'; Version = '8.0.0' },
    @{ Id = 'system.numerics.vectors'; Version = '4.5.0' }
)

foreach ($package in $packages) {
    $name = "$($package.Id).$($package.Version)"
    $expanded = Join-Path $deps $name
    if (Test-Path -LiteralPath $expanded) {
        Write-Host "Already restored: $name"
        continue
    }

    $archive = Join-Path $deps ($name + '.zip')
    $uri = "https://api.nuget.org/v3-flatcontainer/$($package.Id)/$($package.Version)/$name.nupkg"
    Write-Host "Restoring $name"
    Invoke-WebRequest -Uri $uri -OutFile $archive
    Expand-Archive -LiteralPath $archive -DestinationPath $expanded -Force
    Remove-Item -LiteralPath $archive -Force
}

Write-Host 'Dependency restore complete.'
