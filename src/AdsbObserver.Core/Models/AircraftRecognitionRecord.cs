namespace AdsbObserver.Core.Models;

public sealed record AircraftRecognitionRecord(
    string Icao,
    string? Registration,
    string? AircraftType,
    string? Operator,
    string? Country);
