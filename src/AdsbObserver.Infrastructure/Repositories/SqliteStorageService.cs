using System.Globalization;
using System.Text.Json;
using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Models;
using Microsoft.Data.Sqlite;

namespace AdsbObserver.Infrastructure.Repositories;

public sealed class SqliteStorageService : IStorageService
{
    private const int CurrentStorageVersion = 1;
    private readonly string _connectionString;

    public SqliteStorageService(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = $"Data Source={databasePath}";
    }

    public async Task<StorageCompatibilityStatus> InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var commands = new[]
        {
            """
            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS app_metadata (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS aircraft_snapshots (
                icao TEXT PRIMARY KEY,
                callsign TEXT NULL,
                first_seen_utc TEXT NOT NULL,
                last_seen_utc TEXT NOT NULL,
                latitude REAL NULL,
                longitude REAL NULL,
                altitude_feet INTEGER NULL,
                ground_speed_knots REAL NULL,
                heading_degrees REAL NULL,
                vertical_rate_fpm INTEGER NULL,
                squawk TEXT NULL,
                emitter_category TEXT NULL,
                registration TEXT NULL,
                aircraft_type TEXT NULL,
                operator TEXT NULL,
                country TEXT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS track_points (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                icao TEXT NOT NULL,
                timestamp_utc TEXT NOT NULL,
                latitude REAL NOT NULL,
                longitude REAL NOT NULL,
                altitude_feet INTEGER NULL,
                ground_speed_knots REAL NULL,
                heading_degrees REAL NULL,
                vertical_rate_fpm INTEGER NULL
            );
            """,
            """
            CREATE INDEX IF NOT EXISTS idx_track_points_icao_time
            ON track_points (icao, timestamp_utc);
            """,
            """
            CREATE TABLE IF NOT EXISTS recognition (
                icao TEXT PRIMARY KEY,
                registration TEXT NULL,
                aircraft_type TEXT NULL,
                operator TEXT NULL,
                country TEXT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS map_packages (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                layer_type TEXT NOT NULL,
                file_path TEXT NOT NULL,
                min_zoom INTEGER NOT NULL,
                max_zoom INTEGER NOT NULL,
                north REAL NOT NULL,
                south REAL NOT NULL,
                east REAL NOT NULL,
                west REAL NOT NULL,
                downloaded_utc TEXT NOT NULL
            );
            """
        };

        foreach (var text in commands)
        {
            var command = connection.CreateCommand();
            command.CommandText = text;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var detectedVersion = await GetStoredVersionAsync(connection, cancellationToken);
        if (detectedVersion is null)
        {
            await SetStoredVersionAsync(connection, CurrentStorageVersion, cancellationToken);
            return new StorageCompatibilityStatus(
                true,
                false,
                CurrentStorageVersion,
                CurrentStorageVersion,
                "Portable data storage initialized.");
        }

        if (detectedVersion > CurrentStorageVersion)
        {
            return new StorageCompatibilityStatus(
                false,
                true,
                CurrentStorageVersion,
                detectedVersion.Value,
                $"Portable data was created by a newer build (v{detectedVersion.Value}).");
        }

        if (detectedVersion < CurrentStorageVersion)
        {
            await SetStoredVersionAsync(connection, CurrentStorageVersion, cancellationToken);
            return new StorageCompatibilityStatus(
                true,
                false,
                CurrentStorageVersion,
                detectedVersion.Value,
                $"Portable data metadata upgraded from v{detectedVersion.Value} to v{CurrentStorageVersion}.");
        }

        return new StorageCompatibilityStatus(
            true,
            false,
            CurrentStorageVersion,
            detectedVersion.Value,
            "Portable data storage is ready.");
    }

    public async Task<ObservationSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key = 'observation_settings';";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is not string json || string.IsNullOrWhiteSpace(json))
        {
            return new ObservationSettings();
        }

