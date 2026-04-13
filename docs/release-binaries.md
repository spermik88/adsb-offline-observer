# Bundled Release Binaries

## dump1090
- Expected path: `src/AdsbObserver.App/BundledAssets/backend/dump1090/dump1090.exe`
- Required before release: yes
- Runtime config: `src/AdsbObserver.App/BundledAssets/backend/dump1090/dump1090.cfg`
- Release source of truth: `BundledAssets/backend/dump1090` plus `SOURCE.txt`
- Action: record source URL, exact version or commit, and SHA256 checksum in `SOURCE.txt`
- Release gate: `scripts/Test-ReleaseLayout.ps1` fails if `dump1090.exe`, `dump1090.cfg`, `portable.layout.json`, or `PORTABLE.txt` is missing, if `SOURCE.txt` still has placeholders, or if drivers are present in the portable ZIP
- Recommended prep order:
  1. copy the approved `dump1090.exe` into `src/AdsbObserver.App/BundledAssets/backend/dump1090/`
  2. fill `src/AdsbObserver.App/BundledAssets/backend/dump1090/SOURCE.txt` with source URL, exact version or commit, and SHA256
  3. run `powershell -ExecutionPolicy Bypass -File scripts\Invoke-ReleaseBuild.ps1`

## Portable package
- Stable ZIP path: `src/artifacts/release/win-x64/AdsbObserver-win-x64-portable.zip`
- Shared portable data is configured through `src/AdsbObserver.App/portable.layout.json`
- Portable layout README lives in `src/AdsbObserver.App/PORTABLE.txt`
- Portable ZIP must include `data`, `maps`, `recordings`, and `logs`
- Portable ZIP must not include `drivers/rtl-sdr`
