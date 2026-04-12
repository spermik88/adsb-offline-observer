using System.Globalization;
using System.Text;
using AdsbObserver.Core.Models;

namespace AdsbObserver.Infrastructure.Services;

public static class ReadsbArgumentBuilder
{
    public static string Build(ObservationSettings settings)
    {
        var builder = new StringBuilder();
        AppendFlag(builder, "--net");
        Append(builder, "--device-type", "rtlsdr");
        Append(builder, "--net-sbs-port", settings.DecoderPort.ToString(CultureInfo.InvariantCulture));
        Append(builder, "--lat", settings.CenterLatitude.ToString("F6", CultureInfo.InvariantCulture));
        Append(builder, "--lon", settings.CenterLongitude.ToString("F6", CultureInfo.InvariantCulture));
        Append(builder, "--gain", settings.Gain.ToString("F1", CultureInfo.InvariantCulture));
        Append(builder, "--ppm", settings.PpmCorrection.ToString(CultureInfo.InvariantCulture));
        Append(builder, "--sample-rate", settings.SampleRate.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(settings.PreferredDeviceId))
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
