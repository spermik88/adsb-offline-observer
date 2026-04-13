using AdsbObserver.Core.Models;

namespace AdsbObserver.Core.Interfaces;

public interface IAiDiagnosticLogService
{
    Task StartSessionAsync(PortableWorkspacePaths workspace, ObservationSettings settings, CancellationToken cancellationToken);
    Task LogEventAsync(string eventType, string scope, string severity, string component, string message, object? payload = null, string? actionId = null, string? operationId = null, string? result = null, double? durationMs = null, string? errorCode = null, CancellationToken cancellationToken = default);
    Task LogExceptionAsync(Exception exception, string component, string message, object? payload = null, string? actionId = null, string? operationId = null, string? errorCode = null, CancellationToken cancellationToken = default);
    string BeginOperation(string eventType, string scope, string component, string message, object? payload = null, string? actionId = null);
    Task CompleteOperationAsync(string eventType, string scope, string component, string operationId, string message, object? payload = null, string? actionId = null, double? durationMs = null, CancellationToken cancellationToken = default);
    Task FailOperationAsync(string eventType, string scope, string component, string operationId, string message, object? payload = null, string? actionId = null, string? errorCode = null, double? durationMs = null, CancellationToken cancellationToken = default);
    Task MarkIncidentAsync(string message, object? payload = null, string? actionId = null, CancellationToken cancellationToken = default);
    Task UpdateSettingsSnapshotAsync(ObservationSettings settings, CancellationToken cancellationToken);
    Task FlushAsync(CancellationToken cancellationToken);
    string? GetCurrentSessionPath();
    void Disable();
}
