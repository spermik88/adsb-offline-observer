using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Models;

namespace AdsbObserver.Infrastructure.Services;

public sealed class AiDiagnosticLogService : IAiDiagnosticLogService
{
    private const int FormatVersion = 2;
    private const int RecentKeyEventsLimit = 8;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _sync = new();
    private readonly JsonSerializerOptions _lineOptions = new(JsonOptions) { WriteIndented = false };
    private readonly Dictionary<string, DateTime> _startedOperations = new(StringComparer.Ordinal);
    private bool _enabled;
    private bool _degraded;
    private string? _degradedReason;
    private string? _sessionId;
    private string? _sessionPath;
    private string? _eventsPath;
    private string? _exceptionsPath;
    private string? _settingsPath;
    private string? _incidentSummaryPath;
    private string? _sessionSummaryPath;
    private AiLogIncidentSummary? _summary;

    public Task StartSessionAsync(PortableWorkspacePaths workspace, ObservationSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.AiLogsEnabled)
        {
            Disable();
            return Task.CompletedTask;
        }

        lock (_sync)
        {
            if (_enabled && !_degraded && !string.IsNullOrWhiteSpace(_sessionPath))
            {
                return Task.CompletedTask;
            }
        }

        try
        {
            var aiLogsRoot = Path.Combine(workspace.LogsRoot, "logs_for_Ai");
            Directory.CreateDirectory(aiLogsRoot);
            EnsureWritable(aiLogsRoot);

            var sessionId = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}";
            var sessionPath = Path.Combine(aiLogsRoot, sessionId);
            Directory.CreateDirectory(sessionPath);

            var manifest = new AiLogSessionManifest(
                FormatVersion,
                sessionId,
                DateTime.UtcNow,
                Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
                Environment.MachineName,
                RuntimeInformation.OSDescription,
                RuntimeInformation.FrameworkDescription,
                workspace.AppRoot,
                sessionPath);

            var backendLogPath = Path.Combine(workspace.LogsRoot, Path.GetFileName(settings.BundledDecoderLogRelativePath));
            var environment = new AiLogEnvironmentSnapshot(
                sessionId,
                DateTime.UtcNow,
                workspace.AppRoot,
                workspace.PortableRoot,
                workspace.DataRoot,
                workspace.MapsRoot,
                workspace.RecordingsRoot,
                workspace.LogsRoot,
                aiLogsRoot,
                Environment.CurrentDirectory,
                Environment.ProcessId,
                Environment.Version.ToString(),
                File.Exists(backendLogPath) ? backendLogPath : null);

            var summary = new AiLogIncidentSummary(
                sessionId,
                DateTime.UtcNow,
                null,
                null,
                null,
                null,
                0,
                0,
                0,
                0,
                0,
                null,
                null,
                Array.Empty<string>(),
                environment.BackendLogPath);

            lock (_sync)
            {
                _enabled = true;
                _degraded = false;
                _degradedReason = null;
                _sessionId = sessionId;
                _sessionPath = sessionPath;
                _eventsPath = Path.Combine(sessionPath, "events.jsonl");
                _exceptionsPath = Path.Combine(sessionPath, "exceptions.jsonl");
                _settingsPath = Path.Combine(sessionPath, "settings.json");
                _incidentSummaryPath = Path.Combine(sessionPath, "incident_summary.json");
                _sessionSummaryPath = Path.Combine(sessionPath, "session_summary.json");
                _summary = summary;
                _startedOperations.Clear();
            }

