# ADS-B Observer

Windows-приложение на `WPF` для наблюдения за `ADS-B` трафиком через `RTL-SDR`, просмотра истории, playback-записей и работы с офлайн-картами `MBTiles`.

## Что умеет

- live-прием через встроенный `dump1090`
- playback сохраненной истории
- карта с переключением слоев и отображением треков
- фильтрация по `ICAO`, callsign, высоте, скорости и дистанции
- импорт базы распознавания бортов
- экспорт истории в `CSV`
- portable-режим с локальными папками `data`, `logs`, `maps`, `recordings`

## Структура проекта

- `src/AdsbObserver.App` — WPF-клиент
- `src/AdsbObserver.Core` — доменные модели и интерфейсы
- `src/AdsbObserver.Infrastructure` — работа с хранилищем, `dump1090`, картами, устройствами и импортом/экспортом
- `tests/AdsbObserver.Tests` — `xUnit` тесты
- `src/artifacts/portable/win-x64` — готовая portable-сборка
- `src/artifacts/portable/AdsbObserver-win-x64-portable.zip` — portable-архив
- `docs` — заметки по релизу и smoke-check

## Быстрый старт

### Вариант 1: запустить готовую portable-сборку

1. Открой папку `C:\Users\bebra\Documents\Playground\src\artifacts\portable\win-x64`.
2. Запусти `AdsbObserver.App.exe`.
3. Если Windows покажет предупреждение безопасности, подтверди запуск.

Приложение будет использовать папки рядом с `.exe`:

- `data` — база и настройки
- `logs` — логи приложения и `dump1090`
- `maps` — офлайн-карты `MBTiles`
- `recordings` — экспорт и сохраненные данные

### Вариант 2: запустить из исходников

Требования:

- Windows x64
- `.NET SDK 10.0.201` по `global.json`
- `Microsoft.WindowsDesktop.App 8.x`

Команда запуска:

```powershell
cd C:\Users\bebra\Documents\Playground
dotnet run --project .\src\AdsbObserver.App\AdsbObserver.App.csproj
```

## Live-режим с RTL-SDR

Для live-приема нужен совместимый `RTL-SDR` донгл и установленный `WinUSB` драйвер.

1. Подключи `RTL-SDR` устройство.
2. Открой папку `C:\Users\bebra\Documents\Playground\src\AdsbObserver.App\BundledAssets\drivers\rtl-sdr`.
3. Запусти `zadig.exe`.
4. В `Zadig` выбери устройство `RTL-SDR`.
5. Установи драйвер `WinUSB`.
6. Запусти `AdsbObserver.App.exe`.
7. Нажми `Refresh SDR`, затем `Check Live`.
8. Если окружение готово, нажми `Start Live`.

Примечания:

- встроенный `dump1090.exe` уже включен в приложение
- portable-сборка не устанавливает драйверы автоматически
- если устройство не найдено, проверь драйвер и повтори `Refresh SDR`

## Работа без SDR

Приложение можно использовать и без live-приема:

- открывать playback
- просматривать историю
- фильтровать треки
- экспортировать данные в `CSV`
- работать с офлайн-картами

Чтобы добавить карты, положи файлы `*.mbtiles` в:

`C:\Users\bebra\Documents\Playground\src\artifacts\portable\win-x64\maps`

## Сборка и публикация

### Сборка решения

```powershell
cd C:\Users\bebra\Documents\Playground
dotnet build .\AdsbObserver.slnx
```

### Запуск тестов

```powershell
dotnet test .\AdsbObserver.slnx
```

### Публикация `win-x64`

```powershell
dotnet publish .\src\AdsbObserver.App\AdsbObserver.App.csproj -c Release -r win-x64
```

Текущий publish-профиль собирает приложение в:

`src\artifacts\publish\win-x64`

## Где искать готовые файлы

- portable-папка: `C:\Users\bebra\Documents\Playground\src\artifacts\portable\win-x64`
- publish-папка: `C:\Users\bebra\Documents\Playground\src\artifacts\publish\win-x64`
- portable-zip: `C:\Users\bebra\Documents\Playground\src\artifacts\portable\AdsbObserver-win-x64-portable.zip`

## Диагностика

Если приложение не стартует:

- проверь наличие `Microsoft.WindowsDesktop.App 8.x`
- проверь, что запускается именно `AdsbObserver.App.exe` из portable-папки
- посмотри логи в `src\artifacts\portable\win-x64\logs`

Если live-режим не поднимается:

- убедись, что `RTL-SDR` определяется в системе
- переустанови `WinUSB` через `zadig.exe`
- в приложении нажми `Refresh SDR` и `Check Live`
- проверь логи `dump1090` в папке `logs`

## Полезные документы

- [Portable release notes](C:\Users\bebra\Documents\Playground\docs\portable-release-notes.md)
- [Release binaries](C:\Users\bebra\Documents\Playground\docs\release-binaries.md)
- [Release smoke checklist](C:\Users\bebra\Documents\Playground\docs\release-smoke-checklist.md)
