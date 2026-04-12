using AdsbObserver.Core.Models;

namespace AdsbObserver.App.ViewModels;

public sealed class TrackViewModel(AircraftTrack model)
{
    public AircraftTrack Model { get; } = model;
    public string Icao => Model.Icao;
    public string Callsign => string.IsNullOrWhiteSpace(Model.Callsign) ? "N/A" : Model.Callsign!;
    public string Registration => Model.Recognition?.Registration ?? "N/A";
    public string AircraftType => Model.Recognition?.AircraftType ?? "N/A";
    public string Operator => Model.Recognition?.Operator ?? "N/A";
    public string Country => Model.Recognition?.Country ?? "N/A";
    public string PositionText => Model.HasPosition ? $"{Model.Latitude:F4}, {Model.Longitude:F4}" : "No position";
    public string AltitudeText => Model.AltitudeFeet.HasValue ? $"{Model.AltitudeFeet:N0} ft" : "N/A";
    public string SpeedText => Model.GroundSpeedKnots.HasValue ? $"{Model.GroundSpeedKnots:N0} kt" : "N/A";
    public string HeadingText => Model.HeadingDegrees.HasValue ? $"{Model.HeadingDegrees:N0}°" : "N/A";
    public string Squawk => Model.Squawk ?? "N/A";
    public string EmitterCategory => Model.EmitterCategory ?? "N/A";
    public string LastSeenText => Model.LastSeenUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}
