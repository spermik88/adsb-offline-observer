namespace AdsbObserver.Core.Models;

public enum LiveEnvironmentIssue
{
    None,
    NoCompatibleDevice,
    MultipleDevicesDetected,
    DriverMissing,
    DriverInstallFailed,
    DriverInstallCancelled,
    BackendMissing,
    PortBusy
}
