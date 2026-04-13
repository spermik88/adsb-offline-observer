param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "src\artifacts\publish\$Runtime"
$portableRoot = Join-Path $root "src\artifacts\portable\$Runtime"
$releaseDir = Join-Path $root "src\artifacts\release\$Runtime"
$zipPath = Join-Path $releaseDir "AdsbObserver-$Runtime-portable.zip"

if (Test-Path $portableRoot) {
    Remove-Item -Recurse -Force $portableRoot
}

if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}

if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

if (Test-Path $releaseDir) {
    Remove-Item -Recurse -Force $releaseDir
}

dotnet publish (Join-Path $root "src\AdsbObserver.App\AdsbObserver.App.csproj") `
    -c $Configuration `
    -r $Runtime `
    -p:PublishProfile=Properties\PublishProfiles\win-x64.pubxml

& (Join-Path $PSScriptRoot "Test-ReleaseLayout.ps1") -PublishDir $publishDir

New-Item -ItemType Directory -Force -Path $portableRoot | Out-Null
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
Copy-Item -Recurse -Force (Join-Path $publishDir "*") $portableRoot
foreach ($directory in @("data", "maps", "recordings", "logs")) {
    $target = Join-Path $portableRoot $directory
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Set-Content -Path (Join-Path $target ".portablekeep") -Value "keep" -NoNewline
}

Compress-Archive -Path (Join-Path $portableRoot "*") -DestinationPath $zipPath
& (Join-Path $PSScriptRoot "Test-ReleaseLayout.ps1") -ZipPath $zipPath

Write-Host "Portable ZIP created:" $zipPath