            File.WriteAllText(Path.Combine(sessionPath, "manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions), Encoding.UTF8);
            File.WriteAllText(Path.Combine(sessionPath, "environment.json"), JsonSerializer.Serialize(environment, JsonOptions), Encoding.UTF8);
            File.WriteAllText(Path.Combine(sessionPath, "backend_log_reference.json"), JsonSerializer.Serialize(new { environment.BackendLogPath }, JsonOptions), Encoding.UTF8);
            WriteSettingsSnapshot(settings);
            PersistSummaries(summary);

            return LogEventAsync(AiLogEventTypes.AppSession, "app", AiLogSeverity.Info, nameof(AiDiagnosticLogService), "AI diagnostics session started",
                new { sessionPath, settings.AiLogsEnabled, environment.BackendLogPath }, result: AiLogResults.Started, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            EnterDegradedMode($"start_session_failed:{ex.GetType().Name}");
            return Task.CompletedTask;
        }
    }

    public Task LogEventAsync(string eventType, string scope, string severity, string component, string message, object? payload = null, string? actionId = null, string? operationId = null, string? result = null, double? durationMs = null, string? errorCode = null, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _degraded)
        {
            return Task.CompletedTask;
        }

        try
        {
            string sessionId;
            string eventsPath;
            lock (_sync)
            {
                if (!_enabled || _degraded || _sessionId is null || _eventsPath is null)
                {
                    return Task.CompletedTask;
                }

                sessionId = _sessionId;
                eventsPath = _eventsPath;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var entry = new AiLogEvent(DateTime.UtcNow, sessionId, eventType, scope, severity, result, component, actionId, operationId, message, durationMs, errorCode, payload);
            AppendJsonLine(eventsPath, entry);
            UpdateIncidentSummary(entry, null);
        }
        catch (Exception ex)
        {
            EnterDegradedMode($"log_event_failed:{ex.GetType().Name}");
        }

        return Task.CompletedTask;
    }

    public Task LogExceptionAsync(Exception exception, string component, string message, object? payload = null, string? actionId = null, string? operationId = null, string? errorCode = null, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _degraded)
        {
            return Task.CompletedTask;
        }

        try
        {
            string sessionId;
            string exceptionsPath;
            string? eventsPath;
            lock (_sync)
            {
                if (!_enabled || _degraded || _sessionId is null || _exceptionsPath is null)
                {
                    return Task.CompletedTask;
                }

                sessionId = _sessionId;
                exceptionsPath = _exceptionsPath;
                eventsPath = _eventsPath;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var exceptionPayload = new
            {
                ExceptionType = exception.GetType().FullName,
                exception.Message,
                exception.StackTrace,
                Payload = payload
            };

            var entry = new AiLogEvent(DateTime.UtcNow, sessionId, AiLogEventTypes.Exception, "runtime", AiLogSeverity.Error, AiLogResults.Failed, component, actionId, operationId, message, null, errorCode, exceptionPayload);
            AppendJsonLine(exceptionsPath, entry);
            if (!string.IsNullOrWhiteSpace(eventsPath))
            {
                AppendJsonLine(eventsPath, entry);
            }

            UpdateIncidentSummary(entry, exception.Message);
        }
        catch (Exception ex)
        {
            EnterDegradedMode($"log_exception_failed:{ex.GetType().Name}");
        }

        return Task.CompletedTask;
    }

    public string BeginOperation(string eventType, string scope, string component, string message, object? payload = null, string? actionId = null)
    {
        var operationId = $"{eventType}-{Guid.NewGuid().ToString("N")[..8]}";
        lock (_sync)
        {
            _startedOperations[operationId] = DateTime.UtcNow;
        }

        _ = LogEventAsync(eventType, scope, AiLogSeverity.Info, component, message, payload, actionId, operationId, AiLogResults.Started);
        return operationId;
    }

    public Task CompleteOperationAsync(string eventType, string scope, string component, string operationId, string message, object? payload = null, string? actionId = null, double? durationMs = null, CancellationToken cancellationToken = default)
        => LogEventAsync(eventType, scope, AiLogSeverity.Info, component, message, payload, actionId, operationId, AiLogResults.Succeeded, durationMs ?? EndOperation(operationId), cancellationToken: cancellationToken);

    public Task FailOperationAsync(string eventType, string scope, string component, string operationId, string message, object? payload = null, string? actionId = null, string? errorCode = null, double? durationMs = null, CancellationToken cancellationToken = default)
        => LogEventAsync(eventType, scope, AiLogSeverity.Error, component, message, payload, actionId, operationId, AiLogResults.Failed, durationMs ?? EndOperation(operationId), errorCode, cancellationToken);

    public Task MarkIncidentAsync(string message, object? payload = null, string? actionId = null, CancellationToken cancellationToken = default) =>
        LogEventAsync(AiLogEventTypes.Error, "incident", AiLogSeverity.Warning, nameof(AiDiagnosticLogService), message, new { incident = true, payload }, actionId, result: AiLogResults.Failed, cancellationToken: cancellationToken);

    public Task UpdateSettingsSnapshotAsync(ObservationSettings settings, CancellationToken cancellationToken)
    {
        if (!_enabled || _degraded)
        {
            return Task.CompletedTask;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteSettingsSnapshot(settings);
        }
        catch (Exception ex)
        {
            EnterDegradedMode($"settings_snapshot_failed:{ex.GetType().Name}");
        }

        return Task.CompletedTask;
    }

    public Task FlushAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_enabled && !_degraded)
        {
            _ = LogEventAsync(AiLogEventTypes.AppSession, "app", AiLogSeverity.Info, nameof(AiDiagnosticLogService), "AI diagnostics session completed", result: AiLogResults.Succeeded);
        }
        return Task.CompletedTask;
    }

    public string? GetCurrentSessionPath()
    {
        lock (_sync)
        {
            return _enabled && !_degraded ? _sessionPath : null;
        }
    }

    public void Disable()
    {
        lock (_sync)
        {
            _enabled = false;
            _startedOperations.Clear();
        }
    }

    private void UpdateIncidentSummary(AiLogEvent entry, string? exceptionMessage)
    {
        lock (_sync)
        {
            if (_summary is null || _incidentSummaryPath is null)
            {
                return;
            }

            var recentKeyEvents = _summary.RecentKeyEvents.ToList();
            if (entry.Result is AiLogResults.Failed or AiLogResults.Succeeded or AiLogResults.Started)
            {
                recentKeyEvents.Insert(0, $"{entry.EventType}:{entry.Result}:{entry.Message}");
                while (recentKeyEvents.Count > RecentKeyEventsLimit)
                {
                    recentKeyEvents.RemoveAt(recentKeyEvents.Count - 1);
                }
            }

            var next = _summary with
            {
                LastUpdatedUtc = DateTime.UtcNow,
                LastActionId = entry.ActionId ?? _summary.LastActionId,
                LastOperationId = entry.OperationId ?? _summary.LastOperationId,
                LastResult = entry.Result ?? _summary.LastResult,
                LastDecoderFailureReason = entry.EventType == AiLogEventTypes.LiveDecoder && !string.IsNullOrWhiteSpace(entry.ErrorCode) ? entry.ErrorCode : _summary.LastDecoderFailureReason,
                IncidentMarkers = _summary.IncidentMarkers + ((entry.EventType == AiLogEventTypes.Error && entry.Severity == AiLogSeverity.Warning) ? 1 : 0),
                ErrorCount = _summary.ErrorCount + (entry.Severity == AiLogSeverity.Error ? 1 : 0),
                ExceptionCount = _summary.ExceptionCount + (entry.EventType == AiLogEventTypes.Exception ? 1 : 0),
                DecoderFailureCount = _summary.DecoderFailureCount + (entry.EventType == AiLogEventTypes.LiveDecoder && entry.Severity == AiLogSeverity.Error ? 1 : 0),
                SimulationFallbackCount = _summary.SimulationFallbackCount + (entry.Message.Contains("fallback", StringComparison.OrdinalIgnoreCase) ? 1 : 0),
                LastErrorMessage = entry.Severity == AiLogSeverity.Error ? entry.Message : _summary.LastErrorMessage,
                LastExceptionMessage = exceptionMessage ?? _summary.LastExceptionMessage,
                RecentKeyEvents = recentKeyEvents
            };

            _summary = next;
            PersistSummaries(next);
        }
    }

    private void PersistSummaries(AiLogIncidentSummary summary)
    {
        if (_incidentSummaryPath is not null)
        {
            File.WriteAllText(_incidentSummaryPath, JsonSerializer.Serialize(summary, JsonOptions), Encoding.UTF8);
        }

        if (_sessionSummaryPath is not null)
        {
            var sessionSummary = new
            {
                summary.SessionId,
                summary.ErrorCount,
                summary.ExceptionCount,
                summary.DecoderFailureCount,
                summary.SimulationFallbackCount,
                PlaybackRuns = summary.RecentKeyEvents.Count(item => item.StartsWith(AiLogEventTypes.Playback, StringComparison.Ordinal)),
                ExportRuns = summary.RecentKeyEvents.Count(item => item.StartsWith(AiLogEventTypes.Export, StringComparison.Ordinal)),
                MapRenders = summary.RecentKeyEvents.Count(item => item.StartsWith(AiLogEventTypes.MapRender, StringComparison.Ordinal)),
                summary.RecentKeyEvents
            };
            File.WriteAllText(_sessionSummaryPath, JsonSerializer.Serialize(sessionSummary, JsonOptions), Encoding.UTF8);
        }
    }

    private void WriteSettingsSnapshot(ObservationSettings settings)
    {
        string? settingsPath;
        lock (_sync)
        {
            settingsPath = _settingsPath;
        }

        if (!string.IsNullOrWhiteSpace(settingsPath))
        {
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(new { savedAtUtc = DateTime.UtcNow, settings }, JsonOptions), Encoding.UTF8);
        }
    }

    private static void EnsureWritable(string directory)
    {
        var probePath = Path.Combine(directory, ".write-test.tmp");
        File.WriteAllText(probePath, "ok");
        File.Delete(probePath);
    }

    private double? EndOperation(string operationId)
    {
        lock (_sync)
        {
            if (_startedOperations.Remove(operationId, out var started))
            {
                return (DateTime.UtcNow - started).TotalMilliseconds;
            }
        }

        return null;
    }

    private void EnterDegradedMode(string reason)
    {
        lock (_sync)
        {
            _degraded = true;
            _degradedReason = reason;
        }
    }

    private void AppendJsonLine(string path, AiLogEvent entry)
    {
        var line = JsonSerializer.Serialize(entry, _lineOptions) + Environment.NewLine;
        File.AppendAllText(path, line, Encoding.UTF8);
    }
}
