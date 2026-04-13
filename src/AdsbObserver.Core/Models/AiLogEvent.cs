namespace AdsbObserver.Core.Models;

public sealed record AiLogEvent(
    DateTime TimestampUtc,
    string SessionId,
    string EventType,
    string Scope,
    string Severity,
    string? Result,
    string Component,
    string? ActionId,
    string? OperationId,
    string Message,
    double? DurationMs,
    string? ErrorCode,
    object? Payload);
