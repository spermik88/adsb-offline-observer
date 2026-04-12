using System.Diagnostics;
using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Models;

namespace AdsbObserver.Infrastructure.Services;

public sealed class RtlSdrDeviceDetector : IDeviceDetector
{
    private static readonly string[] CompatibleTokens =
    [
        "RTL2832",
        "RTL2838",
        "Realtek 2832",
        "DVB-T",
        "RTL-SDR"
    ];

    public async Task<IReadOnlyList<SdrDeviceInfo>> DetectAsync(CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "pnputil",
            Arguments = "/enum-devices /connected /drivers",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start pnputil.");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var devices = new List<SdrDeviceInfo>();
        var currentName = string.Empty;
        var currentId = string.Empty;
        var currentDriver = string.Empty;
        var currentService = string.Empty;

        foreach (var line in output.Split(Environment.NewLine))
        {
            if (line.StartsWith("Device Description:", StringComparison.OrdinalIgnoreCase))
            {
                TryAddCurrentDevice();
                currentName = line[(line.IndexOf(':') + 1)..].Trim();
                currentId = string.Empty;
                currentDriver = string.Empty;
                currentService = string.Empty;
            }
            else if (line.StartsWith("Instance ID:", StringComparison.OrdinalIgnoreCase))
            {
                currentId = line[(line.IndexOf(':') + 1)..].Trim();
            }
            else if (line.StartsWith("Driver Name:", StringComparison.OrdinalIgnoreCase))
            {
                currentDriver = line[(line.IndexOf(':') + 1)..].Trim();
            }
            else if (line.StartsWith("Service Name:", StringComparison.OrdinalIgnoreCase))
            {
                currentService = line[(line.IndexOf(':') + 1)..].Trim();
            }
        }

        TryAddCurrentDevice();
        return devices;

        void TryAddCurrentDevice()
        {
            if (string.IsNullOrWhiteSpace(currentName) && string.IsNullOrWhiteSpace(currentId))
            {
                return;
            }

            var compatible = CompatibleTokens.Any(token =>
                currentName.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                currentId.Contains(token, StringComparison.OrdinalIgnoreCase));

            if (!compatible)
            {
                return;
            }

            var driverReady =
                currentDriver.Contains("winusb", StringComparison.OrdinalIgnoreCase) ||
                currentDriver.Contains("libusb", StringComparison.OrdinalIgnoreCase) ||
                currentService.Contains("winusb", StringComparison.OrdinalIgnoreCase) ||
                currentService.Contains("libusb", StringComparison.OrdinalIgnoreCase);

            devices.Add(new SdrDeviceInfo(
                currentName,
                currentId,
                true,
                string.IsNullOrWhiteSpace(currentDriver) ? null : currentDriver,
                string.IsNullOrWhiteSpace(currentService) ? null : currentService,
                driverReady));
        }
    }
}
