# Bundled Release Binaries

## dump1090
- Expected path: `src/AdsbObserver.App/BundledAssets/backend/dump1090/dump1090.exe`
- Required before release: yes
- Action: record source URL, exact version or commit, and SHA256 checksum in `SOURCE.txt`
- Release gate: `scripts/Test-ReleaseLayout.ps1` fails if `dump1090.exe` is missing or `SOURCE.txt` still contains placeholders
- Recommended prep order:
  1. copy the approved `dump1090.exe` into `src/AdsbObserver.App/BundledAssets/backend/dump1090/`
  2. fill `src/AdsbObserver.App/BundledAssets/backend/dump1090/SOURCE.txt` with source URL, exact version or commit, and SHA256
  3. run `powershell -ExecutionPolicy Bypass -File scripts\Invoke-ReleaseBuild.ps1`

## Zadig
- Expected path: `src/AdsbObserver.App/BundledAssets/drivers/rtl-sdr/zadig.exe`
- Purpose: guided WinUSB driver installation for RTL-SDR on Windows 10
- Action before release: confirm tested version and checksum
