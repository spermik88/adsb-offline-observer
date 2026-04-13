using AdsbObserver.Core.Models;
using AdsbObserver.Infrastructure.Services;
using Xunit;

namespace AdsbObserver.Tests;

public sealed class ReadsbArgumentBuilderTests
{
    [Fact]
    public void Build_IncludesCoreDump1090Arguments()
    {
        var settings = new ObservationSettings
        {
            CenterLatitude = 25.2048,
            CenterLongitude = 55.2708,
            Gain = 49.6,
            PpmCorrection = 12,
            SampleRate = 2_400_000,
            DecoderPort = 30003,
            PreferredDeviceId = "USB\\VID_0BDA&PID_2838"
        };

        var arguments = ReadsbArgumentBuilder.Build(settings);

        Assert.Contains("--net", arguments);
        Assert.Contains("--samplerate \"2400000\"", arguments);
        Assert.Contains("--device \"USB\\VID_0BDA&PID_2838\"", arguments);
        Assert.Contains("--config dump1090.cfg", arguments);
    }
}
