namespace AdsbObserver.Core.Models;

public sealed class AircraftTrack
{
    public string Icao { get; init; } = string.Empty;
    public string? Callsign { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? AltitudeFeet { get; set; }
    public double? GroundSpeedKnots { get; set; }
    public double? HeadingDegrees { get; set; }
    public int? VerticalRateFeetPerMinute { get; set; }
    public string? Squawk { get; set; }
    public string? EmitterCategory { get; set; }
    public AircraftRecognitionRecord? Recognition { get; set; }
    public List<AircraftTrackPoint> Points { get; } = [];

    public bool HasPosition => Latitude.HasValue && Longitude.HasValue;
}
