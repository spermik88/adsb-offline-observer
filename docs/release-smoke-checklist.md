# Release Smoke Checklist

1. Start from a clean Windows machine with .NET Desktop Runtime installed.
2. Extract `src/artifacts/release/win-x64/AdsbObserver-win-x64-portable.zip` into a new versioned folder.
3. Confirm `portable.layout.json` points shared writable data outside the versioned app folder.
4. Launch `AdsbObserver.App.exe` with no internet and no maps installed.
5. Verify the UI text is readable in Russian and the setup banner explains what is available now.
6. Verify startup works with no SDR attached and that playback/history remain available.
7. Put one `.mbtiles` file into `maps/` and confirm the app detects it after `Сканировать карты`.
8. Start the app with `backend/dump1090/dump1090.exe` removed and confirm live diagnostics report bundled backend missing without a crash.
9. Connect one RTL-SDR dongle with a working driver and run `Проверить Live`.
10. Press `Start Live` and verify bundled `dump1090` starts, opens port `30003`, and writes logs into `logs/`.
11. Stop live mode and start it again; confirm no orphaned backend processes remain.
12. Occupy port `30003` before launch and confirm the app reports an external SBS-1 source instead of starting a second backend.
