using AdsbObserver.Core.Models;

namespace AdsbObserver.Core.Interfaces;

public interface IAiDiagnosticLogService
{
    Task StartSessionAsync(PortableWorkspacePaths workspace, ObservationSettings settings, CancellationToken cancellationToken);
    Task LogEventAsync(string eventType, string severity, string component, string message, object? payload = null, string? actionId = null, string? operationId = null, CancellationToken cancellationToken = default);
    Task LogExceptionAsync(Exception exception, string component, string message, object? payload = null, string? actionId = null, string? operationId = null, CancellationToken cancellationToken = default);
    Task MarkIncidentAsync(string message, object? payload = null, string? actionId = null, CancellationToken cancellationToken = default);
    Task UpdateSettingsSnapshotAsync(ObservationSettings settings, CancellationToken cancellationToken);
    Task FlushAsync(CancellationToken cancellationToken);
    string? GetCurrentSessionPath();
    void Disable();
}
