param(
    [string]$PublishDir,
    [string]$ZipPath
)

$ErrorActionPreference = "Stop"

function Test-RequiredFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath
    )

    $required = @(
        "AdsbObserver.App.exe",
        "backend\dump1090\dump1090.exe",
        "backend\dump1090\SOURCE.txt",
        "backend\dump1090\dump1090.cfg",
        "portable.layout.json",
        "PORTABLE.txt"
    )

    $missing = @()
    foreach ($relative in $required) {
        $full = Join-Path $RootPath $relative
        if (-not (Test-Path $full)) {
            $missing += $relative
        }
    }

    if ($missing.Count -gt 0) {
        throw "Release layout is incomplete. Missing: $($missing -join ', ')"
    }

    $sourcePath = Join-Path $RootPath "backend\dump1090\SOURCE.txt"
    $sourceContent = Get-Content $sourcePath -Raw
    $sourcePlaceholders = @(
        "record the exact tag or commit here before release",
        "record SHA256 here before release",
        "Do not ship the setup until this file is filled"
    )

    $unresolved = $sourcePlaceholders | Where-Object { $sourceContent.Contains($_) }
    if ($unresolved.Count -gt 0) {
        throw "Release layout is incomplete. backend\dump1090\SOURCE.txt still contains release placeholders."
    }

    if (Test-Path (Join-Path $RootPath "drivers")) {
        throw "Portable release must not include drivers\."
    }
}

if ($PublishDir) {
    $PublishDir = (Resolve-Path $PublishDir).Path
    Test-RequiredFiles -RootPath $PublishDir
    Write-Host "Publish layout validated:" $PublishDir
}

if ($ZipPath) {
    $ZipPath = (Resolve-Path $ZipPath).Path
    if (-not (Test-Path $ZipPath)) {
        throw "Portable ZIP not found: $ZipPath"
    }

    $extractRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("adsbobserver-layout-" + [Guid]::NewGuid().ToString("N"))
    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $extractRoot)
        Test-RequiredFiles -RootPath $extractRoot

        foreach ($directory in @("data", "maps", "recordings", "logs")) {
            if (-not (Test-Path (Join-Path $extractRoot $directory))) {
                throw "Portable ZIP layout is incomplete. Missing directory: $directory"
            }
        }

        Write-Host "Portable ZIP validated:" $ZipPath
    }
    finally {
        if (Test-Path $extractRoot) {
            Remove-Item -Recurse -Force $extractRoot
        }
    }
}
