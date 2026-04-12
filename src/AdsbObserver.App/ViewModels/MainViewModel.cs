using System.IO;
using System.Collections.ObjectModel;
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
    private readonly IRecognitionImportService _recognitionImportService;
    private readonly ITrackExportService _trackExportService;
    private readonly IMapTileService _mapTileService;
    private readonly AircraftTrackerService _trackerService;
    private readonly PlaybackService _playbackService;
    private readonly string _dataRoot;
    private readonly DispatcherTimer _playbackTimer;
    private readonly ObservableCollection<TrackViewModel> _tracks = [];
    private IReadOnlyDictionary<string, AircraftRecognitionRecord> _recognitionLookup =
        new Dictionary<string, AircraftRecognitionRecord>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<PlaybackFrame> _playbackFrames = [];
    private int _playbackIndex;
    private CancellationTokenSource? _liveCts;
    private ObservationSettings _settings = new();
    private TrackViewModel? _selectedTrack;
    private string _statusText = "Initializing…";
    private string _deviceStatusText = "Checking SDR device…";
    private string _modeText = "Mode: Idle";
    private string _recognitionStatusText = "Recognition DB: not loaded";
    private string _decoderStatusText = "Decoder process: not started";
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
        _recognitionImportService = recognitionImportService;
        _trackExportService = trackExportService;
        _mapTileService = mapTileService;
        _trackerService = trackerService;
        _playbackService = playbackService;
        _dataRoot = dataRoot;
        _decoderProcessService.StatusChanged += (_, message) =>
        {
            _ = Application.Current.Dispatcher.InvokeAsync(() =>
            {
                DecoderStatusText = message;
            });
        };

        StartLiveCommand = new RelayCommand(() => _ = StartLiveAsync(), () => !_isLiveRunning);
        StopLiveCommand = new RelayCommand(() => _ = StopLiveAsync(), () => _isLiveRunning);
        RefreshDevicesCommand = new RelayCommand(() => _ = RefreshDevicesAsync());
        StartPlaybackCommand = new RelayCommand(() => _ = StartPlaybackAsync());
        PausePlaybackCommand = new RelayCommand(PausePlayback);
        DownloadMapCommand = new RelayCommand(() => _ = DownloadCurrentMapAsync());

        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _playbackTimer.Tick += (_, _) => AdvancePlayback();
    }

    public event EventHandler? VisualStateChanged;

    public ObservableCollection<TrackViewModel> Tracks => _tracks;
    public RelayCommand StartLiveCommand { get; }
    public RelayCommand StopLiveCommand { get; }
    public RelayCommand RefreshDevicesCommand { get; }
    public RelayCommand StartPlaybackCommand { get; }
    public RelayCommand PausePlaybackCommand { get; }
    public RelayCommand DownloadMapCommand { get; }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string DeviceStatusText { get => _deviceStatusText; private set => SetProperty(ref _deviceStatusText, value); }
    public string ModeText { get => _modeText; private set => SetProperty(ref _modeText, value); }
    public string RecognitionStatusText { get => _recognitionStatusText; private set => SetProperty(ref _recognitionStatusText, value); }
    public string DecoderStatusText { get => _decoderStatusText; private set => SetProperty(ref _decoderStatusText, value); }
    public string MapStatusText { get => _mapStatusText; private set => SetProperty(ref _mapStatusText, value); }
    public double CenterLatitude { get => _settings.CenterLatitude; set { _settings.CenterLatitude = value; RaisePropertyChanged(); NotifyVisualStateChanged(); } }
    public double CenterLongitude { get => _settings.CenterLongitude; set { _settings.CenterLongitude = value; RaisePropertyChanged(); NotifyVisualStateChanged(); } }
    public double RadiusKilometers { get => _settings.DisplayRadiusKilometers; set { _settings.DisplayRadiusKilometers = value; RaisePropertyChanged(); NotifyVisualStateChanged(); } }
    public int ActiveTrackCount => _tracks.Count;
    public int SelectedZoom { get => _selectedZoom; set { if (SetProperty(ref _selectedZoom, value)) { NotifyVisualStateChanged(); } } }
    public MapLayerType SelectedMapLayer { get => _selectedMapLayer; set => SetProperty(ref _selectedMapLayer, value); }
    public MapPackageInfo? CurrentMapPackage { get => _currentMapPackage; private set { if (SetProperty(ref _currentMapPackage, value)) { NotifyVisualStateChanged(); } } }
    public bool IsPlaybackMode => _isPlaybackMode;

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

        _recognitionLookup = await _storageService.GetRecognitionLookupAsync(CancellationToken.None);
        RecognitionStatusText = $"Recognition DB: {_recognitionLookup.Count} records";

        var packages = await _storageService.GetMapPackagesAsync(CancellationToken.None);
        CurrentMapPackage = packages.FirstOrDefault(package => package.LayerType == SelectedMapLayer) ?? packages.FirstOrDefault();
        MapStatusText = CurrentMapPackage is null ? "Map package: not selected" : $"Map package: {CurrentMapPackage.Name}";

        await RefreshDevicesAsync();
        StatusText = "Ready";
    }

    public async Task RefreshDevicesAsync()
    {
        try
        {
            var devices = await _deviceDetector.DetectAsync(CancellationToken.None);
            DeviceStatusText = devices.Count switch
            {
                0 => "SDR device: not detected",
                1 => $"SDR device: {devices[0].Name}",
                _ => $"SDR devices: {devices.Count} compatible devices found"
            };
        }
        catch (Exception ex)
        {
            DeviceStatusText = $"SDR detection error: {ex.Message}";
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
        _isLiveRunning = true;
        StartLiveCommand.RaiseCanExecuteChanged();
        StopLiveCommand.RaiseCanExecuteChanged();
        ModeText = "Mode: Live";
        StatusText = "Starting live ingest…";

        await _storageService.SaveSettingsAsync(_settings, _liveCts.Token);
        await _decoderProcessService.StartAsync(_settings, _liveCts.Token);

        _ = Task.Run(async () =>
        {
            try
            {
                var devices = await _deviceDetector.DetectAsync(_liveCts.Token);
                var adapter = devices.Count == 1 ? _liveDecoder : _simulationDecoder;
                var adapterName = ReferenceEquals(adapter, _simulationDecoder) ? "simulation" : "SBS-1 TCP";

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    StatusText = $"Live ingest running via {adapterName}";
                });

                await foreach (var message in ReadWithFallbackAsync(adapter, _liveCts.Token))
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
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    StatusText = $"Live ingest error: {ex.Message}";
                });
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

        var progress = new Progress<int>(value => StatusText = $"Downloading {layer} map… {value}%");
        var template = layer == MapLayerType.Osm
            ? "https://tile.openstreetmap.org/{z}/{x}/{y}.png"
            : "https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}";

        await _mapTileService.DownloadPackageAsync(package, template, progress, CancellationToken.None);
        await _storageService.SaveMapPackageAsync(package, CancellationToken.None);

        CurrentMapPackage = package;
        MapStatusText = $"Map package: {package.Name}";
        StatusText = $"Map package downloaded: {package.Name}";
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

    private static (double North, double South, double East, double West) BuildBounds(double centerLat, double centerLon, double radiusKm)
    {
        var latDelta = radiusKm / 111d;
        var lonDelta = radiusKm / (111d * Math.Max(0.2, Math.Cos(centerLat * Math.PI / 180d)));
        return (centerLat + latDelta, centerLat - latDelta, centerLon + lonDelta, centerLon - lonDelta);
    }
}
