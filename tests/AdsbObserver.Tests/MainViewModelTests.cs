using System.Collections.Generic;
using System.IO;
using AdsbObserver.App.ViewModels;
using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Models;
using AdsbObserver.Core.Services;
using Xunit;

namespace AdsbObserver.Tests;

public sealed class MainViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "adsbobserver-vm-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InitializeAsync_ReportsPlaybackWhenNoMapsAndNoSdr()
    {
        Directory.CreateDirectory(_root);
        var workspace = CreateWorkspace();
        var viewModel = CreateViewModel(
            workspace,
            storageService: new FakeStorageService(),
            deviceDetector: new FakeDeviceDetector([]),
            mapTileService: new FakeMapTileService(),
            decoderProcessService: new FakeDecoderProcessService(),
            driverBootstrapService: new FakeDriverBootstrapService(new LiveEnvironmentStatus(
                LiveEnvironmentIssue.NoCompatibleDevice,
                false,
                false,
                true,
                false,
                false,
                false,
                true,
                DriverBootstrapOutcome.None,
                "РЎРѕРІРјРµСЃС‚РёРјС‹Р№ RTL-SDR РЅРµ РЅР°Р№РґРµРЅ.",
                "Playback Рё РёСЃС‚РѕСЂРёСЏ РґРѕСЃС‚СѓРїРЅС‹ Р±РµР· РїСЂРёРµРјРЅРёРєР°.")));

        await viewModel.InitializeAsync();

        Assert.Equal("Карты: не найдены в " + workspace.MapsRoot + ". Используется фоновая сетка.", viewModel.MapStatusText);
        Assert.Contains("playback", viewModel.CapabilitiesText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("история", viewModel.CapabilitiesText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("live недоступен", viewModel.CapabilitiesText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshMapPackagesAsync_FindsPortableMbtilesPackage()
    {
        Directory.CreateDirectory(_root);
        var workspace = CreateWorkspace();
        var mapPath = Path.Combine(workspace.MapsRoot, "city.mbtiles");
        File.WriteAllText(mapPath, "placeholder");

        var package = new MapPackageInfo
        {
            Name = "City",
            FilePath = mapPath,
            LayerType = MapLayerType.Osm,
            MinZoom = 1,
            MaxZoom = 10
        };

        var mapService = new FakeMapTileService();
        mapService.Packages[mapPath] = package;
        var storage = new FakeStorageService();
        var viewModel = CreateViewModel(
            workspace,
            storageService: storage,
            deviceDetector: new FakeDeviceDetector([]),
            mapTileService: mapService,
            decoderProcessService: new FakeDecoderProcessService(),
            driverBootstrapService: new FakeDriverBootstrapService(DefaultEnvironment()));

        await viewModel.RefreshMapPackagesAsync();

        Assert.Equal("Карты: City", viewModel.MapStatusText);
        Assert.NotNull(viewModel.CurrentMapPackage);
        Assert.Single(storage.SavedMapPackages);
    }

    [Fact]
    public async Task InitializeAsync_ShowsMissingBundledBackend()
    {
        Directory.CreateDirectory(_root);
        var workspace = CreateWorkspace();
        var viewModel = CreateViewModel(
            workspace,
            storageService: new FakeStorageService(),
            deviceDetector: new FakeDeviceDetector([new SdrDeviceInfo("RTL", "id", true, "WinUSB", null, true)]),
            mapTileService: new FakeMapTileService(),
            decoderProcessService: new FakeDecoderProcessService(),
            driverBootstrapService: new FakeDriverBootstrapService(new LiveEnvironmentStatus(
                LiveEnvironmentIssue.BackendMissing,
                true,
                true,
                false,
                false,
                false,
                false,
                true,
                DriverBootstrapOutcome.NotNeeded,
                "Р’ portable-РїР°РєРµС‚Рµ РѕС‚СЃСѓС‚СЃС‚РІСѓРµС‚ bundled backend dump1090.",
                "РџСЂРѕРІРµСЂСЊС‚Рµ, С‡С‚Рѕ dump1090.exe РІРєР»СЋС‡РµРЅ РІ portable release layout.",
                "RTL",
                "WinUSB")));

        await viewModel.InitializeAsync();

        Assert.Contains("не найден", viewModel.BackendReadinessText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bundled dump1090 отсутствует", viewModel.SetupHeadlineText, StringComparison.OrdinalIgnoreCase);
    }

    private PortableWorkspacePaths CreateWorkspace()
    {
        var appRoot = Path.Combine(_root, "app");
        var portableRoot = Path.Combine(_root, "portable");
        Directory.CreateDirectory(appRoot);
        Directory.CreateDirectory(portableRoot);
        Directory.CreateDirectory(Path.Combine(portableRoot, "data"));
        Directory.CreateDirectory(Path.Combine(portableRoot, "maps"));
        Directory.CreateDirectory(Path.Combine(portableRoot, "recordings"));
        Directory.CreateDirectory(Path.Combine(portableRoot, "logs"));

        return new PortableWorkspacePaths
        {
            AppRoot = appRoot,
            PortableRoot = portableRoot,
            DataRoot = Path.Combine(portableRoot, "data"),
            MapsRoot = Path.Combine(portableRoot, "maps"),
            RecordingsRoot = Path.Combine(portableRoot, "recordings"),
            LogsRoot = Path.Combine(portableRoot, "logs")
        };
    }

    private static MainViewModel CreateViewModel(
        PortableWorkspacePaths workspace,
        FakeStorageService storageService,
        FakeDeviceDetector deviceDetector,
        FakeMapTileService mapTileService,
        FakeDecoderProcessService decoderProcessService,
        FakeDriverBootstrapService driverBootstrapService)
    {
        return new MainViewModel(
            storageService,
            deviceDetector,
            new FakeDecoderAdapter(),
            new FakeDecoderAdapter(),
            decoderProcessService,
            driverBootstrapService,
            new FakeRecognitionImportService(),
            new FakeTrackExportService(),
            mapTileService,
            new AircraftTrackerService(),
            new PlaybackService(),
            workspace,
            new StorageCompatibilityStatus(true, false, 1, 1, "OK"));
    }

    private static LiveEnvironmentStatus DefaultEnvironment() =>
        new(
            LiveEnvironmentIssue.None,
            false,
            false,
            true,
            false,
            false,
            false,
            false,
            DriverBootstrapOutcome.NotNeeded,
            "Р”РёР°РіРЅРѕСЃС‚РёРєР° Р·Р°РІРµСЂС€РµРЅР°.");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private sealed class FakeStorageService : IStorageService
    {
        public List<MapPackageInfo> SavedMapPackages { get; } = [];
        public ObservationSettings Settings { get; } = new();

        public Task<StorageCompatibilityStatus> InitializeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new StorageCompatibilityStatus(true, false, 1, 1, "OK"));

        public Task<ObservationSettings> GetSettingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Settings);

        public Task SaveSettingsAsync(ObservationSettings settings, CancellationToken cancellationToken)
        {
            Settings.PreferredDeviceId = settings.PreferredDeviceId;
            return Task.CompletedTask;
        }

        public Task UpsertTrackAsync(AircraftTrack track, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AppendTrackPointAsync(string icao, AircraftTrackPoint point, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<AircraftTrack>> GetStoredTracksAsync(DateTime? fromUtc, DateTime? toUtc, string? icao, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AircraftTrack>>([]);
        public Task<IReadOnlyList<AircraftTrackPoint>> GetTrackPointsAsync(string icao, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AircraftTrackPoint>>([]);
        public Task UpsertRecognitionAsync(IEnumerable<AircraftRecognitionRecord> records, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<string, AircraftRecognitionRecord>> GetRecognitionLookupAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, AircraftRecognitionRecord>>(new Dictionary<string, AircraftRecognitionRecord>());

        public Task SaveMapPackageAsync(MapPackageInfo package, CancellationToken cancellationToken)
        {
            SavedMapPackages.Add(package);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MapPackageInfo>> GetMapPackagesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MapPackageInfo>>(SavedMapPackages);
    }

    private sealed class FakeDeviceDetector(IReadOnlyList<SdrDeviceInfo> devices) : IDeviceDetector
    {
        public Task<IReadOnlyList<SdrDeviceInfo>> DetectAsync(CancellationToken cancellationToken) => Task.FromResult(devices);
    }

    private sealed class FakeMapTileService : IMapTileService
    {
        public Dictionary<string, MapPackageInfo> Packages { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task DownloadPackageAsync(MapPackageInfo package, string urlTemplate, IProgress<int>? progress, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<byte[]?> GetTileBytesAsync(MapPackageInfo package, int zoom, int x, int y, CancellationToken cancellationToken) =>
            Task.FromResult<byte[]?>(null);

        public Task<MapPackageInfo?> InspectPackageAsync(string filePath, CancellationToken cancellationToken) =>
            Task.FromResult(Packages.TryGetValue(filePath, out var package) ? package : null);
    }

    private sealed class FakeDecoderProcessService : IDecoderProcessService
    {
        public event EventHandler<DecoderProcessStatus>? StatusChanged { add { } remove { } }
        public bool IsRunning => false;
        public DecoderProcessStatus CurrentStatus { get; } = new(DecoderProcessState.Stopped, "dump1090: stopped");
        public Task<DecoderProcessStatus> StartAsync(ObservationSettings settings, CancellationToken cancellationToken) => Task.FromResult(CurrentStatus);
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeDriverBootstrapService(LiveEnvironmentStatus status) : ISdrDriverBootstrapService
    {
        public Task<LiveEnvironmentStatus> InspectAsync(ObservationSettings settings, CancellationToken cancellationToken) => Task.FromResult(status);
        public Task<LiveEnvironmentStatus> EnsureReadyAsync(ObservationSettings settings, CancellationToken cancellationToken) => Task.FromResult(status);
    }

    private sealed class FakeRecognitionImportService : IRecognitionImportService
    {
        public Task<int> ImportAsync(string path, CancellationToken cancellationToken) => Task.FromResult(0);
    }

    private sealed class FakeTrackExportService : ITrackExportService
    {
        public Task ExportAsync(string path, string? icao, DateTime? fromUtc, DateTime? toUtc, bool withCoordinatesOnly, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeDecoderAdapter : IAdsbDecoderAdapter
    {
        public async IAsyncEnumerable<AircraftMessage> ReadMessagesAsync(ObservationSettings settings, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}

