namespace AdsbObserver.Core.Models;

public sealed record AircraftMessage(
    string Icao,
    DateTime TimestampUtc,
    string? Callsign = null,
    double? Latitude = null,
    double? Longitude = null,
    int? AltitudeFeet = null,
    double? GroundSpeedKnots = null,
    double? HeadingDegrees = null,
    int? VerticalRateFeetPerMinute = null,
    string? Squawk = null,
    string? EmitterCategory = null);
