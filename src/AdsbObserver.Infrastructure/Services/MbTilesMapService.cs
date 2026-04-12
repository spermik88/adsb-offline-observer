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

    private static readonly string[] Schema =
    [
        "CREATE TABLE IF NOT EXISTS metadata (name TEXT PRIMARY KEY, value TEXT);",
        "CREATE TABLE IF NOT EXISTS tiles (zoom_level INTEGER, tile_column INTEGER, tile_row INTEGER, tile_data BLOB, PRIMARY KEY (zoom_level, tile_column, tile_row));"
    ];
}
