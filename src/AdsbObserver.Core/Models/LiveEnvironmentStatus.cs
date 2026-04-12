namespace AdsbObserver.Core.Models;

public sealed record LiveEnvironmentStatus(
    LiveEnvironmentIssue Issue,
    bool DeviceDetected,
    bool DriverInstalled,
    bool BackendAvailable,
    bool PortReachable,
    bool CanBootstrapDriver,
    bool CanStartLive,
    bool RequiresUserAction,
    DriverBootstrapOutcome BootstrapOutcome,
    string Message,
    string? Guidance = null,
    string? DeviceName = null,
    string? DriverHint = null);
