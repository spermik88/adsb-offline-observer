using System.Globalization;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Models;

namespace AdsbObserver.Infrastructure.Services;

public sealed class Sbs1TcpDecoderAdapter : IAdsbDecoderAdapter
{
    public async IAsyncEnumerable<AircraftMessage> ReadMessagesAsync(
        ObservationSettings settings,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(settings.DecoderHost, settings.DecoderPort, cancellationToken);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var message = ParseSbs1(line);
            if (message is not null)
            {
                yield return message;
            }
        }
    }

    private static AircraftMessage? ParseSbs1(string line)
    {
        var cells = line.Split(',');
        if (cells.Length < 22 || !string.Equals(cells[0], "MSG", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var icao = cells[4].Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(icao))
        {
            return null;
        }

        var timestamp = ParseTimestamp(cells[6], cells[7]) ?? DateTime.UtcNow;
        return new AircraftMessage(
            icao,
            timestamp,
            NullIfEmpty(cells[10]),
            ParseDouble(cells[14]),
            ParseDouble(cells[15]),
            ParseInt(cells[11]),
            ParseDouble(cells[12]),
            ParseDouble(cells[13]),
            ParseInt(cells[16]),
            NullIfEmpty(cells[17]),
            NullIfEmpty(cells[8]));
    }

    private static DateTime? ParseTimestamp(string datePart, string timePart)
    {
        if (DateTime.TryParseExact(
                $"{datePart} {timePart}",
                ["yyyy/MM/dd HH:mm:ss.fff", "yyyy/MM/dd HH:mm:ss"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var value))
        {
            return value;
        }

        return null;
    }

    private static string? NullIfEmpty(string value)
    {
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static int? ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static double? ParseDouble(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }
}
