namespace AdsbObserver.Core.Models;

public enum DecoderFailureReason
{
    None,
    AutoStartDisabled,
    BackendMissing,
    PortBusy,
    PortUnavailable,
    ProcessExitedEarly,
    StartFailed
}
