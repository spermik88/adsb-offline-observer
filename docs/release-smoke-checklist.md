# Release Smoke Checklist

1. Start from a clean Windows 10 VM with no RTL-SDR driver installed.
2. Run the generated setup and confirm the app installs into `Program Files\AdsbObserver`.
3. Launch the app and verify the first-run setup banner explains what is missing.
4. Connect one RTL-SDR dongle and run `Prepare Live`.
5. Accept the elevation prompt for the bundled Zadig helper and install the WinUSB driver for the RTL-SDR device.
6. Re-run `Prepare Live` and confirm the app reports backend ready, driver ready, and live ready.
7. Press `Start Live` and verify the bundled backend starts and the SBS-1 port becomes reachable.
8. Stop live mode and confirm the backend process exits cleanly.
9. Install a newer build over the previous one and confirm settings persist.
10. Uninstall the app and confirm the install directory is removed.
