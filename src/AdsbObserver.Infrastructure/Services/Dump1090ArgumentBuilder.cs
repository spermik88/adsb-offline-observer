using System.Globalization;
using System.Text;
using AdsbObserver.Core.Models;

namespace AdsbObserver.Infrastructure.Services;

public static class Dump1090ArgumentBuilder
{
    public static string Build(ObservationSettings settings, string configPath)
    {
        var builder = new StringBuilder();
        AppendFlag(builder, "--net");
        Append(builder, "--config", configPath);

        // This dump1090 build expects tuner settings from config and does not accept
        // Windows PnP instance IDs in --device.
        if (!string.IsNullOrWhiteSpace(settings.PreferredDeviceId) &&
            !settings.PreferredDeviceId.Contains('\\'))
        {
            Append(builder, "--device", settings.PreferredDeviceId);
        }

        if (!string.IsNullOrWhiteSpace(settings.DecoderArguments))
        {
            builder.Append(' ').Append(settings.DecoderArguments.Trim());
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string option, string value)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(option).Append(' ').Append('"').Append(value).Append('"');
    }

    private static void AppendFlag(StringBuilder builder, string option)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(option);
    }
}
