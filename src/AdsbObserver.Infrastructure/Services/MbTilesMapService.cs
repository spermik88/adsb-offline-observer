using System.Globalization;
using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Models;
using Microsoft.Data.Sqlite;

namespace AdsbObserver.Infrastructure.Services;

public sealed class MbTilesMapService : IMapTileService
{
    private readonly HttpClient _httpClient = new();

    public async Task DownloadPackageAsync(MapPackageInfo package, string urlTemplate, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(package.FilePath)!);

        await using var connection = new SqliteConnection($"Data Source={package.FilePath}");
        await connection.OpenAsync(cancellationToken);

        foreach (var commandText in Schema)
        {
            var command = connection.CreateCommand();
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var tiles = BuildTileManifest(package).ToList();
        var processed = 0;
        foreach (var tile in tiles)
        {
            var url = urlTemplate
                .Replace("{z}", tile.Zoom.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{x}", tile.X.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{y}", tile.Y.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

            var bytes = await _httpClient.GetByteArrayAsync(url, cancellationToken);
            var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT OR REPLACE INTO tiles(zoom_level, tile_column, tile_row, tile_data)
                VALUES ($z, $x, $y, $bytes);
                """;
            command.Parameters.AddWithValue("$z", tile.Zoom);
            command.Parameters.AddWithValue("$x", tile.X);
            command.Parameters.AddWithValue("$y", TmsY(tile.Zoom, tile.Y));
            command.Parameters.AddWithValue("$bytes", bytes);
            await command.ExecuteNonQueryAsync(cancellationToken);

            processed++;
            progress?.Report((int)(processed / (double)tiles.Count * 100));
        }

        await UpsertMetadataAsync(connection, "name", package.Name, cancellationToken);
        await UpsertMetadataAsync(connection, "type", "baselayer", cancellationToken);
        await UpsertMetadataAsync(connection, "format", "png", cancellationToken);
        await UpsertMetadataAsync(connection, "minzoom", package.MinZoom.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await UpsertMetadataAsync(connection, "maxzoom", package.MaxZoom.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await UpsertMetadataAsync(
            connection,
            "bounds",
            $"{package.West.ToString(CultureInfo.InvariantCulture)},{package.South.ToString(CultureInfo.InvariantCulture)},{package.East.ToString(CultureInfo.InvariantCulture)},{package.North.ToString(CultureInfo.InvariantCulture)}",
            cancellationToken);
    }

    public async Task<byte[]?> GetTileBytesAsync(MapPackageInfo package, int zoom, int x, int y, CancellationToken cancellationToken)
    {
        if (!File.Exists(package.FilePath))
        {
            return null;
        }

        await using var connection = new SqliteConnection($"Data Source={package.FilePath}");
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT tile_data
            FROM tiles
            WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y;
            """;
        command.Parameters.AddWithValue("$z", zoom);
        command.Parameters.AddWithValue("$x", x);
        command.Parameters.AddWithValue("$y", TmsY(zoom, y));

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value as byte[];
    }

    public async Task<MapPackageInfo?> InspectPackageAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var connection = new SqliteConnection($"Data Source={filePath}");
        await connection.OpenAsync(cancellationToken);

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT name, value FROM metadata;";

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                metadata[reader.GetString(0)] = reader.GetString(1);
            }
        }
        catch (SqliteException)
        {
            return null;
        }

        var bounds = ParseBounds(metadata.TryGetValue("bounds", out var boundsValue) ? boundsValue : null);
        var minZoom = ParseInt(metadata.TryGetValue("minzoom", out var minZoomValue) ? minZoomValue : null, 0);
        var maxZoom = ParseInt(metadata.TryGetValue("maxzoom", out var maxZoomValue) ? maxZoomValue : null, Math.Max(minZoom, 12));
        var name = metadata.TryGetValue("name", out var nameValue)
            ? nameValue
            : Path.GetFileNameWithoutExtension(filePath);

        return new MapPackageInfo
        {
            Id = BuildStablePackageId(filePath),
            Name = name,
            LayerType = InferLayerType(filePath, name),
            FilePath = Path.GetFullPath(filePath),
            MinZoom = minZoom,
            MaxZoom = maxZoom,
            North = bounds.North,
            South = bounds.South,
            East = bounds.East,
            West = bounds.West,
            DownloadedUtc = File.GetLastWriteTimeUtc(filePath)
        };
    }

    private static IEnumerable<(int Zoom, int X, int Y)> BuildTileManifest(MapPackageInfo package)
    {
        for (var zoom = package.MinZoom; zoom <= package.MaxZoom; zoom++)
        {
            var (minX, minY) = DegToTile(package.North, package.West, zoom);
            var (maxX, maxY) = DegToTile(package.South, package.East, zoom);

            var fromX = Math.Min(minX, maxX);
            var toX = Math.Max(minX, maxX);
            var fromY = Math.Min(minY, maxY);
            var toY = Math.Max(minY, maxY);

            for (var x = fromX; x <= toX; x++)
            {
                for (var y = fromY; y <= toY; y++)
                {
                    yield return (zoom, x, y);
                }
            }
        }
    }

    private static (int X, int Y) DegToTile(double lat, double lon, int zoom)
    {
        var latRad = lat * Math.PI / 180d;
        var n = Math.Pow(2d, zoom);
        var x = (int)Math.Floor((lon + 180d) / 360d * n);
        var y = (int)Math.Floor((1d - Math.Asinh(Math.Tan(latRad)) / Math.PI) / 2d * n);
        return (x, y);
    }

    private static int TmsY(int zoom, int y)
    {
        return (1 << zoom) - 1 - y;
    }

    private static async Task UpsertMetadataAsync(SqliteConnection connection, string name, string value, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT OR REPLACE INTO metadata(name, value)
            VALUES ($name, $value);
            """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static (double North, double South, double East, double West) ParseBounds(string? bounds)
    {
        if (string.IsNullOrWhiteSpace(bounds))
        {
            return (85, -85, 180, -180);
        }

        var parts = bounds.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var west) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var south) ||
            !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var east) ||
            !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var north))
        {
            return (85, -85, 180, -180);
        }

        return (north, south, east, west);
    }

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static MapLayerType InferLayerType(string filePath, string name)
    {
        var text = $"{filePath} {name}".ToLowerInvariant();
        return text.Contains("sat", StringComparison.Ordinal) || text.Contains("imagery", StringComparison.Ordinal)
            ? MapLayerType.Satellite
            : MapLayerType.Osm;
    }

    private static string BuildStablePackageId(string filePath)
    {
        var normalized = Path.GetFullPath(filePath).ToUpperInvariant();
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized)));
    }

    private static readonly string[] Schema =
    [
        "CREATE TABLE IF NOT EXISTS metadata (name TEXT PRIMARY KEY, value TEXT);",
        "CREATE TABLE IF NOT EXISTS tiles (zoom_level INTEGER, tile_column INTEGER, tile_row INTEGER, tile_data BLOB, PRIMARY KEY (zoom_level, tile_column, tile_row));"
    ];
}
