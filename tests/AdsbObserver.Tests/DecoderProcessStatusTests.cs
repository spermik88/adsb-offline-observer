using AdsbObserver.Core.Models;
using Xunit;

namespace AdsbObserver.Tests;

public sealed class DecoderProcessStatusTests
{
    [Fact]
    public void Status_CanRepresentPortUnavailableFailure()
    {
        var status = new DecoderProcessStatus(
            DecoderProcessState.Failed,
            "Port unavailable",
            FailureReason: DecoderFailureReason.PortUnavailable,
            PortReachable: false);

        Assert.Equal(DecoderFailureReason.PortUnavailable, status.FailureReason);
        Assert.False(status.PortReachable);
    }

    [Fact]
    public void Status_CanRepresentProcessExitedEarly()
    {
        var status = new DecoderProcessStatus(
            DecoderProcessState.Failed,
            "Exited early",
            FailureReason: DecoderFailureReason.ProcessExitedEarly);

        Assert.Equal(DecoderFailureReason.ProcessExitedEarly, status.FailureReason);
        Assert.False(status.IsReady);
    }

    [Fact]
    public void Status_CanRepresentBackendMissing()
    {
        var status = new DecoderProcessStatus(
            DecoderProcessState.Failed,
            "Backend missing",
            FailureReason: DecoderFailureReason.BackendMissing,
            ExecutablePath: @"backend\dump1090\dump1090.exe");

        Assert.Equal(DecoderFailureReason.BackendMissing, status.FailureReason);
        Assert.Equal(@"backend\dump1090\dump1090.exe", status.ExecutablePath);
    }
}
