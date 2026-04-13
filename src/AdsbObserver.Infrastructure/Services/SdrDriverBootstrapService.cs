using System.Net.Sockets;
using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Models;

namespace AdsbObserver.Infrastructure.Services;

public sealed class SdrDriverBootstrapService(IDeviceDetector deviceDetector) : ISdrDriverBootstrapService
{
    public async Task<LiveEnvironmentStatus> InspectAsync(ObservationSettings settings, CancellationToken cancellationToken)
    {
        var devices = await deviceDetector.DetectAsync(cancellationToken);
        var compatibleDevices = devices.Where(device => device.IsCompatible).ToList();
        var selectedDevice = ResolveSelectedDevice(compatibleDevices, settings.PreferredDeviceId);
        var driverReady = compatibleDevices.Any(item => item.IsDriverReady);
        var backendPath = BundledAssetPathResolver.ResolveDecoderExecutable(settings);
        var backendAvailable = !string.IsNullOrWhiteSpace(backendPath) && File.Exists(backendPath);
        var portReachable = await IsPortReachableAsync(settings.DecoderHost, settings.DecoderPort, cancellationToken);
        const bool canBootstrapDriver = false;

        if (selectedDevice is null)
        {
            return new LiveEnvironmentStatus(
                LiveEnvironmentIssue.NoCompatibleDevice,
                false,
                false,
                backendAvailable,
                portReachable,
                canBootstrapDriver,
                false,
                true,
                DriverBootstrapOutcome.None,
                "Совместимый RTL-SDR не найден.",
                "Подключите заранее подготовленный RTL-SDR, если нужен live-режим. Playback и история доступны без приемника.");
        }

        if (compatibleDevices.Count > 1 && string.IsNullOrWhiteSpace(settings.PreferredDeviceId))
        {
            return new LiveEnvironmentStatus(
                LiveEnvironmentIssue.MultipleDevicesDetected,
                true,
                driverReady,
                backendAvailable,
                portReachable,
                canBootstrapDriver,
                false,
                true,
                DriverBootstrapOutcome.None,
                "Обнаружено несколько совместимых RTL-SDR.",
                "Оставьте один донгл или выберите предпочтительное устройство в настройках.",
                selectedDevice.Name,
                selectedDevice.DriverName ?? selectedDevice.ServiceName);
        }

        if (!selectedDevice.IsDriverReady)
        {
            return new LiveEnvironmentStatus(
                LiveEnvironmentIssue.DriverMissing,
                true,
                false,
                backendAvailable,
                portReachable,
                canBootstrapDriver,
                false,
                true,
                DriverBootstrapOutcome.ManualActionRequired,
                "RTL-SDR обнаружен, но драйвер WinUSB/libusb не готов.",
                "Подготовьте драйвер донгла вне программы и повторите проверку. Portable-версия не устанавливает драйверы.",
                selectedDevice.Name,
                selectedDevice.DriverName ?? selectedDevice.ServiceName);
        }

        if (!backendAvailable)
        {
            return new LiveEnvironmentStatus(
                LiveEnvironmentIssue.BackendMissing,
                true,
                true,
                false,
                portReachable,
                canBootstrapDriver,
                false,
                true,
                DriverBootstrapOutcome.NotNeeded,
                "В portable-пакете отсутствует bundled backend readsb.",
                "Проверьте, что readsb.exe включен в portable release layout.",
                selectedDevice.Name,
                selectedDevice.DriverName ?? selectedDevice.ServiceName);
        }

        if (portReachable)
        {
            return new LiveEnvironmentStatus(
                LiveEnvironmentIssue.PortBusy,
                true,
                true,
                true,
                true,
                canBootstrapDriver,
                true,
                false,
                DriverBootstrapOutcome.NotNeeded,
                "Live-окружение готово. SBS-1 порт уже доступен.",
                "На указанном SBS-1 порту уже есть поток. Live-режим может подключиться без запуска второго backend.",
                selectedDevice.Name,
                selectedDevice.DriverName ?? selectedDevice.ServiceName);
        }

        return new LiveEnvironmentStatus(
            LiveEnvironmentIssue.None,
            true,
            true,
            true,
            false,
            canBootstrapDriver,
            true,
            false,
            DriverBootstrapOutcome.NotNeeded,
            "Live-окружение готово. Bundled backend можно запускать по требованию.",
            "Нажмите Start Live, чтобы запустить bundled readsb и начать прием ADS-B.",
            selectedDevice.Name,
            selectedDevice.DriverName ?? selectedDevice.ServiceName);
    }

    public Task<LiveEnvironmentStatus> EnsureReadyAsync(ObservationSettings settings, CancellationToken cancellationToken) =>
        InspectAsync(settings, cancellationToken);

    private static SdrDeviceInfo? ResolveSelectedDevice(IReadOnlyList<SdrDeviceInfo> compatibleDevices, string? preferredDeviceId)
    {
        if (compatibleDevices.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(preferredDeviceId))
        {
            return compatibleDevices[0];
        }

        return compatibleDevices.FirstOrDefault(device => device.DeviceId == preferredDeviceId) ?? compatibleDevices[0];
    }

    private static async Task<bool> IsPortReachableAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cancellationToken);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