        return JsonSerializer.Deserialize<ObservationSettings>(json) ?? new ObservationSettings();
    }

    public async Task SaveSettingsAsync(ObservationSettings settings, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO settings(key, value)
            VALUES('observation_settings', $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$value", JsonSerializer.Serialize(settings));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertTrackAsync(AircraftTrack track, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO aircraft_snapshots (
                icao, callsign, first_seen_utc, last_seen_utc, latitude, longitude, altitude_feet,
                ground_speed_knots, heading_degrees, vertical_rate_fpm, squawk, emitter_category,
                registration, aircraft_type, operator, country
            )
            VALUES (
                $icao, $callsign, $firstSeen, $lastSeen, $lat, $lon, $altitude,
                $speed, $heading, $vr, $squawk, $emitter,
                $registration, $aircraftType, $operator, $country
            )
            ON CONFLICT(icao) DO UPDATE SET
                callsign = excluded.callsign,
                first_seen_utc = excluded.first_seen_utc,
                last_seen_utc = excluded.last_seen_utc,
                latitude = excluded.latitude,
                longitude = excluded.longitude,
                altitude_feet = excluded.altitude_feet,
                ground_speed_knots = excluded.ground_speed_knots,
                heading_degrees = excluded.heading_degrees,
                vertical_rate_fpm = excluded.vertical_rate_fpm,
                squawk = excluded.squawk,
                emitter_category = excluded.emitter_category,
                registration = excluded.registration,
                aircraft_type = excluded.aircraft_type,
                operator = excluded.operator,
                country = excluded.country;
            """;
        BindTrack(command, track);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AppendTrackPointAsync(string icao, AircraftTrackPoint point, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO track_points (
                icao, timestamp_utc, latitude, longitude, altitude_feet,
                ground_speed_knots, heading_degrees, vertical_rate_fpm
            )
            VALUES ($icao, $timestamp, $lat, $lon, $altitude, $speed, $heading, $vr);
            """;
        command.Parameters.AddWithValue("$icao", icao.ToUpperInvariant());
        command.Parameters.AddWithValue("$timestamp", point.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$lat", point.Latitude);
        command.Parameters.AddWithValue("$lon", point.Longitude);
        command.Parameters.AddWithValue("$altitude", (object?)point.AltitudeFeet ?? DBNull.Value);
        command.Parameters.AddWithValue("$speed", (object?)point.GroundSpeedKnots ?? DBNull.Value);
        command.Parameters.AddWithValue("$heading", (object?)point.HeadingDegrees ?? DBNull.Value);
        command.Parameters.AddWithValue("$vr", (object?)point.VerticalRateFeetPerMinute ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AircraftTrack>> GetStoredTracksAsync(DateTime? fromUtc, DateTime? toUtc, string? icao, CancellationToken cancellationToken)
    {
        var tracks = new Dictionary<string, AircraftTrack>(StringComparer.OrdinalIgnoreCase);
        var recognition = await GetRecognitionLookupAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                icao, callsign, first_seen_utc, last_seen_utc, latitude, longitude, altitude_feet,
                ground_speed_knots, heading_degrees, vertical_rate_fpm, squawk, emitter_category,
                registration, aircraft_type, operator, country
            FROM aircraft_snapshots
            WHERE ($icao IS NULL OR icao = $icao)
              AND ($fromUtc IS NULL OR last_seen_utc >= $fromUtc)
              AND ($toUtc IS NULL OR first_seen_utc <= $toUtc)
            ORDER BY last_seen_utc DESC;
            """;
        command.Parameters.AddWithValue("$icao", string.IsNullOrWhiteSpace(icao) ? DBNull.Value : icao.ToUpperInvariant());
        command.Parameters.AddWithValue("$fromUtc", fromUtc.HasValue ? fromUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);
        command.Parameters.AddWithValue("$toUtc", toUtc.HasValue ? toUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var track = new AircraftTrack
            {
                Icao = reader.GetString(0),
                Callsign = reader.IsDBNull(1) ? null : reader.GetString(1),
                FirstSeenUtc = DateTime.Parse(reader.GetString(2), null, DateTimeStyles.RoundtripKind),
                LastSeenUtc = DateTime.Parse(reader.GetString(3), null, DateTimeStyles.RoundtripKind),
                Latitude = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                Longitude = reader.IsDBNull(5) ? null : reader.GetDouble(5),
                AltitudeFeet = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                GroundSpeedKnots = reader.IsDBNull(7) ? null : reader.GetDouble(7),
                HeadingDegrees = reader.IsDBNull(8) ? null : reader.GetDouble(8),
                VerticalRateFeetPerMinute = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                Squawk = reader.IsDBNull(10) ? null : reader.GetString(10),
                EmitterCategory = reader.IsDBNull(11) ? null : reader.GetString(11),
                Recognition = recognition.TryGetValue(reader.GetString(0), out var record)
                    ? record
                    : new AircraftRecognitionRecord(reader.GetString(0),
                        reader.IsDBNull(12) ? null : reader.GetString(12),
                        reader.IsDBNull(13) ? null : reader.GetString(13),
                        reader.IsDBNull(14) ? null : reader.GetString(14),
                        reader.IsDBNull(15) ? null : reader.GetString(15))
            };

            tracks[track.Icao] = track;
        }

        foreach (var track in tracks.Values)
        {
            var points = await GetTrackPointsAsync(track.Icao, fromUtc, toUtc, cancellationToken);
            track.Points.AddRange(points);
        }

        return tracks.Values.OrderByDescending(track => track.LastSeenUtc).ToList();
    }

    public async Task<IReadOnlyList<AircraftTrackPoint>> GetTrackPointsAsync(string icao, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var result = new List<AircraftTrackPoint>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT timestamp_utc, latitude, longitude, altitude_feet, ground_speed_knots, heading_degrees, vertical_rate_fpm
            FROM track_points
            WHERE icao = $icao
              AND ($fromUtc IS NULL OR timestamp_utc >= $fromUtc)
              AND ($toUtc IS NULL OR timestamp_utc <= $toUtc)
            ORDER BY timestamp_utc;
            """;
        command.Parameters.AddWithValue("$icao", icao.ToUpperInvariant());
        command.Parameters.AddWithValue("$fromUtc", fromUtc.HasValue ? fromUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);
        command.Parameters.AddWithValue("$toUtc", toUtc.HasValue ? toUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new AircraftTrackPoint(
                DateTime.Parse(reader.GetString(0), null, DateTimeStyles.RoundtripKind),
                reader.GetDouble(1),
                reader.GetDouble(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetDouble(4),
                reader.IsDBNull(5) ? null : reader.GetDouble(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6)));
        }

        return result;
    }

    public async Task UpsertRecognitionAsync(IEnumerable<AircraftRecognitionRecord> records, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        foreach (var record in records)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO recognition(icao, registration, aircraft_type, operator, country)
                VALUES ($icao, $registration, $aircraftType, $operator, $country)
                ON CONFLICT(icao) DO UPDATE SET
                    registration = excluded.registration,
                    aircraft_type = excluded.aircraft_type,
                    operator = excluded.operator,
                    country = excluded.country;
                """;
            command.Parameters.AddWithValue("$icao", record.Icao.ToUpperInvariant());
            command.Parameters.AddWithValue("$registration", (object?)record.Registration ?? DBNull.Value);
            command.Parameters.AddWithValue("$aircraftType", (object?)record.AircraftType ?? DBNull.Value);
            command.Parameters.AddWithValue("$operator", (object?)record.Operator ?? DBNull.Value);
            command.Parameters.AddWithValue("$country", (object?)record.Country ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, AircraftRecognitionRecord>> GetRecognitionLookupAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, AircraftRecognitionRecord>(StringComparer.OrdinalIgnoreCase);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT icao, registration, aircraft_type, operator, country FROM recognition;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetString(0)] = new AircraftRecognitionRecord(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4));
        }

        return result;
    }

    public async Task SaveMapPackageAsync(MapPackageInfo package, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO map_packages (
                id, name, layer_type, file_path, min_zoom, max_zoom,
                north, south, east, west, downloaded_utc
            )
            VALUES (
                $id, $name, $layerType, $filePath, $minZoom, $maxZoom,
                $north, $south, $east, $west, $downloadedUtc
            )
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                layer_type = excluded.layer_type,
                file_path = excluded.file_path,
                min_zoom = excluded.min_zoom,
                max_zoom = excluded.max_zoom,
                north = excluded.north,
                south = excluded.south,
                east = excluded.east,
                west = excluded.west,
                downloaded_utc = excluded.downloaded_utc;
            """;
        command.Parameters.AddWithValue("$id", package.Id);
        command.Parameters.AddWithValue("$name", package.Name);
        command.Parameters.AddWithValue("$layerType", package.LayerType.ToString());
        command.Parameters.AddWithValue("$filePath", package.FilePath);
        command.Parameters.AddWithValue("$minZoom", package.MinZoom);
        command.Parameters.AddWithValue("$maxZoom", package.MaxZoom);
        command.Parameters.AddWithValue("$north", package.North);
        command.Parameters.AddWithValue("$south", package.South);
        command.Parameters.AddWithValue("$east", package.East);
        command.Parameters.AddWithValue("$west", package.West);
        command.Parameters.AddWithValue("$downloadedUtc", package.DownloadedUtc.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MapPackageInfo>> GetMapPackagesAsync(CancellationToken cancellationToken)
    {
        var result = new List<MapPackageInfo>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, name, layer_type, file_path, min_zoom, max_zoom, north, south, east, west, downloaded_utc
            FROM map_packages
            ORDER BY downloaded_utc DESC;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new MapPackageInfo
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                LayerType = Enum.Parse<MapLayerType>(reader.GetString(2), true),
                FilePath = reader.GetString(3),
                MinZoom = reader.GetInt32(4),
                MaxZoom = reader.GetInt32(5),
                North = reader.GetDouble(6),
                South = reader.GetDouble(7),
                East = reader.GetDouble(8),
                West = reader.GetDouble(9),
                DownloadedUtc = DateTime.Parse(reader.GetString(10), null, DateTimeStyles.RoundtripKind)
            });
        }

        return result;
    }

    private static void BindTrack(SqliteCommand command, AircraftTrack track)
    {
        command.Parameters.AddWithValue("$icao", track.Icao.ToUpperInvariant());
        command.Parameters.AddWithValue("$callsign", (object?)track.Callsign ?? DBNull.Value);
        command.Parameters.AddWithValue("$firstSeen", track.FirstSeenUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$lastSeen", track.LastSeenUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$lat", (object?)track.Latitude ?? DBNull.Value);
        command.Parameters.AddWithValue("$lon", (object?)track.Longitude ?? DBNull.Value);
        command.Parameters.AddWithValue("$altitude", (object?)track.AltitudeFeet ?? DBNull.Value);
        command.Parameters.AddWithValue("$speed", (object?)track.GroundSpeedKnots ?? DBNull.Value);
        command.Parameters.AddWithValue("$heading", (object?)track.HeadingDegrees ?? DBNull.Value);
        command.Parameters.AddWithValue("$vr", (object?)track.VerticalRateFeetPerMinute ?? DBNull.Value);
        command.Parameters.AddWithValue("$squawk", (object?)track.Squawk ?? DBNull.Value);
        command.Parameters.AddWithValue("$emitter", (object?)track.EmitterCategory ?? DBNull.Value);
        command.Parameters.AddWithValue("$registration", (object?)track.Recognition?.Registration ?? DBNull.Value);
        command.Parameters.AddWithValue("$aircraftType", (object?)track.Recognition?.AircraftType ?? DBNull.Value);
        command.Parameters.AddWithValue("$operator", (object?)track.Recognition?.Operator ?? DBNull.Value);
        command.Parameters.AddWithValue("$country", (object?)track.Recognition?.Country ?? DBNull.Value);
    }

    private static async Task<int?> GetStoredVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_metadata WHERE key = 'storage_version';";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string text && int.TryParse(text, out var parsed)
            ? parsed
            : null;
    }

    private static async Task SetStoredVersionAsync(SqliteConnection connection, int version, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO app_metadata(key, value)
            VALUES('storage_version', $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$value", version.ToString(CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
