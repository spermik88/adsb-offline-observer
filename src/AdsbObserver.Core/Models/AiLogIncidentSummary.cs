namespace AdsbObserver.Core.Models;

public sealed record AiLogIncidentSummary(
    string SessionId,
    DateTime LastUpdatedUtc,
    string? LastActionId,
    string? LastOperationId,
    string? LastResult,
    string? LastDecoderFailureReason,
    int IncidentMarkers,
    int ErrorCount,
    int ExceptionCount,
    int DecoderFailureCount,
    int SimulationFallbackCount,
    string? LastErrorMessage,
    string? LastExceptionMessage,
    IReadOnlyList<string> RecentKeyEvents,
    string? BackendLogPath);
