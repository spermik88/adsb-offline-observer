using System.Diagnostics;
using System.ComponentModel;
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
        var device = compatibleDevices.FirstOrDefault();
        var driverReady = compatibleDevices.Any(item => item.IsDriverReady);
        var backendPath = BundledAssetPathResolver.ResolveDecoderExecutable(settings);
        var backendAvailable = !string.IsNullOrWhiteSpace(backendPath) && File.Exists(backendPath);
        var portReachable = await IsPortReachableAsync(settings.DecoderHost, settings.DecoderPort, cancellationToken);
        var canBootstrapDriver =
            File.Exists(BundledAssetPathResolver.ResolveDriverSetupScript(settings)) ||
            File.Exists(BundledAssetPathResolver.ResolveDriverInf(settings));

        if (device is null)
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
                "No compatible RTL-SDR device detected.",
                "Connect a supported RTL-SDR dongle before starting live mode.");
        }

        if (compatibleDevices.Count > 1 && string.IsNullOrWhiteSpace(settings.PreferredDeviceId))
        {
            return new LiveEnvironmentStatus(
                LiveEnvironmentIssue.MultipleDevicesDetected,
                true,
                compatibleDevices.Any(item => item.IsDriverReady),
                backendAvailable,
                portReachable,
                canBootstrapDriver,
                false,
                true,
                DriverBootstrapOutcome.None,
                "Multiple compatible RTL-SDR devices were detected.",
                "Keep one device connected or save settings after selecting the preferred device.",
                device.Name,
                device.DriverName ?? device.ServiceName);
        }

        if (!driverReady)
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
                canBootstrapDriver ? DriverBootstrapOutcome.ManualActionRequired : DriverBootstrapOutcome.Failed,
                canBootstrapDriver
                    ? "RTL-SDR detected, but WinUSB/libusb driver is missing. Guided driver bootstrap is available."
                    : "RTL-SDR detected, but WinUSB/libusb driver is missing.",
                canBootstrapDriver
                    ? "Run Prepare Live to launch the bundled Zadig helper and install the WinUSB driver for the RTL-SDR device."
                    : "Bundle the RTL-SDR driver helper into the installer or install the WinUSB/libusb driver manually.",
                device.Name,
                device.DriverName ?? device.ServiceName);
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
                "Bundled readsb backend is missing from the install layout.",
                "Add readsb.exe to BundledAssets/backend/readsb before publishing the setup.",
                device.Name,
                device.DriverName ?? device.ServiceName);
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
                "Live environment ready. SBS-1 port is already reachable.",
                "The configured SBS-1 port is already serving data. Live mode can connect without starting another backend.",
                device.Name,
                device.DriverName ?? device.ServiceName);
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
            "Live environment ready. Backend can be started on demand.",
            "Press Start Live to launch the bundled readsb backend and begin receiving SBS-1 data.",
            device.Name,
            device.DriverName ?? device.ServiceName);
    }

    public async Task<LiveEnvironmentStatus> EnsureReadyAsync(ObservationSettings settings, CancellationToken cancellationToken)
    {
        var current = await InspectAsync(settings, cancellationToken);
        if (!current.DeviceDetected || current.DriverInstalled || !current.CanBootstrapDriver)
        {
            return current;
        }

        var helperPath = BundledAssetPathResolver.ResolveDriverSetupScript(settings);
        try
        {
            if (File.Exists(helperPath))
            {
                await RunElevatedAsync(helperPath, string.Empty, cancellationToken);
            }
            else
            {
                var infPath = BundledAssetPathResolver.ResolveDriverInf(settings);
                if (File.Exists(infPath))
                {
                    await RunElevatedAsync("pnputil", $"/add-driver \"{infPath}\" /install", cancellationToken);
                }
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return current with
            {
                Issue = LiveEnvironmentIssue.DriverInstallCancelled,
                BootstrapOutcome = DriverBootstrapOutcome.Cancelled,
                Message = "Driver bootstrap was cancelled.",
                Guidance = "Run Prepare Live again and accept the elevation prompt to install the RTL-SDR driver."
            };
        }
        catch (Exception ex)
        {
            return current with
            {
                Issue = LiveEnvironmentIssue.DriverInstallFailed,
                BootstrapOutcome = DriverBootstrapOutcome.Failed,
                Message = $"Driver bootstrap failed: {ex.Message}",
                Guidance = "Check the bundled Zadig helper and Windows driver installation permissions."
            };
        }

        var updated = await InspectAsync(settings, cancellationToken);
        return updated.DriverInstalled
            ? updated with
            {
                BootstrapOutcome = DriverBootstrapOutcome.Succeeded,
                Message = "RTL-SDR driver installed successfully. Live environment is ready for the next step."
            }
            : updated with
            {
                Issue = LiveEnvironmentIssue.DriverInstallFailed,
                BootstrapOutcome = DriverBootstrapOutcome.Failed,
                Guidance = updated.Guidance ?? "The helper finished, but the WinUSB/libusb driver was still not detected."
            };
    }

    private static async Task RunElevatedAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(fileName) ?? AppContext.BaseDirectory
        }) ?? throw new InvalidOperationException($"Failed to launch {fileName}.");

        await process.WaitForExitAsync(cancellationToken);
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
