# ADS-B Observer

Windows-приложение на `WPF` для наблюдения за `ADS-B` трафиком через `RTL-SDR`, просмотра истории, playback-записей и работы с офлайн-картами `MBTiles`.

## Что в репозитории считать рабочим

- Исходники приложения находятся только в `src/AdsbObserver.App`, `src/AdsbObserver.Core`, `src/AdsbObserver.Infrastructure`.
- Каталоги `bin/`, `obj/`, `tests/**/bin`, `tests/**/obj`, `src/artifacts`, `artifacts` считаются производными.
- `C:\Users\bebra\Documents\Playground\src\AdsbObserver.App\bin\Debug\net8.0-windows\AdsbObserver.App.exe` это обычный локальный debug-выход текущего проекта, а не отдельная версия приложения.

## Структура проекта

- `src/AdsbObserver.App` - WPF-клиент
- `src/AdsbObserver.Core` - доменные модели и интерфейсы
- `src/AdsbObserver.Infrastructure` - хранилище, `dump1090`, карты, устройства, импорт и экспорт
- `tests/AdsbObserver.Tests` - `xUnit` тесты
- `scripts` - служебные скрипты очистки и release-сборки
- `artifacts` - publish/portable/release артефакты, создаются скриптами и могут быть удалены
- `docs` - заметки по релизу и проверкам

## Быстрый запуск для разработки

Требования:

- Windows x64
- `.NET SDK 10.0.201` по `global.json`
- `Microsoft.WindowsDesktop.App 8.x`

Сборка:

```powershell
cd C:\Users\bebra\Documents\Playground
dotnet build .\AdsbObserver.slnx
```

Запуск из исходников:

```powershell
dotnet run --project .\src\AdsbObserver.App\AdsbObserver.App.csproj
```

После обычной debug-сборки запускной файл появляется здесь:

`C:\Users\bebra\Documents\Playground\src\AdsbObserver.App\bin\Debug\net8.0-windows\AdsbObserver.App.exe`

## Очистка мусора сборки

Безопасно удалять:

- все `bin/` и `obj/`
- `src/artifacts`
- `artifacts`

Полная очистка рабочего дерева:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Clean-Workspace.ps1
```

## Release и portable-сборка

Publish и portable-артефакты больше не должны смешиваться с исходниками. Скрипты складывают результаты в верхнеуровневый каталог:

- publish: `C:\Users\bebra\Documents\Playground\artifacts\publish\win-x64`
- portable: `C:\Users\bebra\Documents\Playground\artifacts\portable\win-x64`
- portable zip: `C:\Users\bebra\Documents\Playground\artifacts\release\win-x64\AdsbObserver-win-x64-portable.zip`

Сборка portable-релиза:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Invoke-ReleaseBuild.ps1
```

## Live-режим с RTL-SDR

Для live-приёма нужен совместимый `RTL-SDR` донгл и установленный `WinUSB` драйвер.

1. Подключите `RTL-SDR` устройство.
2. Откройте `C:\Users\bebra\Documents\Playground\src\AdsbObserver.App\BundledAssets\drivers\rtl-sdr`.
3. Запустите `zadig.exe`.
4. В `Zadig` выберите устройство `RTL-SDR`.
5. Установите драйвер `WinUSB`.
6. Запустите приложение.
7. Нажмите `Обновить SDR`, затем `Проверить Live`.
8. Если окружение готово, нажмите `Старт Live`.

## Работа без SDR

Приложение можно использовать и без live-приёма:

- открывать playback
- просматривать историю
- фильтровать треки
- экспортировать данные в `CSV`
- работать с офлайн-картами

Чтобы добавить карты, положите файлы `*.mbtiles` в каталог `maps` рядом с выбранной portable-сборкой или в рабочую portable-папку данных.

## Диагностика

Если приложение не стартует:

- проверьте наличие `Microsoft.WindowsDesktop.App 8.x`
- убедитесь, что запускаете либо debug-выход из `bin\Debug`, либо явную portable-сборку из `artifacts\portable`
- проверьте логи в portable-каталоге `logs`

Если live-режим не поднимается:

- убедитесь, что `RTL-SDR` определяется системой
- переустановите `WinUSB` через `zadig.exe`
- нажмите `Обновить SDR` и `Проверить Live`
- проверьте логи `dump1090`

## Полезные документы

- [Portable release notes](/C:/Users/bebra/Documents/Playground/docs/portable-release-notes.md)
- [Release binaries](/C:/Users/bebra/Documents/Playground/docs/release-binaries.md)
- [Release smoke checklist](/C:/Users/bebra/Documents/Playground/docs/release-smoke-checklist.md)
