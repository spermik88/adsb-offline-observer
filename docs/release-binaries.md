# Bundled Release Binaries

## readsb
- Expected path: `src/AdsbObserver.App/BundledAssets/backend/readsb/readsb.exe`
- Required before release: yes
- Action: record source URL, exact version or commit, and SHA256 checksum in `SOURCE.txt`

## Zadig
- Expected path: `src/AdsbObserver.App/BundledAssets/drivers/rtl-sdr/zadig.exe`
- Purpose: guided WinUSB driver installation for RTL-SDR on Windows 10
- Action before release: confirm tested version and checksum
