namespace AdsbObserver.Core.Models;

public sealed record AiLogSessionManifest(
    int FormatVersion,
    string SessionId,
    DateTime StartedUtc,
    string AppVersion,
    string MachineName,
    string OsDescription,
    string FrameworkDescription,
    string AppRoot,
    string SessionPath);
