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
    private readonly PortableWorkspacePaths _workspace;
    private readonly StorageCompatibilityStatus _storageCompatibility;
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
    private string _statusText = "Инициализация portable-окружения...";
    private string _deviceStatusText = "Проверка RTL-SDR...";
    private string _modeText = "Режим: ожидание";
    private string _recognitionStatusText = "База распознавания: не загружена";
    private string _decoderStatusText = "Backend: не запущен";
    private string _backendReadinessText = "Bundled backend: проверка...";
    private string _driverReadinessText = "Драйвер RTL-SDR: проверка...";
    private string _liveReadinessText = "Live-режим: проверка...";
    private string _setupHeadlineText = "Portable first-run";
    private string _setupGuidanceText = "Приложение готовит portable-папки и проверяет, какие режимы доступны.";
    private string _liveSourceText = "Источник live: bundled readsb";
    private string _mapStatusText = "Карты: не найдены";
    private string _portableStatusText = "Portable storage: проверка...";
    private string _workspaceStatusText = string.Empty;
    private string _capabilitiesText = "Доступно: анализ";
    private bool _isSetupBlocking;
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
        PortableWorkspacePaths workspace,
        StorageCompatibilityStatus storageCompatibility)
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
        _workspace = workspace;
        _storageCompatibility = storageCompatibility;

        _decoderProcessService.StatusChanged += (_, status) =>
        {
            _ = Application.Current.Dispatcher.InvokeAsync(() => { DecoderStatusText = TranslateDecoderStatus(status.Message); });
        };

        StartLiveCommand = new RelayCommand(() => _ = StartLiveAsync(), () => !_isLiveRunning);
        StopLiveCommand = new RelayCommand(() => _ = StopLiveAsync(), () => _isLiveRunning);
        RefreshDevicesCommand = new RelayCommand(() => _ = RefreshDevicesAsync());
        StartPlaybackCommand = new RelayCommand(() => _ = StartPlaybackAsync());
        PausePlaybackCommand = new RelayCommand(PausePlayback);
        DownloadMapCommand = new RelayCommand(() => _ = RefreshMapPackagesAsync());
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
    public string PortableStatusText { get => _portableStatusText; private set => SetProperty(ref _portableStatusText, value); }
    public string WorkspaceStatusText { get => _workspaceStatusText; private set => SetProperty(ref _workspaceStatusText, value); }
    public string CapabilitiesText { get => _capabilitiesText; private set => SetProperty(ref _capabilitiesText, value); }
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
    public MapLayerType SelectedMapLayer
    {
        get => _selectedMapLayer;
        set
        {
            if (SetProperty(ref _selectedMapLayer, value))
            {
                _ = RefreshMapPackagesAsync();
            }
        }
    }
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
                return "Предпочтительный RTL-SDR не выбран";
            }

            var device = _availableDevices.FirstOrDefault(item => item.DeviceId == _settings.PreferredDeviceId);
            return device is null
                ? "Сохраненный RTL-SDR сейчас не подключен"
                : $"Выбрано устройство: {device.Name}";
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

        PortableStatusText = $"Portable storage: {_storageCompatibility.Message}";
        WorkspaceStatusText = $"Папки: data={_workspace.DataRoot}, maps={_workspace.MapsRoot}, recordings={_workspace.RecordingsRoot}";

        _recognitionLookup = await _storageService.GetRecognitionLookupAsync(CancellationToken.None);
        RecognitionStatusText = $"База распознавания: {_recognitionLookup.Count} записей";

        await RefreshMapPackagesAsync();
        await RefreshDevicesAsync();
        await RefreshEnvironmentStatusAsync();
        UpdateCapabilitiesText();
        StatusText = "Portable-клиент готов";
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
                0 => "RTL-SDR: не обнаружен",
                1 => $"RTL-SDR: {devices[0].Name}",
                _ => $"RTL-SDR: найдено {devices.Count} совместимых устройств"
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
            DeviceStatusText = $"Ошибка проверки RTL-SDR: {ex.Message}";
            LiveReadinessText = "Live-режим: недоступен";
            UpdateCapabilitiesText();
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
                ? $"{TranslateDecoderStatus(decoderStatus.Message)} Проверьте порт и bundled backend."
                : TranslateDecoderStatus(decoderStatus.Message);
            await RefreshEnvironmentStatusAsync();
            _liveCts.Dispose();
            _liveCts = null;
            return;
        }

        _isLiveRunning = true;
        StartLiveCommand.RaiseCanExecuteChanged();
        StopLiveCommand.RaiseCanExecuteChanged();
        ModeText = "Режим: live";
        StatusText = "Запуск live-приема...";
        UpdateCapabilitiesText();

        _ = Task.Run(async () =>
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    StatusText = "Live-прием работает через bundled readsb";
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
                        StatusText = $"Ошибка live-приема: {ex.Message}";
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
                        ModeText = "Режим: ожидание";
                    }

                    UpdateCapabilitiesText();
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
        ModeText = "Режим: ожидание";
        StatusText = "Live-прием остановлен";
        UpdateCapabilitiesText();
        return _decoderProcessService.StopAsync(CancellationToken.None);
    }

    public async Task<int> ImportRecognitionAsync(string path)
    {
        var imported = await _recognitionImportService.ImportAsync(path, CancellationToken.None);
        _recognitionLookup = await _storageService.GetRecognitionLookupAsync(CancellationToken.None);
        RecognitionStatusText = $"База распознавания: {_recognitionLookup.Count} записей";
        StatusText = $"Импортировано записей: {imported}";
        return imported;
    }

    public async Task ExportTracksAsync(string path)
    {
        await _trackExportService.ExportAsync(path, SelectedTrack?.Icao, null, null, CancellationToken.None);
        StatusText = $"CSV экспортирован: {path}";
    }

    public async Task StartPlaybackAsync()
    {
        await StopLiveAsync();

        var tracks = await _storageService.GetStoredTracksAsync(DateTime.UtcNow.AddDays(-7), null, null, CancellationToken.None);
        if (tracks.Count == 0)
        {
            StatusText = "Нет архивных треков для playback";
            return;
        }

        _playbackFrames = _playbackService.BuildFrames(tracks);
        if (_playbackFrames.Count == 0)
        {
            StatusText = "В истории нет точек с координатами для playback";
            return;
        }

        _playbackIndex = 0;
        _isPlaybackMode = true;
        RaisePropertyChanged(nameof(IsPlaybackMode));
        ModeText = "Режим: playback";
        StatusText = "Playback запущен";
        UpdateCapabilitiesText();
        _playbackTimer.Start();
    }

    public void PausePlayback()
    {
        _playbackTimer.Stop();
        if (_isPlaybackMode)
        {
            StatusText = "Playback на паузе";
        }
    }

    public async Task RefreshMapPackagesAsync()
    {
        Directory.CreateDirectory(_workspace.MapsRoot);

        var packages = new List<MapPackageInfo>();
        foreach (var filePath in Directory.EnumerateFiles(_workspace.MapsRoot, "*.mbtiles", SearchOption.TopDirectoryOnly))
        {
            var package = await _mapTileService.InspectPackageAsync(filePath, CancellationToken.None);
            if (package is null)
            {
                continue;
            }

            await _storageService.SaveMapPackageAsync(package, CancellationToken.None);
            packages.Add(package);
        }

        if (packages.Count == 0)
        {
            var storedPackages = await _storageService.GetMapPackagesAsync(CancellationToken.None);
            packages.AddRange(storedPackages.Where(package => File.Exists(package.FilePath)));
        }

        CurrentMapPackage = packages.FirstOrDefault(package => package.LayerType == SelectedMapLayer) ?? packages.FirstOrDefault();
        MapStatusText = CurrentMapPackage is null
            ? $"Карты: не найдены в {_workspace.MapsRoot}. Используется фоновая сетка."
            : $"Карты: {CurrentMapPackage.Name}";

        UpdateCapabilitiesText();
        NotifyVisualStateChanged();
    }

    public async Task SaveSettingsAsync()
    {
        await _storageService.SaveSettingsAsync(_settings, CancellationToken.None);
        await RefreshEnvironmentStatusAsync();
        StatusText = "Настройки сохранены";
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
            StatusText = "Основной live-backend недоступен, включен simulation fallback";
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
            StatusText = "Playback завершен";
            return;
        }

        var frame = _playbackFrames[_playbackIndex++];
        RefreshTrackCollection(frame.Tracks);
        ModeText = $"Режим: playback @ {frame.TimestampUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
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
        StatusText = "Проверка live-окружения...";
        var environment = await _driverBootstrapService.EnsureReadyAsync(_settings, cancellationToken);
        ApplyEnvironmentStatus(environment);
        StatusText = environment.Message;
        UpdateCapabilitiesText();
        return environment;
    }

    private async Task RefreshEnvironmentStatusAsync()
    {
        var environment = await _driverBootstrapService.InspectAsync(_settings, CancellationToken.None);
        ApplyEnvironmentStatus(environment);
    }

    private void ApplyEnvironmentStatus(LiveEnvironmentStatus environment)
    {
        BackendReadinessText = $"Bundled backend: {(environment.BackendAvailable ? "готов" : "не найден")}";
        DriverReadinessText = $"Драйвер RTL-SDR: {(environment.DriverInstalled ? "готов" : "не готов")}";
        LiveReadinessText = $"Live-режим: {(environment.CanStartLive ? "доступен" : "недоступен")} ({environment.Message})";
        SetupHeadlineText = environment.Issue switch
        {
            LiveEnvironmentIssue.None => "Portable-окружение готово",
            LiveEnvironmentIssue.NoCompatibleDevice => "RTL-SDR не обнаружен",
            LiveEnvironmentIssue.MultipleDevicesDetected => "Выберите одно устройство RTL-SDR",
            LiveEnvironmentIssue.DriverMissing => "Донгл требует внешней подготовки",
            LiveEnvironmentIssue.BackendMissing => "Bundled backend отсутствует",
            LiveEnvironmentIssue.PortBusy => "SBS-1 поток уже доступен",
            _ => "Live-окружение требует внимания"
        };
        SetupGuidanceText = environment.Guidance ?? environment.Message;
        LiveSourceText = environment.Issue == LiveEnvironmentIssue.PortBusy
            ? $"Источник live: внешний SBS-1 {DecoderHost}:{DecoderPort}"
            : "Источник live: bundled readsb";
        IsSetupBlocking = false;
        if (environment.DeviceDetected && !string.IsNullOrWhiteSpace(environment.DeviceName))
        {
            DeviceStatusText = $"RTL-SDR: {environment.DeviceName}";
        }

        RaisePropertyChanged(nameof(SelectedDeviceSummary));
        UpdateCapabilitiesText(environment);
    }

    private async Task<bool> TryRunSimulationFallbackAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusText = "Основной live-backend завершился, включен simulation fallback";
                ModeText = "Режим: simulation fallback";
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

    private void UpdateCapabilitiesText(LiveEnvironmentStatus? environment = null)
    {
        var liveReady = environment?.CanStartLive ?? LiveReadinessText.Contains("доступен", StringComparison.OrdinalIgnoreCase);
        var capabilities = new List<string>();

        capabilities.Add(liveReady ? "live" : "live недоступен");
        capabilities.Add("playback");
        capabilities.Add("история");
        capabilities.Add(CurrentMapPackage is null ? "карты отсутствуют" : "карты");

        CapabilitiesText = $"Доступно сейчас: {string.Join(", ", capabilities)}";
    }

    private static string TranslateDecoderStatus(string message)
    {
        return message
            .Replace("Decoder process: stopped", "Backend: остановлен", StringComparison.Ordinal)
            .Replace("Decoder process: starting bundled readsb", "Backend: запуск bundled readsb", StringComparison.Ordinal)
            .Replace("Decoder process: ready", "Backend: готов", StringComparison.Ordinal)
            .Replace("Decoder process: emitted errors", "Backend: сообщает об ошибках", StringComparison.Ordinal)
            .Replace("Decoder process: running", "Backend: работает", StringComparison.Ordinal)
            .Replace("Decoder process exited", "Backend завершился", StringComparison.Ordinal)
            .Replace("Decoder process: auto-start disabled", "Backend: автозапуск отключен", StringComparison.Ordinal)
            .Replace("Decoder process: bundled backend missing", "Backend: bundled executable не найден", StringComparison.Ordinal);
    }
}
