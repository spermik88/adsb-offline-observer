namespace AdsbObserver.Core.Models;

public sealed record AiLogIncidentSummary(
    string SessionId,
    DateTime LastUpdatedUtc,
    string? LastActionId,
    int IncidentMarkers,
    int ErrorCount,
    int ExceptionCount,
    int DecoderFailureCount,
    int SimulationFallbackCount,
    string? LastErrorMessage,
    string? LastExceptionMessage,
    string? BackendLogPath);
