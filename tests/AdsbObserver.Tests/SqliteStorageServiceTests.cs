using AdsbObserver.Core.Models;
using AdsbObserver.Infrastructure.Repositories;
using Xunit;

namespace AdsbObserver.Tests;

public sealed class SqliteStorageServiceTests
{
    [Fact]
    public async Task SaveAndLoadSettings_PreservesBundledDeliveryFields()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var storage = new SqliteStorageService(databasePath);
        var compatibility = await storage.InitializeAsync(CancellationToken.None);
        Assert.True(compatibility.IsCompatible);

        var settings = new ObservationSettings
        {
            CenterLatitude = 10,
            CenterLongitude = 20,
            Gain = 33.3,
            PpmCorrection = 7,
            SampleRate = 2_048_000,
            PreferBundledDecoder = true,
            BundledDecoderRelativePath = @"backend\dump1090\dump1090.exe",
            BundledDriverSetupRelativePath = @"drivers\rtl-sdr\install-driver.cmd",
            BundledDriverInfRelativePath = @"drivers\rtl-sdr\rtlsdr-winusb.inf",
            UseSimulationFallback = false
        };

        await storage.SaveSettingsAsync(settings, CancellationToken.None);
        var loaded = await storage.GetSettingsAsync(CancellationToken.None);

        Assert.Equal(settings.CenterLatitude, loaded.CenterLatitude);
        Assert.Equal(settings.CenterLongitude, loaded.CenterLongitude);
        Assert.Equal(settings.Gain, loaded.Gain);
        Assert.Equal(settings.PpmCorrection, loaded.PpmCorrection);
        Assert.Equal(settings.SampleRate, loaded.SampleRate);
        Assert.Equal(settings.PreferBundledDecoder, loaded.PreferBundledDecoder);
        Assert.Equal(settings.BundledDecoderRelativePath, loaded.BundledDecoderRelativePath);
        Assert.Equal(settings.BundledDriverSetupRelativePath, loaded.BundledDriverSetupRelativePath);
        Assert.Equal(settings.BundledDriverInfRelativePath, loaded.BundledDriverInfRelativePath);
        Assert.Equal(settings.UseSimulationFallback, loaded.UseSimulationFallback);
    }

    [Fact]
    public async Task InitializeAsync_RejectsFutureStorageVersion()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var storage = new SqliteStorageService(databasePath);
        await storage.InitializeAsync(CancellationToken.None);

        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO app_metadata(key, value)
            VALUES('storage_version', '99')
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        await command.ExecuteNonQueryAsync();

        var secondStorage = new SqliteStorageService(databasePath);
        var compatibility = await secondStorage.InitializeAsync(CancellationToken.None);

        Assert.False(compatibility.IsCompatible);
        Assert.True(compatibility.RequiresMigration);
        Assert.Equal(99, compatibility.DetectedVersion);
    }
}
