# Release Smoke Checklist

## Автоматическая проверка
1. Запустите `dotnet test AdsbObserver.slnx`.
2. Запустите `powershell -ExecutionPolicy Bypass -File scripts\Invoke-ReleaseBuild.ps1`.
3. Убедитесь, что создан `src/artifacts/release/win-x64/AdsbObserver-win-x64-portable.zip`.
4. Убедитесь, что smoke-check release layout и ZIP завершился без ошибок.

## Ручная проверка portable-сборки
1. Подготовьте чистую Windows-машину с установленным .NET Desktop Runtime.
2. Распакуйте `AdsbObserver-win-x64-portable.zip` в новую versioned-папку.
3. Проверьте, что `portable.layout.json` указывает shared writable data вне versioned-папки.
4. Запустите `AdsbObserver.App.exe` без интернета и без карт.
5. Убедитесь, что русский текст в UI читается нормально и блок состояния объясняет, что доступно сейчас.
6. Проверьте сценарий без SDR: приложение запускается, `playback` и история доступны, live помечен как недоступный.
7. Положите один `.mbtiles` в `maps/` и нажмите `Сканировать карты`; приложение должно подхватить карту.
8. Удалите `backend/dump1090/dump1090.exe` и проверьте, что `Проверить Live` показывает отсутствие bundled backend без падения приложения.
9. Подключите RTL-SDR с уже установленным рабочим драйвером и нажмите `Проверить Live`.
10. Нажмите `Start Live` и проверьте, что bundled `dump1090` стартует, открывает `30003` и пишет лог в `logs/`.
11. Выполните `Stop`, затем снова `Start Live`; убедитесь, что висящих `dump1090` процессов не остается.
12. Займите `30003` заранее и проверьте, что приложение сообщает о внешнем SBS-1 источнике, а не запускает второй backend.
13. Повторите запуск без SDR, без карт и без интернета после первого live-сеанса, чтобы убедиться, что portable data переживает перезапуск.
