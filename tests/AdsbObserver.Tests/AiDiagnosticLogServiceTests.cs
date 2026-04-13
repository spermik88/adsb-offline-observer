using System.IO;
using System.Text.Json;
using AdsbObserver.Core.Models;
using AdsbObserver.Infrastructure.Services;
using Xunit;

namespace AdsbObserver.Tests;

public sealed class AiDiagnosticLogServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "adsbobserver-ai-logs-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StartSessionAsync_CreatesSessionBundle()
    {
        var workspace = CreateWorkspace();
        var service = new AiDiagnosticLogService();

        await service.StartSessionAsync(workspace, new ObservationSettings { AiLogsEnabled = true }, CancellationToken.None);

        var sessionPath = service.GetCurrentSessionPath();
        Assert.False(string.IsNullOrWhiteSpace(sessionPath));
        Assert.True(Directory.Exists(sessionPath!));
        Assert.True(File.Exists(Path.Combine(sessionPath!, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(sessionPath, "environment.json")));
        Assert.True(File.Exists(Path.Combine(sessionPath, "settings.json")));
        Assert.True(File.Exists(Path.Combine(sessionPath, "incident_summary.json")));
    }

    [Fact]
    public async Task LogEventAndException_WriteJsonLines()
    {
        var workspace = CreateWorkspace();
        var service = new AiDiagnosticLogService();
        await service.StartSessionAsync(workspace, new ObservationSettings { AiLogsEnabled = true }, CancellationToken.None);
        var sessionPath = service.GetCurrentSessionPath()!;

        await service.LogEventAsync("ui.command", "info", "test", "started", new { value = 1 }, "a1", "o1");
        await service.LogExceptionAsync(new InvalidOperationException("boom"), "test", "failed", actionId: "a1", operationId: "o1");

        var eventLine = File.ReadLines(Path.Combine(sessionPath, "events.jsonl")).Last();
        using var eventDoc = JsonDocument.Parse(eventLine);
        Assert.Equal("exception", eventDoc.RootElement.GetProperty("EventType").GetString());

        var exceptionLine = File.ReadLines(Path.Combine(sessionPath, "exceptions.jsonl")).Last();
        using var exceptionDoc = JsonDocument.Parse(exceptionLine);
        Assert.Equal("test", exceptionDoc.RootElement.GetProperty("Component").GetString());
    }

    [Fact]
    public async Task DisabledSettings_DoNotCreateSession()
    {
        var workspace = CreateWorkspace();
        var service = new AiDiagnosticLogService();

        await service.StartSessionAsync(workspace, new ObservationSettings { AiLogsEnabled = false }, CancellationToken.None);

        Assert.Null(service.GetCurrentSessionPath());
        Assert.False(Directory.Exists(Path.Combine(workspace.LogsRoot, "logs_for_Ai")));
    }

    [Fact]
    public async Task MarkIncident_UpdatesSummary()
    {
        var workspace = CreateWorkspace();
        var service = new AiDiagnosticLogService();
        await service.StartSessionAsync(workspace, new ObservationSettings { AiLogsEnabled = true }, CancellationToken.None);
        var sessionPath = service.GetCurrentSessionPath()!;

        await service.MarkIncidentAsync("User marked incident", actionId: "incident-1");

        var summaryJson = await File.ReadAllTextAsync(Path.Combine(sessionPath, "incident_summary.json"));
        using var doc = JsonDocument.Parse(summaryJson);
        Assert.Equal("incident-1", doc.RootElement.GetProperty("LastActionId").GetString());
        Assert.True(doc.RootElement.GetProperty("IncidentMarkers").GetInt32() > 0);
    }

    private PortableWorkspacePaths CreateWorkspace()
    {
        Directory.CreateDirectory(_root);
        var portable = Path.Combine(_root, "portable");
        var logs = Path.Combine(portable, "logs");
        Directory.CreateDirectory(logs);
        return new PortableWorkspacePaths
        {
            AppRoot = _root,
            PortableRoot = portable,
            DataRoot = Path.Combine(portable, "data"),
            MapsRoot = Path.Combine(portable, "maps"),
            RecordingsRoot = Path.Combine(portable, "recordings"),
            LogsRoot = logs
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
