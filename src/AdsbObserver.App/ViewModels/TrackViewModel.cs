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
    public string Callsign => string.IsNullOrWhiteSpace(Model.Callsign) ? "Без callsign" : Model.Callsign!;
    public string Registration => Model.Recognition?.Registration ?? "N/A";
    public string AircraftType => Model.Recognition?.AircraftType ?? "N/A";
    public string Operator => Model.Recognition?.Operator ?? "N/A";
    public string Country => Model.Recognition?.Country ?? "N/A";
    public string PositionText => Model.HasPosition ? $"{Model.Latitude:F4}, {Model.Longitude:F4}" : "Нет позиции";
    public string AltitudeText => Model.AltitudeFeet.HasValue ? $"{Model.AltitudeFeet:N0} ft" : "N/A";
    public string SpeedText => Model.GroundSpeedKnots.HasValue ? $"{Model.GroundSpeedKnots:N0} kt" : "N/A";
    public string HeadingText => Model.HeadingDegrees.HasValue ? $"{Model.HeadingDegrees:N0}°" : "N/A";
    public string VerticalRateText => Model.VerticalRateFeetPerMinute.HasValue ? $"{Model.VerticalRateFeetPerMinute:N0} ft/min" : "N/A";
    public string DistanceText => DistanceKm.HasValue ? $"{DistanceKm.Value:N1} km" : "N/A";
    public string BearingText => BearingDegrees.HasValue ? $"{BearingDegrees.Value:N0}°" : "N/A";
    public string TrackStatusText => IsEmergency ? "Emergency" : IsStale ? "Stale" : HasPosition ? "Active" : "No position";
    public string Squawk => Model.Squawk ?? "N/A";
    public string EmitterCategory => Model.EmitterCategory ?? "N/A";
    public string LastSeenText => Model.LastSeenUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string LastSeenAgeText
    {
        get
        {
            var age = DateTime.UtcNow - Model.LastSeenUtc;
            return age.TotalSeconds < 60
                ? $"{Math.Max(0, age.Seconds)}s ago"
                : $"{Math.Max(0, (int)age.TotalMinutes)}m ago";
        }
    }
}
