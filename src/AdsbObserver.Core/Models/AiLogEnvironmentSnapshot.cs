namespace AdsbObserver.Core.Models;

public sealed record AiLogEnvironmentSnapshot(
    string SessionId,
    DateTime CapturedUtc,
    string AppRoot,
    string PortableRoot,
    string DataRoot,
    string MapsRoot,
    string RecordingsRoot,
    string LogsRoot,
    string AiLogsRoot,
    string CurrentDirectory,
    int ProcessId,
    string RuntimeVersion,
    string? BackendLogPath);
