using AdsbObserver.Core.Models;
using AdsbObserver.Core.Services;

namespace AdsbObserver.App.ViewModels;

public sealed class TrackViewModel
{
    public TrackViewModel(
        AircraftTrack model,
        double centerLatitude,
        double centerLongitude,
        TrackVisualState visualState,
        bool isSelected)
    {
        Model = model;
        VisualState = visualState;
        IsSelected = isSelected;

        if (model.HasPosition)
        {
            DistanceKm = GeoMath.DistanceKilometers(centerLatitude, centerLongitude, model.Latitude!.Value, model.Longitude!.Value);
            BearingDegrees = GeoMath.BearingDegrees(centerLatitude, centerLongitude, model.Latitude.Value, model.Longitude.Value);
        }
    }

    public AircraftTrack Model { get; }
    public TrackVisualState VisualState { get; }
    public bool IsSelected { get; }
    public bool IsStale => VisualState == TrackVisualState.Stale;
    public bool HasPosition => Model.HasPosition;
    public bool IsEmergency => Model.Squawk is "7500" or "7600" or "7700";
    public double? DistanceKm { get; }
    public double? BearingDegrees { get; }
    public string Icao => Model.Icao;
    public string Callsign => string.IsNullOrWhiteSpace(Model.Callsign) ? "Без позывного" : Model.Callsign!;
    public string Registration => Model.Recognition?.Registration ?? "Нет данных";
    public string AircraftType => Model.Recognition?.AircraftType ?? "Нет данных";
    public string Operator => Model.Recognition?.Operator ?? "Нет данных";
    public string Country => Model.Recognition?.Country ?? "Нет данных";
    public string PositionText => Model.HasPosition ? $"{Model.Latitude:F4}, {Model.Longitude:F4}" : "Нет позиции";
    public string AltitudeText => Model.AltitudeFeet.HasValue ? $"{Model.AltitudeFeet:N0} ft" : "Нет данных";
    public string SpeedText => Model.GroundSpeedKnots.HasValue ? $"{Model.GroundSpeedKnots:N0} kt" : "Нет данных";
    public string HeadingText => Model.HeadingDegrees.HasValue ? $"{Model.HeadingDegrees:N0}°" : "Нет данных";
    public string VerticalRateText => Model.VerticalRateFeetPerMinute.HasValue ? $"{Model.VerticalRateFeetPerMinute:N0} ft/min" : "Нет данных";
    public string DistanceText => DistanceKm.HasValue ? $"{DistanceKm.Value:N1} км" : "Нет данных";
    public string BearingText => BearingDegrees.HasValue ? $"{BearingDegrees.Value:N0}°" : "Нет данных";
    public string TrackStatusText => IsEmergency ? "Тревога" : IsStale ? "Устарел" : HasPosition ? "Активен" : "Нет позиции";
    public string Squawk => Model.Squawk ?? "Нет данных";
    public string EmitterCategory => Model.EmitterCategory ?? "Нет данных";
    public string LastSeenText => Model.LastSeenUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public string LastSeenAgeText
    {
        get
        {
            var age = DateTime.UtcNow - Model.LastSeenUtc;
            return age.TotalSeconds < 60
                ? $"{Math.Max(0, age.Seconds)} с назад"
                : $"{Math.Max(0, (int)age.TotalMinutes)} мин назад";
        }
    }
}
