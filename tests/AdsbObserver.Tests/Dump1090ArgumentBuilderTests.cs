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
        Assert.DoesNotContain("--samplerate", arguments);
        Assert.DoesNotContain("--gain", arguments);
        Assert.DoesNotContain("--rtlsdr-ppm", arguments);
        Assert.DoesNotContain("--net-sbs-port", arguments);
        Assert.DoesNotContain("--device", arguments);
    }

    [Fact]
    public void Build_IncludesDevice_WhenSelectorLooksLikeBackendDeviceName()
    {
        var settings = new ObservationSettings
        {
            PreferredDeviceId = "RTL2838-silver"
        };

        var arguments = Dump1090ArgumentBuilder.Build(settings, @"C:\portable\backend\dump1090\dump1090.runtime.cfg");

        Assert.Contains("--device \"RTL2838-silver\"", arguments);
    }
}
