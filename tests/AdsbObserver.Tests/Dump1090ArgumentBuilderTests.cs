using AdsbObserver.Core.Models;
using AdsbObserver.Infrastructure.Services;
using Xunit;

namespace AdsbObserver.Tests;

public sealed class Dump1090ArgumentBuilderTests
{
    [Fact]
    public void Build_IncludesCoreDump1090Arguments()
    {
        var settings = new ObservationSettings
        {
            Gain = 49.6,
            PpmCorrection = 12,
            SampleRate = 2_400_000,
            DecoderPort = 30003,
            PreferredDeviceId = "USB\\VID_0BDA&PID_2838"
        };

        var arguments = Dump1090ArgumentBuilder.Build(settings, @"C:\portable\backend\dump1090\dump1090.runtime.cfg");

        Assert.Contains("--net", arguments);
        Assert.Contains("--config \"C:\\portable\\backend\\dump1090\\dump1090.runtime.cfg\"", arguments);
        Assert.Contains("--samplerate \"2400000\"", arguments);
        Assert.Contains("--gain \"49.6\"", arguments);
        Assert.Contains("--rtlsdr-ppm \"12\"", arguments);
        Assert.Contains("--net-sbs-port \"30003\"", arguments);
        Assert.Contains("--device \"USB\\VID_0BDA&PID_2838\"", arguments);
    }
}
