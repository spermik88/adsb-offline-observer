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
        Append(builder, "--samplerate", settings.SampleRate.ToString(CultureInfo.InvariantCulture));

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
