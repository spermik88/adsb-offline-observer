namespace AdsbObserver.Core.Models;

public sealed class ObservationSettings
{
    public double CenterLatitude { get; set; } = 25.2048;
    public double CenterLongitude { get; set; } = 55.2708;
    public double DisplayRadiusKilometers { get; set; } = 200;
    public int ActiveTargetTimeoutMinutes { get; set; } = 5;
    public int DefaultZoom { get; set; } = 8;
    public int MinZoom { get; set; } = 5;
    public int MaxZoom { get; set; } = 12;
    public int SampleRate { get; set; } = 2_000_000;
    public double Gain { get; set; } = 49.6;
    public int PpmCorrection { get; set; }
    public string? PreferredDeviceId { get; set; }
    public string DecoderHost { get; set; } = "127.0.0.1";
    public int DecoderPort { get; set; } = 30003;
    public bool DecoderAutoStart { get; set; } = true;
    public bool PreferBundledDecoder { get; set; } = true;
    public string BundledDecoderRelativePath { get; set; } = @"backend\dump1090\dump1090.exe";
    public string? DecoderExecutablePath { get; set; }
    public string? DecoderArguments { get; set; } = "--config dump1090.cfg";
    public string BundledDriverSetupRelativePath { get; set; } = @"drivers\rtl-sdr\install-driver.cmd";
    public string BundledDriverInfRelativePath { get; set; } = @"drivers\rtl-sdr\rtlsdr-winusb.inf";
    public bool UseSimulationFallback { get; set; } = true;
}
