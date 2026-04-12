param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "src\artifacts\publish\$Runtime"
$setupScript = Join-Path $root "installer\AdsbObserver.iss"

dotnet publish (Join-Path $root "src\AdsbObserver.App\AdsbObserver.App.csproj") `
    -c $Configuration `
    -p:PublishProfile=Properties\PublishProfiles\win-x64.pubxml

& (Join-Path $PSScriptRoot "Test-ReleaseLayout.ps1") -PublishDir $publishDir

$iscc = Get-Command iscc -ErrorAction SilentlyContinue
if ($null -eq $iscc) {
    Write-Warning "Inno Setup compiler 'iscc' was not found. Publish layout is ready, but setup was not built."
    exit 0
}

& $iscc.Source $setupScript
