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
        "RTL2838UHIDIR",
        "Realtek 2832",
        "DVB-T",
        "RTL-SDR",
        "VID_0BDA&PID_2838"
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

        return ParsePnpUtilOutput(output);
    }

    internal static IReadOnlyList<SdrDeviceInfo> ParsePnpUtilOutput(string output)
    {
        var devices = new List<SdrDeviceInfo>();
        var current = new DeviceBlock();
        var inMatchingDrivers = false;

        foreach (var rawLine in output.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (TryReadValue(line, "Instance ID:", out var instanceId))
            {
                TryAddCurrentDevice(current);
                current = new DeviceBlock
                {
                    InstanceId = instanceId
                };
                inMatchingDrivers = false;
            }
            else if (TryReadValue(line, "Device Description:", out var description))
            {
                current.Description = description;
            }
            else if (line.StartsWith("Matching Drivers:", StringComparison.OrdinalIgnoreCase))
            {
                inMatchingDrivers = true;
            }
            else if (!inMatchingDrivers && TryReadValue(line, "Driver Name:", out var driverName))
            {
                current.DriverName = driverName;
            }
            else if (!inMatchingDrivers && TryReadValue(line, "Service Name:", out var serviceName))
            {
                current.ServiceName = serviceName;
            }
            else if (TryReadValue(line, "Original Name:", out var originalName))
            {
                current.OriginalNames.Add(originalName);
            }
            else if (TryReadValue(line, "Provider Name:", out var providerName))
            {
                current.ProviderNames.Add(providerName);
            }
        }

        TryAddCurrentDevice(current);
        return devices;

        static bool TryReadValue(string line, string prefix, out string value)
        {
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = line[prefix.Length..].Trim();
                return true;
            }

            value = string.Empty;
            return false;
        }

        void TryAddCurrentDevice(DeviceBlock device)
        {
            if (string.IsNullOrWhiteSpace(device.Description) && string.IsNullOrWhiteSpace(device.InstanceId))
            {
                return;
            }

            var compatible = CompatibleTokens.Any(token =>
                device.Description.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                device.InstanceId.Contains(token, StringComparison.OrdinalIgnoreCase));

            if (!compatible)
            {
                return;
            }

            var driverReady = IsDriverReady(device);

            devices.Add(new SdrDeviceInfo(
                string.IsNullOrWhiteSpace(device.Description) ? device.InstanceId : device.Description,
                device.InstanceId,
                true,
                string.IsNullOrWhiteSpace(device.DriverName) ? null : device.DriverName,
                string.IsNullOrWhiteSpace(device.ServiceName) ? null : device.ServiceName,
                driverReady));
        }
    }

    private static bool IsDriverReady(DeviceBlock device)
    {
        if (ContainsAny(device.DriverName, "winusb", "libusb") ||
            ContainsAny(device.ServiceName, "winusb", "libusb"))
        {
            return true;
        }

        if (ContainsAny(device.ProviderNames, "libwdi") &&
            ContainsAny(device.OriginalNames, "rtl2838uhidir.inf", "rtl2832u.inf", "rtl-sdr"))
        {
            return true;
        }

        return false;
    }

    private static bool ContainsAny(string? value, params string[] tokens) =>
        !string.IsNullOrWhiteSpace(value) &&
        tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsAny(IEnumerable<string> values, params string[] tokens) =>
        values.Any(value => ContainsAny(value, tokens));

    private sealed class DeviceBlock
    {
        public string Description { get; set; } = string.Empty;
        public string InstanceId { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public List<string> OriginalNames { get; } = [];
        public List<string> ProviderNames { get; } = [];
    }
}
