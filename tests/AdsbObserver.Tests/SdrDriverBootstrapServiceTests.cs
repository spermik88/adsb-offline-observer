using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Models;
using AdsbObserver.Infrastructure.Services;
using Xunit;

namespace AdsbObserver.Tests;

public sealed class SdrDriverBootstrapServiceTests
{
    [Fact]
    public async Task InspectAsync_ReturnsNoDevice_WhenNothingDetected()
    {
        var service = new SdrDriverBootstrapService(new StubDeviceDetector([]));

        var status = await service.InspectAsync(new ObservationSettings(), CancellationToken.None);

        Assert.Equal(LiveEnvironmentIssue.NoCompatibleDevice, status.Issue);
        Assert.False(status.DeviceDetected);
        Assert.False(status.CanStartLive);
    }

    [Fact]
    public async Task InspectAsync_ReturnsMultipleDevices_WhenNoPreferenceConfigured()
    {
        var devices = new[]
        {
            new SdrDeviceInfo("RTL A", "A", true, "WinUSB", "WinUSB", true),
            new SdrDeviceInfo("RTL B", "B", true, "WinUSB", "WinUSB", true)
        };
        var service = new SdrDriverBootstrapService(new StubDeviceDetector(devices));

        var status = await service.InspectAsync(new ObservationSettings(), CancellationToken.None);

        Assert.Equal(LiveEnvironmentIssue.MultipleDevicesDetected, status.Issue);
        Assert.False(status.CanStartLive);
    }

    [Fact]
    public async Task InspectAsync_AllowsConfiguredPreferredDevice_WhenMultipleDevicesExist()
    {
        var devices = new[]
        {
            new SdrDeviceInfo("RTL A", "A", true, "WinUSB", "WinUSB", true),
            new SdrDeviceInfo("RTL B", "B", true, "WinUSB", "WinUSB", true)
        };
        var service = new SdrDriverBootstrapService(new StubDeviceDetector(devices));

        var status = await service.InspectAsync(new ObservationSettings
        {
            PreferredDeviceId = "B"
        }, CancellationToken.None);

        Assert.NotEqual(LiveEnvironmentIssue.MultipleDevicesDetected, status.Issue);
    }

    [Fact]
    public async Task InspectAsync_ReturnsDriverMissing_WhenDeviceDriverIsNotReady()
    {
        var devices = new[]
        {
            new SdrDeviceInfo("RTL A", "A", true, "OEM", "OEM", false)
        };
        var service = new SdrDriverBootstrapService(new StubDeviceDetector(devices));

        var status = await service.InspectAsync(new ObservationSettings(), CancellationToken.None);

        Assert.Equal(LiveEnvironmentIssue.DriverMissing, status.Issue);
        Assert.False(status.DriverInstalled);
    }

    private sealed class StubDeviceDetector(IReadOnlyList<SdrDeviceInfo> devices) : IDeviceDetector
    {
        public Task<IReadOnlyList<SdrDeviceInfo>> DetectAsync(CancellationToken cancellationToken) =>
            Task.FromResult(devices);
    }
}
