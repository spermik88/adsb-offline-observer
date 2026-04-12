namespace AdsbObserver.Core.Models;

public sealed record SdrDeviceInfo(
    string Name,
    string DeviceId,
    bool IsCompatible,
    string? DriverName = null,
    string? ServiceName = null,
    bool IsDriverReady = false);
