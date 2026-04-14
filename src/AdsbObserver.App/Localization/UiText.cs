using AdsbObserver.Core.Models;

namespace AdsbObserver.App.Localization;

internal static class UiText
{
    public static string WindowTitle => "ADS-B Observer";
    public static string StartLive => "Старт Live";
    public static string Stop => "Стоп";
    public static string Playback => "Воспроизведение";
    public static string Pause => "Пауза";
    public static string RefreshSdr => "Обновить SDR";
    public static string CheckLive => "Проверить Live";
    public static string CenterSelected => "К центру по выбранному";
    public static string ResetCenter => "Сбросить центр";
    public static string ShowSelected => "Только выбранный";
    public static string MarkIncident => "Отметить инцидент";
    public static string ImportRecognition => "Импорт базы";
    public static string ExportCsv => "Экспорт CSV";
    public static string Layer => "Слой";
    public static string Sort => "Сортировка";
    public static string Zoom => "Масштаб";
    public static string ObservationCenter => "Центр наблюдения";
    public static string Targets => "Цели";
    public static string Search => "Поиск (ICAO / позывной)";
    public static string WithPosition => "Только с координатами";
    public static string AirborneOnly => "Только в воздухе";
    public static string AltitudeRange => "Диапазон высоты (ft)";
    public static string SpeedRange => "Диапазон скорости (kt)";
    public static string MaxDistance => "Макс. дистанция (км)";
    public static string ResetFilters => "Сбросить фильтры";
    public static string SelectedTrack => "Выбранный борт";
    public static string NoSelection => "Ничего не выбрано";
    public static string HistoryScope => "Область истории";
    public static string IcaoFilter => "Фильтр ICAO";
    public static string PeriodLocal => "Период (местное время)";
    public static string SelectedOnly => "Только выбранный";
    public static string CoordinatesOnly => "Только с координатами";
    public static string HistoryHint => "Эти фильтры применяются к воспроизведению и экспорту CSV.";
    public static string LiveStatus => "Состояние Live";
    public static string Health => "Готовность";
    public static string Metrics => "Метрики";
    public static string RecentEvents => "Последние события";
    public static string Settings => "Настройки";
    public static string RtlSdrDevice => "Устройство RTL-SDR";
    public static string UseSimulationFallback => "Использовать simulation fallback";
    public static string AiLogsEnabled => "Включить AI-логи";
    public static string OpenAiLogs => "Открыть AI-логи";
    public static string CopyAiPath => "Скопировать путь AI";
    public static string SaveSettings => "Сохранить настройки";
    public static string MinAltitudeTooltip => "Минимальная высота";
    public static string MaxAltitudeTooltip => "Максимальная высота";
    public static string MinSpeedTooltip => "Минимальная скорость";
    public static string MaxSpeedTooltip => "Максимальная скорость";
    public static string MaxDistanceTooltip => "Максимальная дистанция от центра наблюдения";
    public static string HistoryFromTooltip => "Начало периода, например 2026-04-10 или 2026-04-10 14:30";
    public static string HistoryToTooltip => "Конец периода, например 2026-04-13 или 2026-04-13 22:00";

    public static string LatFormat(double value) => $"Широта: {value:F4}";
    public static string LonFormat(double value) => $"Долгота: {value:F4}";
    public static string RadiusFormat(double value) => $"Радиус: {value:F0} км";
    public static string CallsignFormat(string? value) => $"Позывной: {value}";
    public static string RegistrationFormat(string? value) => $"Регистрация: {value}";
    public static string TypeFormat(string? value) => $"Тип: {value}";
    public static string OperatorFormat(string? value) => $"Оператор: {value}";
    public static string CountryFormat(string? value) => $"Страна: {value}";
    public static string PositionFormat(string? value) => $"Позиция: {value}";
    public static string DistanceFormat(string? value) => $"Дистанция: {value}";
    public static string BearingFormat(string? value) => $"Пеленг: {value}";
    public static string HeadingFormat(string? value) => $"Курс: {value}";
    public static string VerticalRateFormat(string? value) => $"Вертикальная скорость: {value}";
    public static string StatusFormat(string? value) => $"Статус: {value}";
    public static string EmitterFormat(string? value) => $"Категория излучателя: {value}";
    public static string SquawkFormat(string? value) => $"Squawk: {value}";
    public static string LastSeenFormat(string? value) => $"Последний сигнал: {value}";
    public static string AgeFormat(string? value) => $"Возраст: {value}";

    public static string TrackSortMode(TrackSortMode mode) => mode switch
    {
        Core.Models.TrackSortMode.Distance => "По дистанции",
        Core.Models.TrackSortMode.Altitude => "По высоте",
        Core.Models.TrackSortMode.Speed => "По скорости",
        _ => "По времени сигнала"
    };

    public static string MapLayer(MapLayerType layer) => layer switch
    {
        MapLayerType.Satellite => "Спутник",
        _ => "Схема"
    };
}
