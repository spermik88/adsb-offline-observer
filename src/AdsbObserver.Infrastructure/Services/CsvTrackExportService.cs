using System.Globalization;
using System.Text;
using AdsbObserver.Core.Interfaces;

namespace AdsbObserver.Infrastructure.Services;

public sealed class CsvTrackExportService(IStorageService storageService) : ITrackExportService
{
    public async Task ExportAsync(string path, string? icao, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var tracks = await storageService.GetStoredTracksAsync(fromUtc, toUtc, icao, cancellationToken);

        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteLineAsync("icao,timestamp_utc,latitude,longitude,altitude_feet,ground_speed_knots,heading_degrees,vertical_rate_fpm");

        foreach (var track in tracks)
        {
            foreach (var point in track.Points.OrderBy(point => point.TimestampUtc))
            {
                var line = string.Join(",",
                    track.Icao,
                    point.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
                    point.Latitude.ToString(CultureInfo.InvariantCulture),
                    point.Longitude.ToString(CultureInfo.InvariantCulture),
                    point.AltitudeFeet?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    point.GroundSpeedKnots?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    point.HeadingDegrees?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    point.VerticalRateFeetPerMinute?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                await writer.WriteLineAsync(line);
            }
        }
    }
}
