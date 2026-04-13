using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Models;

namespace AdsbObserver.Infrastructure.Services;

public sealed class AiDiagnosticLogService : IAiDiagnosticLogService
{
    private const int FormatVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _sync = new();
    private readonly JsonSerializerOptions _lineOptions = new(JsonOptions) { WriteIndented = false };
    private bool _enabled;
    private bool _degraded;
    private string? _sessionId;
    private string? _sessionPath;
    private string? _eventsPath;
    private string? _exceptionsPath;
    private string? _settingsPath;
    private string? _incidentSummaryPath;
    private AiLogIncidentSummary? _summary;

    public Task StartSessionAsync(PortableWorkspacePaths workspace, ObservationSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.AiLogsEnabled)
        {
            Disable();
            return Task.CompletedTask;
        }

        try
        {
            var aiLogsRoot = Path.Combine(workspace.LogsRoot, "logs_for_Ai");
            Directory.CreateDirectory(aiLogsRoot);

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
                0,
                0,
                0,
                0,
                0,
                null,
                null,
                environment.BackendLogPath);

            lock (_sync)
            {
                _enabled = true;
                _degraded = false;
                _sessionId = sessionId;
                _sessionPath = sessionPath;
                _eventsPath = Path.Combine(sessionPath, "events.jsonl");
                _exceptionsPath = Path.Combine(sessionPath, "exceptions.jsonl");
                _settingsPath = Path.Combine(sessionPath, "settings.json");
                _incidentSummaryPath = Path.Combine(sessionPath, "incident_summary.json");
                _summary = summary;
            }

            File.WriteAllText(Path.Combine(sessionPath, "manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions), Encoding.UTF8);
            File.WriteAllText(Path.Combine(sessionPath, "environment.json"), JsonSerializer.Serialize(environment, JsonOptions), Encoding.UTF8);
            File.WriteAllText(_settingsPath!, JsonSerializer.Serialize(settings, JsonOptions), Encoding.UTF8);
            File.WriteAllText(_incidentSummaryPath!, JsonSerializer.Serialize(summary, JsonOptions), Encoding.UTF8);

            return LogEventAsync("app.session", "info", "AiDiagnosticLogService", "AI diagnostics session started",
                new { sessionPath, settings.AiLogsEnabled, environment.BackendLogPath }, cancellationToken: cancellationToken);
        }
        catch
        {
            lock (_sync)
            {
                _enabled = false;
                _degraded = true;
            }

            return Task.CompletedTask;
        }
    }

    public Task LogEventAsync(string eventType, string severity, string component, string message, object? payload = null, string? actionId = null, string? operationId = null, CancellationToken cancellationToken = default)
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
            var entry = new AiLogEvent(DateTime.UtcNow, sessionId, eventType, severity, component, actionId, operationId, message, payload);
            AppendJsonLine(eventsPath, entry);
            UpdateIncidentSummary(eventType, severity, message, null, actionId);
        }
        catch
        {
            lock (_sync)
            {
                _degraded = true;
            }
        }

        return Task.CompletedTask;
    }

    public Task LogExceptionAsync(Exception exception, string component, string message, object? payload = null, string? actionId = null, string? operationId = null, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _degraded)
        {
            return Task.CompletedTask;
        }

        try
        {
            string sessionId;
            string exceptionsPath;
            lock (_sync)
            {
                if (!_enabled || _degraded || _sessionId is null || _exceptionsPath is null)
                {
                    return Task.CompletedTask;
                }

                sessionId = _sessionId;
                exceptionsPath = _exceptionsPath;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var exceptionPayload = new
            {
                exception.GetType().FullName,
                exception.Message,
                exception.StackTrace,
                Payload = payload
            };

            var entry = new AiLogEvent(DateTime.UtcNow, sessionId, "exception", "error", component, actionId, operationId, message, exceptionPayload);
            AppendJsonLine(exceptionsPath, entry);
            if (_eventsPath is not null)
            {
                AppendJsonLine(_eventsPath, entry);
            }

            UpdateIncidentSummary("exception", "error", message, exception.Message, actionId);
        }
        catch
        {
            lock (_sync)
            {
                _degraded = true;
            }
        }

        return Task.CompletedTask;
    }

    public Task MarkIncidentAsync(string message, object? payload = null, string? actionId = null, CancellationToken cancellationToken = default) =>
        LogEventAsync("error", "warning", "AiDiagnosticLogService", message, new { incident = true, payload }, actionId, cancellationToken: cancellationToken);

    public Task UpdateSettingsSnapshotAsync(ObservationSettings settings, CancellationToken cancellationToken)
    {
        if (!_enabled || _degraded)
        {
            return Task.CompletedTask;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? settingsPath;
            lock (_sync)
            {
                settingsPath = _settingsPath;
            }

            if (!string.IsNullOrWhiteSpace(settingsPath))
            {
                File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, JsonOptions), Encoding.UTF8);
            }
        }
        catch
        {
            lock (_sync)
            {
                _degraded = true;
            }
        }

        return Task.CompletedTask;
    }

    public Task FlushAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
        }
    }

    private void UpdateIncidentSummary(string eventType, string severity, string message, string? exceptionMessage, string? actionId)
    {
        lock (_sync)
        {
            if (_summary is null || _incidentSummaryPath is null)
            {
                return;
            }

            var next = _summary with
            {
                LastUpdatedUtc = DateTime.UtcNow,
                LastActionId = actionId ?? _summary.LastActionId,
                IncidentMarkers = _summary.IncidentMarkers + (message.Contains("incident", StringComparison.OrdinalIgnoreCase) || eventType == "error" && severity == "warning" ? 1 : 0),
                ErrorCount = _summary.ErrorCount + ((severity.Equals("error", StringComparison.OrdinalIgnoreCase) || eventType == "error") ? 1 : 0),
                ExceptionCount = _summary.ExceptionCount + (eventType == "exception" ? 1 : 0),
                DecoderFailureCount = _summary.DecoderFailureCount + (eventType == "live.decoder" && severity.Equals("error", StringComparison.OrdinalIgnoreCase) ? 1 : 0),
                SimulationFallbackCount = _summary.SimulationFallbackCount + (message.Contains("fallback", StringComparison.OrdinalIgnoreCase) ? 1 : 0),
                LastErrorMessage = severity.Equals("error", StringComparison.OrdinalIgnoreCase) || eventType == "error" ? message : _summary.LastErrorMessage,
                LastExceptionMessage = exceptionMessage ?? _summary.LastExceptionMessage
            };

            _summary = next;
            File.WriteAllText(_incidentSummaryPath, JsonSerializer.Serialize(next, JsonOptions), Encoding.UTF8);
        }
    }

    private void AppendJsonLine(string path, AiLogEvent entry)
    {
        var line = JsonSerializer.Serialize(entry, _lineOptions) + Environment.NewLine;
        File.AppendAllText(path, line, Encoding.UTF8);
    }
}
