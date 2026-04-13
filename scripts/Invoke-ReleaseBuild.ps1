param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "src\artifacts\publish\$Runtime"
$portableRoot = Join-Path $root "src\artifacts\portable\$Runtime"
$zipPath = Join-Path $root "src\artifacts\portable\AdsbObserver-$Runtime-portable.zip"

if (Test-Path $portableRoot) {
    Remove-Item -Recurse -Force $portableRoot
}

if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

dotnet publish (Join-Path $root "src\AdsbObserver.App\AdsbObserver.App.csproj") `
    -c $Configuration `
    -p:PublishProfile=Properties\PublishProfiles\win-x64.pubxml

& (Join-Path $PSScriptRoot "Test-ReleaseLayout.ps1") -PublishDir $publishDir

New-Item -ItemType Directory -Force -Path $portableRoot | Out-Null
Copy-Item -Recurse -Force (Join-Path $publishDir "*") $portableRoot
foreach ($directory in @("data", "maps", "recordings", "logs")) {
    New-Item -ItemType Directory -Force -Path (Join-Path $portableRoot $directory) | Out-Null
}

Compress-Archive -Path (Join-Path $portableRoot "*") -DestinationPath $zipPath

Write-Host "Portable ZIP created:" $zipPath
