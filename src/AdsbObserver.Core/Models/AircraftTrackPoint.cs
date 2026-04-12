namespace AdsbObserver.Core.Models;

public sealed record AircraftTrackPoint(
    DateTime TimestampUtc,
    double Latitude,
    double Longitude,
    int? AltitudeFeet,
    double? GroundSpeedKnots,
    double? HeadingDegrees,
    int? VerticalRateFeetPerMinute);
