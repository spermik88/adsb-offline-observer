using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Models;
using AdsbObserver.Core.Services;

namespace AdsbObserver.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IStorageService _storageService;
    private readonly IDeviceDetector _deviceDetector;
    private readonly IAdsbDecoderAdapter _liveDecoder;
    private readonly IAdsbDecoderAdapter _simulationDecoder;
    private readonly IDecoderProcessService _decoderProcessService;
    private readonly ISdrDriverBootstrapService _driverBootstrapService;
    private readonly IRecognitionImportService _recognitionImportService;
    private readonly ITrackExportService _trackExportService;
    private readonly IMapTileService _mapTileService;
    private readonly AircraftTrackerService _trackerService;
    private readonly PlaybackService _playbackService;
    private readonly string _dataRoot;
    private readonly DispatcherTimer _playbackTimer;
    private readonly ObservableCollection<TrackViewModel> _tracks = [];
    private readonly ObservableCollection<SdrDeviceInfo> _availableDevices = [];
    private IReadOnlyDictionary<string, AircraftRecognitionRecord> _recognitionLookup =
        new Dictionary<string, AircraftRecognitionRecord>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<PlaybackFrame> _playbackFrames = [];
    private int _playbackIndex;
    private CancellationTokenSource? _liveCts;
    private ObservationSettings _settings = new();
    private TrackViewModel? _selectedTrack;
    private string _statusText = "Initializing...";
    private string _deviceStatusText = "Checking SDR device...";
    private string _modeText = "Mode: Idle";
    private string _recognitionStatusText = "Recognition DB: not loaded";
    private string _decoderStatusText = "Decoder process: not started";
    private string _backendReadinessText = "Backend ready: pending";
    private string _driverReadinessText = "Driver ready: pending";
    private string _liveReadinessText = "Live ready: pending";
    private string _setupHeadlineText = "First-run setup pending";
    private string _setupGuidanceText = "Prepare Live to validate the bundled backend, device, and driver.";
    private string _liveSourceText = "Live source: bundled readsb";
    private bool _isSetupBlocking = true;
    private string _mapStatusText = "Map package: not selected";
    private bool _isLiveRunning;
    private bool _isPlaybackMode;
    private int _selectedZoom = 8;
    private MapLayerType _selectedMapLayer = MapLayerType.Osm;
    private MapPackageInfo? _currentMapPackage;

    public MainViewModel(
        IStorageService storageService,
        IDeviceDetector deviceDetector,
        IAdsbDecoderAdapter liveDecoder,
        IAdsbDecoderAdapter simulationDecoder,
        IDecoderProcessService decoderProcessService,
        ISdrDriverBootstrapService driverBootstrapService,
        IRecognitionImportService recognitionImportService,
        ITrackExportService trackExportService,
        IMapTileService mapTileService,
        AircraftTrackerService trackerService,
        PlaybackService playbackService,
        string dataRoot)
    {
        _storageService = storageService;
        _deviceDetector = deviceDetector;
        _liveDecoder = liveDecoder;
        _simulationDecoder = simulationDecoder;
        _decoderProcessService = decoderProcessService;
        _driverBootstrapService = driverBootstrapService;
        _recognitionImportService = recognitionImportService;
        _trackExportService = trackExportService;
        _mapTileService = mapTileService;
        _trackerService = trackerService;
        _playbackService = playbackService;
        _dataRoot = dataRoot;
        _decoderProcessService.StatusChanged += (_, status) =>
        {
            _ = Application.Current.Dispatcher.InvokeAsync(() =>
            {
                DecoderStatusText = status.Message;
            });
        };

        StartLiveCommand = new RelayCommand(() => _ = StartLiveAsync(), () => !_isLiveRunning);
        StopLiveCommand = new RelayCommand(() => _ = StopLiveAsync(), () => _isLiveRunning);
        RefreshDevicesCommand = new RelayCommand(() => _ = RefreshDevicesAsync());
        StartPlaybackCommand = new RelayCommand(() => _ = StartPlaybackAsync());
        PausePlaybackCommand = new RelayCommand(PausePlayback);
        DownloadMapCommand = new RelayCommand(() => _ = DownloadCurrentMapAsync());
        SaveSettingsCommand = new RelayCommand(() => _ = SaveSettingsAsync());
        PrepareLiveCommand = new RelayCommand(() => _ = PrepareLiveEnvironmentAsync());

        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _playbackTimer.Tick += (_, _) => AdvancePlayback();
    }

    public event EventHandler? VisualStateChanged;

    public ObservableCollection<TrackViewModel> Tracks => _tracks;
    public ObservableCollection<SdrDeviceInfo> AvailableDevices => _availableDevices;
    public RelayCommand StartLiveCommand { get; }
    public RelayCommand StopLiveCommand { get; }
    public RelayCommand RefreshDevicesCommand { get; }
    public RelayCommand StartPlaybackCommand { get; }
    public RelayCommand PausePlaybackCommand { get; }
    public RelayCommand DownloadMapCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }
    public RelayCommand PrepareLiveCommand { get; }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string DeviceStatusText { get => _deviceStatusText; private set => SetProperty(ref _deviceStatusText, value); }
    public string ModeText { get => _modeText; private set => SetProperty(ref _modeText, value); }
    public string RecognitionStatusText { get => _recognitionStatusText; private set => SetProperty(ref _recognitionStatusText, value); }
    public string DecoderStatusText { get => _decoderStatusText; private set => SetProperty(ref _decoderStatusText, value); }
    public string BackendReadinessText { get => _backendReadinessText; private set => SetProperty(ref _backendReadinessText, value); }
    public string DriverReadinessText { get => _driverReadinessText; private set => SetProperty(ref _driverReadinessText, value); }
    public string LiveReadinessText { get => _liveReadinessText; private set => SetProperty(ref _liveReadinessText, value); }
    public string SetupHeadlineText { get => _setupHeadlineText; private set => SetProperty(ref _setupHeadlineText, value); }
    public string SetupGuidanceText { get => _setupGuidanceText; private set => SetProperty(ref _setupGuidanceText, value); }
    public string LiveSourceText { get => _liveSourceText; private set => SetProperty(ref _liveSourceText, value); }
    public bool IsSetupBlocking { get => _isSetupBlocking; private set => SetProperty(ref _isSetupBlocking, value); }
    public string MapStatusText { get => _mapStatusText; private set => SetProperty(ref _mapStatusText, value); }
    public double CenterLatitude { get => _settings.CenterLatitude; set { _settings.CenterLatitude = value; RaisePropertyChanged(); NotifyVisualStateChanged(); } }
    public double CenterLongitude { get => _settings.CenterLongitude; set { _settings.CenterLongitude = value; RaisePropertyChanged(); NotifyVisualStateChanged(); } }
    public double RadiusKilometers { get => _settings.DisplayRadiusKilometers; set { _settings.DisplayRadiusKilometers = value; RaisePropertyChanged(); NotifyVisualStateChanged(); } }
    public double Gain { get => _settings.Gain; set { _settings.Gain = value; RaisePropertyChanged(); } }
    public int PpmCorrection { get => _settings.PpmCorrection; set { _settings.PpmCorrection = value; RaisePropertyChanged(); } }
    public int SampleRate { get => _settings.SampleRate; set { _settings.SampleRate = value; RaisePropertyChanged(); } }
    public string DecoderHost { get => _settings.DecoderHost; set { _settings.DecoderHost = value; RaisePropertyChanged(); } }
    public int DecoderPort { get => _settings.DecoderPort; set { _settings.DecoderPort = value; RaisePropertyChanged(); } }
    public bool UseSimulationFallback { get => _settings.UseSimulationFallback; set { _settings.UseSimulationFallback = value; RaisePropertyChanged(); } }
    public int ActiveTrackCount => _tracks.Count;
    public int SelectedZoom { get => _selectedZoom; set { if (SetProperty(ref _selectedZoom, value)) { NotifyVisualStateChanged(); } } }
    public MapLayerType SelectedMapLayer { get => _selectedMapLayer; set => SetProperty(ref _selectedMapLayer, value); }
    public MapPackageInfo? CurrentMapPackage { get => _currentMapPackage; private set { if (SetProperty(ref _currentMapPackage, value)) { NotifyVisualStateChanged(); } } }
    public bool IsPlaybackMode => _isPlaybackMode;

    public string? SelectedDeviceId
    {
        get => _settings.PreferredDeviceId;
        set
        {
            if (_settings.PreferredDeviceId == value)
            {
                return;
            }

            _settings.PreferredDeviceId = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SelectedDeviceSummary));
        }
    }

    public string SelectedDeviceSummary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_settings.PreferredDeviceId))
            {
                return "Preferred RTL-SDR: not selected";
            }

            var device = _availableDevices.FirstOrDefault(item => item.DeviceId == _settings.PreferredDeviceId);
            return device is null
                ? "Preferred RTL-SDR: saved device not currently connected"
                : $"Preferred RTL-SDR: {device.Name}";
        }
    }

    public TrackViewModel? SelectedTrack
    {
        get => _selectedTrack;
        set => SetProperty(ref _selectedTrack, value);
    }

    public async Task InitializeAsync()
    {
        _settings = await _storageService.GetSettingsAsync(CancellationToken.None);
        _selectedZoom = _settings.DefaultZoom;
        RaisePropertyChanged(nameof(CenterLatitude));
        RaisePropertyChanged(nameof(CenterLongitude));
        RaisePropertyChanged(nameof(RadiusKilometers));
        RaisePropertyChanged(nameof(SelectedZoom));
        RaisePropertyChanged(nameof(Gain));
        RaisePropertyChanged(nameof(PpmCorrection));
        RaisePropertyChanged(nameof(SampleRate));
        RaisePropertyChanged(nameof(DecoderHost));
        RaisePropertyChanged(nameof(DecoderPort));
        RaisePropertyChanged(nameof(UseSimulationFallback));
        RaisePropertyChanged(nameof(SelectedDeviceId));
        RaisePropertyChanged(nameof(SelectedDeviceSummary));

        _recognitionLookup = await _storageService.GetRecognitionLookupAsync(CancellationToken.None);
        RecognitionStatusText = $"Recognition DB: {_recognitionLookup.Count} records";

        var packages = await _storageService.GetMapPackagesAsync(CancellationToken.None);
        CurrentMapPackage = packages.FirstOrDefault(package => package.LayerType == SelectedMapLayer) ?? packages.FirstOrDefault();
        MapStatusText = CurrentMapPackage is null ? "Map package: not selected" : $"Map package: {CurrentMapPackage.Name}";

        await RefreshDevicesAsync();
        await RefreshEnvironmentStatusAsync();
        StatusText = "Ready";
    }

    public async Task RefreshDevicesAsync()
    {
        try
        {
            var devices = await _deviceDetector.DetectAsync(CancellationToken.None);
            _availableDevices.Clear();
            foreach (var device in devices)
            {
                _availableDevices.Add(device);
            }

            DeviceStatusText = devices.Count switch
            {
                0 => "SDR device: not detected",
                1 => $"SDR device: {devices[0].Name}",
                _ => $"SDR devices: {devices.Count} compatible devices found"
            };

            if (devices.Count == 1)
            {
                _settings.PreferredDeviceId ??= devices[0].DeviceId;
            }
            else if (devices.Count > 1 && !devices.Any(device => device.DeviceId == _settings.PreferredDeviceId))
            {
                _settings.PreferredDeviceId = null;
            }

            RaisePropertyChanged(nameof(SelectedDeviceId));
            RaisePropertyChanged(nameof(SelectedDeviceSummary));
            await RefreshEnvironmentStatusAsync();
        }
        catch (Exception ex)
        {
            DeviceStatusText = $"SDR detection error: {ex.Message}";
            LiveReadinessText = "Live ready: no";
        }
    }

    public async Task StartLiveAsync()
    {
        if (_isLiveRunning)
        {
            return;
        }

        PausePlayback();
        _isPlaybackMode = false;
        RaisePropertyChanged(nameof(IsPlaybackMode));

        _liveCts = new CancellationTokenSource();
        await _storageService.SaveSettingsAsync(_settings, _liveCts.Token);

        var environment = await PrepareLiveEnvironmentAsync(_liveCts.Token);
        if (!environment.CanStartLive)
        {
            _liveCts.Dispose();
            _liveCts = null;
            return;
        }

        var decoderStatus = await _decoderProcessService.StartAsync(_settings, _liveCts.Token);
        if (!decoderStatus.IsReady)
        {
            StatusText = decoderStatus.FailureReason == DecoderFailureReason.PortUnavailable
                ? $"{decoderStatus.Message}. Check whether the bundled backend supports the configured arguments or if another process blocked the port."
                : decoderStatus.Message;
            await RefreshEnvironmentStatusAsync();
            _liveCts.Dispose();
            _liveCts = null;
            return;
        }

        _isLiveRunning = true;
        StartLiveCommand.RaiseCanExecuteChanged();
        StopLiveCommand.RaiseCanExecuteChanged();
        ModeText = "Mode: Live";
        StatusText = "Starting live ingest...";

        _ = Task.Run(async () =>
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    StatusText = "Live ingest running via bundled readsb";
                });

                await foreach (var message in ReadWithFallbackAsync(_liveDecoder, _liveCts.Token))
                {
                    var before = _trackerService.GetAllTracks().FirstOrDefault(track =>
                        track.Icao.Equals(message.Icao, StringComparison.OrdinalIgnoreCase))?.Points.Count ?? 0;
                    var track = _trackerService.ProcessMessage(message, _recognitionLookup);
                    await _storageService.UpsertTrackAsync(track, _liveCts.Token);

                    if (track.Points.Count > before)
                    {
                        await _storageService.AppendTrackPointAsync(track.Icao, track.Points[^1], _liveCts.Token);
                    }

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        RefreshTrackCollection(_trackerService.GetActiveTracks(DateTime.UtcNow, TimeSpan.FromMinutes(_settings.ActiveTargetTimeoutMinutes)));
                    });
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                var fallbackUsed = false;
                if (_settings.UseSimulationFallback && _liveCts is not null && !_liveCts.IsCancellationRequested)
                {
                    fallbackUsed = await TryRunSimulationFallbackAsync(_liveCts.Token);
                }

                if (!fallbackUsed)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        StatusText = $"Live ingest error: {ex.Message}";
                    });
                }
            }
            finally
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _isLiveRunning = false;
                    StartLiveCommand.RaiseCanExecuteChanged();
                    StopLiveCommand.RaiseCanExecuteChanged();
                    if (!_isPlaybackMode)
                    {
                        ModeText = "Mode: Idle";
                    }
                });

                await _decoderProcessService.StopAsync(CancellationToken.None);
                await RefreshEnvironmentStatusAsync();
            }
        });
    }

    public Task StopLiveAsync()
    {
        _liveCts?.Cancel();
        _isLiveRunning = false;
        StartLiveCommand.RaiseCanExecuteChanged();
        StopLiveCommand.RaiseCanExecuteChanged();
        ModeText = "Mode: Idle";
        StatusText = "Live ingest stopped";
        return _decoderProcessService.StopAsync(CancellationToken.None);
    }

    public async Task<int> ImportRecognitionAsync(string path)
    {
        var imported = await _recognitionImportService.ImportAsync(path, CancellationToken.None);
        _recognitionLookup = await _storageService.GetRecognitionLookupAsync(CancellationToken.None);
        RecognitionStatusText = $"Recognition DB: {_recognitionLookup.Count} records";
        StatusText = $"Imported {imported} recognition records";
        return imported;
    }

    public async Task ExportTracksAsync(string path)
    {
        await _trackExportService.ExportAsync(path, SelectedTrack?.Icao, null, null, CancellationToken.None);
        StatusText = $"Exported track data to {path}";
    }

    public async Task StartPlaybackAsync()
    {
        await StopLiveAsync();

        var tracks = await _storageService.GetStoredTracksAsync(DateTime.UtcNow.AddDays(-7), null, null, CancellationToken.None);
        if (tracks.Count == 0)
        {
            StatusText = "No archived tracks available for playback";
            return;
        }

        _playbackFrames = _playbackService.BuildFrames(tracks);
        if (_playbackFrames.Count == 0)
        {
            StatusText = "Stored tracks do not contain position history";
            return;
        }

        _playbackIndex = 0;
        _isPlaybackMode = true;
        RaisePropertyChanged(nameof(IsPlaybackMode));
        ModeText = "Mode: Playback";
        StatusText = "Playback started";
        _playbackTimer.Start();
    }

    public void PausePlayback()
    {
        _playbackTimer.Stop();
        if (_isPlaybackMode)
        {
            StatusText = "Playback paused";
        }
    }

    public async Task DownloadCurrentMapAsync()
    {
        var layer = SelectedMapLayer;
        var bounds = BuildBounds(_settings.CenterLatitude, _settings.CenterLongitude, _settings.DisplayRadiusKilometers);
        var packagesDirectory = Path.Combine(_dataRoot, "maps");
        Directory.CreateDirectory(packagesDirectory);

        var package = new MapPackageInfo
        {
            Name = $"{layer}_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
            LayerType = layer,
            FilePath = Path.Combine(packagesDirectory, $"{layer}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.mbtiles"),
            MinZoom = _settings.MinZoom,
            MaxZoom = _settings.MaxZoom,
            North = bounds.North,
            South = bounds.South,
            East = bounds.East,
            West = bounds.West,
            DownloadedUtc = DateTime.UtcNow
        };

        var progress = new Progress<int>(value => StatusText = $"Downloading {layer} map... {value}%");
        var template = layer == MapLayerType.Osm
            ? "https://tile.openstreetmap.org/{z}/{x}/{y}.png"
            : "https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}";

        await _mapTileService.DownloadPackageAsync(package, template, progress, CancellationToken.None);
        await _storageService.SaveMapPackageAsync(package, CancellationToken.None);

        CurrentMapPackage = package;
        MapStatusText = $"Map package: {package.Name}";
        StatusText = $"Map package downloaded: {package.Name}";
    }

    public async Task SaveSettingsAsync()
    {
        await _storageService.SaveSettingsAsync(_settings, CancellationToken.None);
        await RefreshEnvironmentStatusAsync();
        StatusText = "Settings saved";
    }

    public async Task<byte[]?> GetTileBytesAsync(int zoom, int x, int y, CancellationToken cancellationToken)
    {
        return CurrentMapPackage is null
            ? null
            : await _mapTileService.GetTileBytesAsync(CurrentMapPackage, zoom, x, y, cancellationToken);
    }

    private async IAsyncEnumerable<AircraftMessage> ReadWithFallbackAsync(
        IAdsbDecoderAdapter preferredAdapter,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var useFallback = false;

        await using (var enumerator = preferredAdapter.ReadMessagesAsync(_settings, cancellationToken).GetAsyncEnumerator(cancellationToken))
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                AircraftMessage current;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        yield break;
                    }

                    current = enumerator.Current;
                }
                catch when (!ReferenceEquals(preferredAdapter, _simulationDecoder) && _settings.UseSimulationFallback)
                {
                    useFallback = true;
                    break;
                }

                yield return current;
            }
        }

        if (!useFallback)
        {
            yield break;
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusText = "Primary decoder unavailable, switching to simulation fallback";
        });

        await foreach (var message in _simulationDecoder.ReadMessagesAsync(_settings, cancellationToken))
        {
            yield return message;
        }
    }

    private void AdvancePlayback()
    {
        if (_playbackFrames.Count == 0)
        {
            return;
        }

        if (_playbackIndex >= _playbackFrames.Count)
        {
            _playbackTimer.Stop();
            StatusText = "Playback completed";
            return;
        }

        var frame = _playbackFrames[_playbackIndex++];
        RefreshTrackCollection(frame.Tracks);
        ModeText = $"Mode: Playback @ {frame.TimestampUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
    }

    private void RefreshTrackCollection(IReadOnlyList<AircraftTrack> tracks)
    {
        _tracks.Clear();
        foreach (var track in tracks.OrderByDescending(track => track.LastSeenUtc))
        {
            _tracks.Add(new TrackViewModel(track));
        }

        SelectedTrack ??= _tracks.FirstOrDefault();
        RaisePropertyChanged(nameof(ActiveTrackCount));
        NotifyVisualStateChanged();
    }

    private void NotifyVisualStateChanged()
    {
        VisualStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task<LiveEnvironmentStatus> PrepareLiveEnvironmentAsync(CancellationToken cancellationToken = default)
    {
        StatusText = "Checking live environment...";
        var environment = await _driverBootstrapService.InspectAsync(_settings, cancellationToken);
        ApplyEnvironmentStatus(environment);

        if (!environment.DeviceDetected)
        {
            StatusText = environment.Message;
            return environment;
        }

        if (!environment.DriverInstalled && environment.CanBootstrapDriver)
        {
            StatusText = "Launching SDR driver bootstrap...";
            environment = await _driverBootstrapService.EnsureReadyAsync(_settings, cancellationToken);
            ApplyEnvironmentStatus(environment);
        }

        StatusText = environment.Message;
        return environment;
    }

    private async Task RefreshEnvironmentStatusAsync()
    {
        var environment = await _driverBootstrapService.InspectAsync(_settings, CancellationToken.None);
        ApplyEnvironmentStatus(environment);
    }

    private void ApplyEnvironmentStatus(LiveEnvironmentStatus environment)
    {
        BackendReadinessText = $"Backend ready: {(environment.BackendAvailable ? "yes" : "no")}";
        DriverReadinessText = $"Driver ready: {(environment.DriverInstalled ? "yes" : "no")}";
        LiveReadinessText = $"Live ready: {(environment.CanStartLive ? "yes" : "no")} ({environment.Message})";
        SetupHeadlineText = environment.Issue switch
        {
            LiveEnvironmentIssue.None => "Live setup complete",
            LiveEnvironmentIssue.NoCompatibleDevice => "RTL-SDR not detected",
            LiveEnvironmentIssue.MultipleDevicesDetected => "Choose a single RTL-SDR device",
            LiveEnvironmentIssue.DriverMissing => "Driver setup required",
            LiveEnvironmentIssue.DriverInstallCancelled => "Driver setup was cancelled",
            LiveEnvironmentIssue.DriverInstallFailed => "Driver setup failed",
            LiveEnvironmentIssue.BackendMissing => "Bundled backend missing",
            LiveEnvironmentIssue.PortBusy => "External SBS-1 source already available",
            _ => "Live setup needs attention"
        };
        SetupGuidanceText = environment.Guidance ?? environment.Message;
        LiveSourceText = environment.Issue == LiveEnvironmentIssue.PortBusy
            ? $"Live source: external SBS-1 on {_settings.DecoderHost}:{_settings.DecoderPort}"
            : "Live source: bundled readsb";
        IsSetupBlocking = !environment.CanStartLive && !_isLiveRunning;
        if (environment.DeviceDetected && !string.IsNullOrWhiteSpace(environment.DeviceName))
        {
            DeviceStatusText = $"SDR device: {environment.DeviceName}";
        }

        RaisePropertyChanged(nameof(SelectedDeviceSummary));
    }

    private async Task<bool> TryRunSimulationFallbackAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusText = "Primary live backend failed, switching to simulation fallback";
                ModeText = "Mode: Simulation Fallback";
            });

            await foreach (var message in _simulationDecoder.ReadMessagesAsync(_settings, cancellationToken))
            {
                var before = _trackerService.GetAllTracks().FirstOrDefault(track =>
                    track.Icao.Equals(message.Icao, StringComparison.OrdinalIgnoreCase))?.Points.Count ?? 0;
                var track = _trackerService.ProcessMessage(message, _recognitionLookup);
                await _storageService.UpsertTrackAsync(track, cancellationToken);

                if (track.Points.Count > before)
                {
                    await _storageService.AppendTrackPointAsync(track.Icao, track.Points[^1], cancellationToken);
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    RefreshTrackCollection(_trackerService.GetActiveTracks(DateTime.UtcNow, TimeSpan.FromMinutes(_settings.ActiveTargetTimeoutMinutes)));
                });
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static (double North, double South, double East, double West) BuildBounds(double centerLat, double centerLon, double radiusKm)
    {
        var latDelta = radiusKm / 111d;
        var lonDelta = radiusKm / (111d * Math.Max(0.2, Math.Cos(centerLat * Math.PI / 180d)));
        return (centerLat + latDelta, centerLat - latDelta, centerLon + lonDelta, centerLon - lonDelta);
    }
}
