namespace AdsbObserver.Core.Models;

public sealed record AiLogEvent(
    DateTime TimestampUtc,
    string SessionId,
    string EventType,
    string Severity,
    string Component,
    string? ActionId,
    string? OperationId,
    string Message,
    object? Payload);
