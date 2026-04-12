namespace AdsbObserver.Core.Models;

public sealed record DecoderProcessStatus(
    DecoderProcessState State,
    string Message,
    bool IsReady = false,
    DecoderFailureReason FailureReason = DecoderFailureReason.None,
    bool PortReachable = false,
    string? ExecutablePath = null,
    string? LastOutputLine = null,
    string? LastErrorLine = null);
