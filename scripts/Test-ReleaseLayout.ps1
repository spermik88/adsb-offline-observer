param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir
)

$ErrorActionPreference = "Stop"
$PublishDir = (Resolve-Path $PublishDir).Path

$required = @(
    "AdsbObserver.App.exe",
    "backend\readsb\readsb.exe",
    "backend\readsb\SOURCE.txt",
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

$sourcePath = Join-Path $PublishDir "backend\readsb\SOURCE.txt"
$sourceContent = Get-Content $sourcePath -Raw
$sourcePlaceholders = @(
    "record the exact tag or commit here before release",
    "record SHA256 here before release",
    "Do not ship the setup until this file is filled"
)

$unresolved = $sourcePlaceholders | Where-Object { $sourceContent.Contains($_) }
if ($unresolved.Count -gt 0) {
    throw "Release layout is incomplete. backend\readsb\SOURCE.txt still contains release placeholders."
}

Write-Host "Release layout validated:" $PublishDir
