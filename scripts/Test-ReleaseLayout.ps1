param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir
)

$ErrorActionPreference = "Stop"
$PublishDir = (Resolve-Path $PublishDir).Path

$required = @(
    "AdsbObserver.App.exe",
    "backend\readsb\readsb.exe",
    "drivers\rtl-sdr\zadig.exe",
    "drivers\rtl-sdr\install-driver.cmd"
)

$missing = @()
foreach ($relative in $required) {
    $full = Join-Path $PublishDir $relative
    if (-not (Test-Path $full)) {
        $missing += $relative
    }
}

if ($missing.Count -gt 0) {
    throw "Release layout is incomplete. Missing: $($missing -join ', ')"
}

Write-Host "Release layout validated:" $PublishDir
