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
        try
        {
            var storage = new SqliteStorageService(databasePath);
            await storage.InitializeAsync(CancellationToken.None);

            var settings = new ObservationSettings
            {
                CenterLatitude = 10,
                CenterLongitude = 20,
                Gain = 33.3,
                PpmCorrection = 7,
                SampleRate = 2_048_000,
                PreferBundledDecoder = true,
                BundledDecoderRelativePath = @"backend\readsb\readsb.exe",
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
        finally
        {
            _ = databasePath;
        }
    }
}
