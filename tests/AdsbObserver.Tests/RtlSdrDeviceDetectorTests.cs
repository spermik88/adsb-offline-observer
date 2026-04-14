using AdsbObserver.Infrastructure.Services;
using Xunit;

namespace AdsbObserver.Tests;

public sealed class RtlSdrDeviceDetectorTests
{
    [Fact]
    public void ParsePnpUtilOutput_DetectsRtl2838ViaLibwdiDriver()
    {
        var output = """
            Microsoft PnP Utility

            Instance ID:                USB\VID_0BDA&PID_2838\00000001
            Device Description:         RTL2838UHIDIR
            Class Name:                 USBDevice
            Status:                     Started
            Driver Name:                oem175.inf
            Matching Drivers:
                Driver Name:            oem175.inf
                Original Name:          rtl2838uhidir.inf
                Provider Name:          libwdi
                Matching Device ID:     USB\VID_0BDA&PID_2838
                Driver Status:          Best Ranked / Installed
            """;

        var devices = RtlSdrDeviceDetector.ParsePnpUtilOutput(output);

        var device = Assert.Single(devices);
        Assert.Equal("RTL2838UHIDIR", device.Name);
        Assert.Equal(@"USB\VID_0BDA&PID_2838\00000001", device.DeviceId);
        Assert.Equal("oem175.inf", device.DriverName);
        Assert.True(device.IsDriverReady);
    }

    [Fact]
    public void ParsePnpUtilOutput_DetectsDeviceWhenServiceNameIsMissing()
    {
        var output = """
            Instance ID:                USB\VID_0BDA&PID_2838\00000001
            Device Description:         RTL2838UHIDIR
            Driver Name:                oem175.inf
            Matching Drivers:
                Original Name:          rtl2838uhidir.inf
                Provider Name:          libwdi
            """;

        var devices = RtlSdrDeviceDetector.ParsePnpUtilOutput(output);

        var device = Assert.Single(devices);
        Assert.Null(device.ServiceName);
    }

    [Fact]
    public void ParsePnpUtilOutput_DoesNotOverrideInstalledDriverWithMatchingDriverSection()
    {
        var output = """
            Instance ID:                USB\VID_0BDA&PID_2838\00000001
            Device Description:         RTL2838UHIDIR
            Driver Name:                oem175.inf
            Matching Drivers:
                Driver Name:            usb.inf
                Provider Name:          Microsoft
                Matching Device ID:     USB\Class_FF
            """;

        var devices = RtlSdrDeviceDetector.ParsePnpUtilOutput(output);

        var device = Assert.Single(devices);
        Assert.Equal("oem175.inf", device.DriverName);
    }

    [Fact]
    public void ParsePnpUtilOutput_ReturnsEmptyListWhenNoRtlDevicesExist()
    {
        var output = """
            Instance ID:                USB\VID_8087&PID_0024\5&27e8c4b3&0&1
            Device Description:         Generic USB Hub
            Driver Name:                usb.inf
            Service Name:               USBHUB3
            """;

        var devices = RtlSdrDeviceDetector.ParsePnpUtilOutput(output);

        Assert.Empty(devices);
    }
}
