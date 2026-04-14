param(
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$directories = @(
    (Join-Path $root "src\AdsbObserver.App\bin"),
    (Join-Path $root "src\AdsbObserver.App\obj"),
    (Join-Path $root "src\AdsbObserver.Core\bin"),
    (Join-Path $root "src\AdsbObserver.Core\obj"),
    (Join-Path $root "src\AdsbObserver.Infrastructure\bin"),
    (Join-Path $root "src\AdsbObserver.Infrastructure\obj"),
    (Join-Path $root "tests\AdsbObserver.Tests\bin"),
    (Join-Path $root "tests\AdsbObserver.Tests\obj"),
    (Join-Path $root "src\artifacts")
)

if (-not $KeepArtifacts) {
    $directories += (Join-Path $root "artifacts")
}

foreach ($path in $directories) {
    if (Test-Path $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
        Write-Host "Removed:" $path
    }
}

Write-Host "Workspace cleanup completed."
