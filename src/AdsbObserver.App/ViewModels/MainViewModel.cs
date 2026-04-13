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
    private readonly PlaybackCoordinator _playbackCoordinator;
    private readonly PortableWorkspacePaths _workspace;
    private readonly StorageCompatibilityStatus _storageCompatibility;
    private readonly DispatcherTimer _playbackTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly DispatcherTimer _uiRefreshTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly TrackListState _trackListState = new();
    private readonly ObservableCollection<SdrDeviceInfo> _availableDevices = [];
    private readonly LiveStatusState _liveStatusState = new();
    private readonly MapViewState _mapViewState = new();
    private readonly object _trackerLock = new();
    private IReadOnlyDictionary<string, AircraftRecognitionRecord> _recognitionLookup = new Dictionary<string, AircraftRecognitionRecord>(StringComparer.OrdinalIgnoreCase);
    private ObservationSettings _settings = new();
    private CancellationTokenSource? _liveCts;
    private string _deviceStatusText = "RTL-SDR: проверка...";
    private string _recognitionStatusText = "Распознавание: база не загружена";
    private string _decoderStatusText = "Источник: decoder не запущен";
    private string _backendReadinessText = "Backend: проверка...";
    private string _driverReadinessText = "RTL-SDR driver: проверка...";
    private string _liveReadinessText = "Live: проверка...";
    private string _setupHeadlineText = "Проверка portable-окружения";
    private string _setupGuidanceText = "Приложение проверяет live, playback, карты и историю.";
    private string _mapStatusText = "Карты: не найдены";
    private string _portableStatusText = "Portable storage: проверка...";
    private string _workspaceStatusText = string.Empty;
    private string _capabilitiesText = "Доступно: анализ истории";
    private string _searchText = string.Empty;
    private string _minAltitudeText = string.Empty;
    private string _maxAltitudeText = string.Empty;
    private string _minSpeedText = string.Empty;
    private string _maxSpeedText = string.Empty;
    private string _maxDistanceText = string.Empty;
    private string _trackMetricsText = "Треки: 0 active / 0 stale / 0 with position";
    private string _sourceSummaryText = "Источник: idle";
    private string _decoderFailureText = "Decoder: явных ошибок нет";
    private string _historyIcaoText = string.Empty;
    private string _historyFromText = string.Empty;
    private string _historyToText = string.Empty;
    private bool _historySelectedOnly;
    private bool _historyWithCoordinatesOnly = true;
    private bool _isLiveRunning;
    private bool _isPlaybackMode;
    private bool _withPositionOnly;
    private bool _airborneOnly;
    private bool _showSelectedOnly;
    private TrackSortMode _selectedSortMode = TrackSortMode.LastSeen;

    public MainViewModel(IStorageService storageService, IDeviceDetector deviceDetector, IAdsbDecoderAdapter liveDecoder, IAdsbDecoderAdapter simulationDecoder, IDecoderProcessService decoderProcessService, ISdrDriverBootstrapService driverBootstrapService, IRecognitionImportService recognitionImportService, ITrackExportService trackExportService, IMapTileService mapTileService, AircraftTrackerService trackerService, PlaybackService playbackService, PortableWorkspacePaths workspace, StorageCompatibilityStatus storageCompatibility)
    {
        _storageService = storageService; _deviceDetector = deviceDetector; _liveDecoder = liveDecoder; _simulationDecoder = simulationDecoder; _decoderProcessService = decoderProcessService; _driverBootstrapService = driverBootstrapService; _recognitionImportService = recognitionImportService; _trackExportService = trackExportService; _mapTileService = mapTileService; _trackerService = trackerService; _playbackCoordinator = new PlaybackCoordinator(playbackService); _workspace = workspace; _storageCompatibility = storageCompatibility;
        _decoderProcessService.StatusChanged += (_, status) => _ = InvokeOnUiAsync(() => ApplyDecoderStatus(status));
        StartLiveCommand = new RelayCommand(() => _ = StartLiveAsync(), () => !_isLiveRunning);
        StopLiveCommand = new RelayCommand(() => _ = StopLiveAsync(), () => _isLiveRunning);
        RefreshDevicesCommand = new RelayCommand(() => _ = RefreshDevicesAsync());
        StartPlaybackCommand = new RelayCommand(() => _ = StartPlaybackAsync());
        PausePlaybackCommand = new RelayCommand(PausePlayback);
        DownloadMapCommand = new RelayCommand(() => _ = RefreshMapPackagesAsync());
        SaveSettingsCommand = new RelayCommand(() => _ = SaveSettingsAsync());
        PrepareLiveCommand = new RelayCommand(() => _ = PrepareLiveEnvironmentAsync());
        CenterOnSelectedCommand = new RelayCommand(CenterOnSelectedTrack, () => SelectedTrack?.HasPosition == true);
        ResetCenterCommand = new RelayCommand(ResetCenterToObservationPoint);
        ToggleSelectedOnlyCommand = new RelayCommand(() => ShowSelectedOnly = !ShowSelectedOnly);
        ResetFiltersCommand = new RelayCommand(ResetFilters);
        _playbackTimer.Tick += (_, _) => AdvancePlayback();
        _uiRefreshTimer.Tick += (_, _) => PublishTrackSnapshot();
    }

    public event EventHandler? VisualStateChanged;
    public ObservableCollection<TrackViewModel> Tracks => _trackListState.Tracks;
    public ObservableCollection<SdrDeviceInfo> AvailableDevices => _availableDevices;
    public ObservableCollection<string> RecentEvents => _liveStatusState.RecentEvents;
    public Array SortModes => Enum.GetValues(typeof(TrackSortMode));
    public RelayCommand StartLiveCommand { get; }
    public RelayCommand StopLiveCommand { get; }
    public RelayCommand RefreshDevicesCommand { get; }
    public RelayCommand StartPlaybackCommand { get; }
    public RelayCommand PausePlaybackCommand { get; }
    public RelayCommand DownloadMapCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }
    public RelayCommand PrepareLiveCommand { get; }
    public RelayCommand CenterOnSelectedCommand { get; }
    public RelayCommand ResetCenterCommand { get; }
    public RelayCommand ToggleSelectedOnlyCommand { get; }
    public RelayCommand ResetFiltersCommand { get; }
    public string StatusText { get => _liveStatusState.StatusText; private set { _liveStatusState.SetStatus(value); RaisePropertyChanged(); } }
    public string DeviceStatusText { get => _deviceStatusText; private set => SetProperty(ref _deviceStatusText, value); }
    public string ModeText { get => LiveStatusState.FormatMode(_liveStatusState.Mode); private set { } }
    public string RecognitionStatusText { get => _recognitionStatusText; private set => SetProperty(ref _recognitionStatusText, value); }
    public string DecoderStatusText { get => _decoderStatusText; private set => SetProperty(ref _decoderStatusText, value); }
    public string BackendReadinessText { get => _backendReadinessText; private set => SetProperty(ref _backendReadinessText, value); }
    public string DriverReadinessText { get => _driverReadinessText; private set => SetProperty(ref _driverReadinessText, value); }
    public string LiveReadinessText { get => _liveReadinessText; private set => SetProperty(ref _liveReadinessText, value); }
    public string SetupHeadlineText { get => _setupHeadlineText; private set => SetProperty(ref _setupHeadlineText, value); }
    public string SetupGuidanceText { get => _setupGuidanceText; private set => SetProperty(ref _setupGuidanceText, value); }
    public string LiveSourceText { get => _liveStatusState.LiveSourceText; private set { _liveStatusState.SetSource(value); RaisePropertyChanged(); } }
    public string MapStatusText { get => _mapStatusText; private set => SetProperty(ref _mapStatusText, value); }
    public string PortableStatusText { get => _portableStatusText; private set => SetProperty(ref _portableStatusText, value); }
    public string WorkspaceStatusText { get => _workspaceStatusText; private set => SetProperty(ref _workspaceStatusText, value); }
    public string CapabilitiesText { get => _capabilitiesText; private set => SetProperty(ref _capabilitiesText, value); }
    public string MessagesPerSecondText => _liveStatusState.MessagesPerSecondText;
    public string TrackMetricsText { get => _trackMetricsText; private set => SetProperty(ref _trackMetricsText, value); }
    public string SourceSummaryText { get => _sourceSummaryText; private set => SetProperty(ref _sourceSummaryText, value); }
    public string DecoderFailureText { get => _decoderFailureText; private set => SetProperty(ref _decoderFailureText, value); }
    public double CenterLatitude { get => _settings.CenterLatitude; set { _settings.CenterLatitude = value; RaisePropertyChanged(); NotifyVisualStateChanged(); PublishTrackSnapshot(); } }
    public double CenterLongitude { get => _settings.CenterLongitude; set { _settings.CenterLongitude = value; RaisePropertyChanged(); NotifyVisualStateChanged(); PublishTrackSnapshot(); } }
    public double RadiusKilometers { get => _settings.DisplayRadiusKilometers; set { _settings.DisplayRadiusKilometers = value; RaisePropertyChanged(); NotifyVisualStateChanged(); PublishTrackSnapshot(); } }
    public double Gain { get => _settings.Gain; set { _settings.Gain = value; RaisePropertyChanged(); } }
    public int PpmCorrection { get => _settings.PpmCorrection; set { _settings.PpmCorrection = value; RaisePropertyChanged(); } }
    public int SampleRate { get => _settings.SampleRate; set { _settings.SampleRate = value; RaisePropertyChanged(); } }
    public string DecoderHost { get => _settings.DecoderHost; set { _settings.DecoderHost = value; RaisePropertyChanged(); } }
    public int DecoderPort { get => _settings.DecoderPort; set { _settings.DecoderPort = value; RaisePropertyChanged(); } }
    public bool UseSimulationFallback { get => _settings.UseSimulationFallback; set { _settings.UseSimulationFallback = value; RaisePropertyChanged(); } }
    public int ActiveTrackCount => Tracks.Count(track => !track.IsStale);
    public bool IsPlaybackMode => _isPlaybackMode;
    public int SelectedZoom { get => _mapViewState.SelectedZoom; set { if (_mapViewState.SelectedZoom != value) { _mapViewState.SelectedZoom = value; RaisePropertyChanged(); NotifyVisualStateChanged(); } } }
    public MapLayerType SelectedMapLayer { get => _mapViewState.SelectedMapLayer; set { if (_mapViewState.SelectedMapLayer != value) { _mapViewState.SelectedMapLayer = value; RaisePropertyChanged(); _ = RefreshMapPackagesAsync(); } } }
    public TrackSortMode SelectedSortMode { get => _selectedSortMode; set { if (SetProperty(ref _selectedSortMode, value)) PublishTrackSnapshot(); } }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) PublishTrackSnapshot(); } }
    public bool WithPositionOnly { get => _withPositionOnly; set { if (SetProperty(ref _withPositionOnly, value)) PublishTrackSnapshot(); } }
    public bool AirborneOnly { get => _airborneOnly; set { if (SetProperty(ref _airborneOnly, value)) PublishTrackSnapshot(); } }
    public bool ShowSelectedOnly { get => _showSelectedOnly; set { if (SetProperty(ref _showSelectedOnly, value)) PublishTrackSnapshot(); } }
    public string MinAltitudeText { get => _minAltitudeText; set { if (SetProperty(ref _minAltitudeText, value)) PublishTrackSnapshot(); } }
    public string MaxAltitudeText { get => _maxAltitudeText; set { if (SetProperty(ref _maxAltitudeText, value)) PublishTrackSnapshot(); } }
    public string MinSpeedText { get => _minSpeedText; set { if (SetProperty(ref _minSpeedText, value)) PublishTrackSnapshot(); } }
    public string MaxSpeedText { get => _maxSpeedText; set { if (SetProperty(ref _maxSpeedText, value)) PublishTrackSnapshot(); } }
    public string MaxDistanceText { get => _maxDistanceText; set { if (SetProperty(ref _maxDistanceText, value)) PublishTrackSnapshot(); } }
    public string HistoryIcaoText { get => _historyIcaoText; set => SetProperty(ref _historyIcaoText, value); }
    public string HistoryFromText { get => _historyFromText; set => SetProperty(ref _historyFromText, value); }
    public string HistoryToText { get => _historyToText; set => SetProperty(ref _historyToText, value); }
    public bool HistorySelectedOnly { get => _historySelectedOnly; set => SetProperty(ref _historySelectedOnly, value); }
    public bool HistoryWithCoordinatesOnly { get => _historyWithCoordinatesOnly; set => SetProperty(ref _historyWithCoordinatesOnly, value); }
    public MapPackageInfo? CurrentMapPackage { get => _mapViewState.CurrentMapPackage; private set { if (!EqualityComparer<MapPackageInfo?>.Default.Equals(_mapViewState.CurrentMapPackage, value)) { _mapViewState.CurrentMapPackage = value; RaisePropertyChanged(); NotifyVisualStateChanged(); } } }
    public string? SelectedDeviceId { get => _settings.PreferredDeviceId; set { if (_settings.PreferredDeviceId != value) { _settings.PreferredDeviceId = value; RaisePropertyChanged(); RaisePropertyChanged(nameof(SelectedDeviceSummary)); } } }
    public string SelectedDeviceSummary => string.IsNullOrWhiteSpace(_settings.PreferredDeviceId) ? "Предпочтительный RTL-SDR не выбран" : _availableDevices.FirstOrDefault(item => item.DeviceId == _settings.PreferredDeviceId) is { } device ? $"Выбрано устройство: {device.Name}" : "Сохранённый RTL-SDR сейчас не подключён";
    public TrackViewModel? SelectedTrack { get => _trackListState.SelectedTrack; set { if (!ReferenceEquals(_trackListState.SelectedTrack, value)) { _trackListState.SetSelectedTrack(value); RaisePropertyChanged(); CenterOnSelectedCommand.RaiseCanExecuteChanged(); PublishTrackSnapshot(); } } }

    public async Task InitializeAsync()
    {
        _settings = await _storageService.GetSettingsAsync(CancellationToken.None);
        _mapViewState.SelectedZoom = _settings.DefaultZoom;
        _selectedSortMode = Enum.TryParse<TrackSortMode>(_settings.DefaultSortMode, true, out var sortMode) ? sortMode : TrackSortMode.LastSeen;
        _withPositionOnly = _settings.DefaultFilterWithPositionOnly;
        _airborneOnly = _settings.DefaultFilterAirborneOnly;
        _mapViewState.CaptureObservationCenter(_settings.CenterLatitude, _settings.CenterLongitude);
        RaisePropertyChanged(nameof(CenterLatitude)); RaisePropertyChanged(nameof(CenterLongitude)); RaisePropertyChanged(nameof(RadiusKilometers)); RaisePropertyChanged(nameof(SelectedZoom)); RaisePropertyChanged(nameof(Gain)); RaisePropertyChanged(nameof(PpmCorrection)); RaisePropertyChanged(nameof(SampleRate)); RaisePropertyChanged(nameof(DecoderHost)); RaisePropertyChanged(nameof(DecoderPort)); RaisePropertyChanged(nameof(UseSimulationFallback)); RaisePropertyChanged(nameof(SelectedDeviceId)); RaisePropertyChanged(nameof(SelectedDeviceSummary));
        PortableStatusText = $"Portable storage: {_storageCompatibility.Message}";
        WorkspaceStatusText = $"Папки: data={_workspace.DataRoot}, maps={_workspace.MapsRoot}, recordings={_workspace.RecordingsRoot}, logs={_workspace.LogsRoot}";
        _recognitionLookup = await _storageService.GetRecognitionLookupAsync(CancellationToken.None);
        RecognitionStatusText = $"Распознавание: {_recognitionLookup.Count} записей";
        await RefreshMapPackagesAsync();
        await RefreshDevicesAsync();
        await RefreshEnvironmentStatusAsync();
        ApplyDecoderStatus(_decoderProcessService.CurrentStatus);
        UpdateCapabilitiesText();
        PublishTrackSnapshot();
        _uiRefreshTimer.Start();
        LogEvent("Приложение инициализировано");
        StatusText = "Клиент готов";
    }

    public async Task RefreshDevicesAsync()
    {
        try
        {
            var devices = await _deviceDetector.DetectAsync(CancellationToken.None);
            _availableDevices.Clear();
            foreach (var device in devices) _availableDevices.Add(device);
            DeviceStatusText = devices.Count switch { 0 => "RTL-SDR: устройство не найдено", 1 => $"RTL-SDR: {devices[0].Name}", _ => $"RTL-SDR: найдено {devices.Count} совместимых устройств" };
            if (devices.Count == 1) _settings.PreferredDeviceId ??= devices[0].DeviceId; else if (devices.Count > 1 && !devices.Any(device => device.DeviceId == _settings.PreferredDeviceId)) _settings.PreferredDeviceId = null;
            RaisePropertyChanged(nameof(SelectedDeviceId));
            RaisePropertyChanged(nameof(SelectedDeviceSummary));
            await RefreshEnvironmentStatusAsync();
        }
        catch (Exception ex)
        {
            DeviceStatusText = $"RTL-SDR: ошибка проверки ({ex.Message})";
            LiveReadinessText = "Live: недоступен";
            UpdateCapabilitiesText();
        }
    }

    public async Task StartLiveAsync()
    {
        if (_isLiveRunning) return;
        PausePlayback();
        _isPlaybackMode = false;
        RaisePropertyChanged(nameof(IsPlaybackMode));
        _liveCts = new CancellationTokenSource();
        await _storageService.SaveSettingsAsync(_settings, _liveCts.Token);
        var environment = await PrepareLiveEnvironmentAsync(_liveCts.Token);
        if (!environment.CanStartLive) { _liveCts.Dispose(); _liveCts = null; return; }
        var decoderStatus = await _decoderProcessService.StartAsync(_settings, _liveCts.Token);
        if (!decoderStatus.IsReady) { StatusText = TranslateDecoderStatus(decoderStatus.Message); await RefreshEnvironmentStatusAsync(); _liveCts.Dispose(); _liveCts = null; return; }
        _isLiveRunning = true;
        StartLiveCommand.RaiseCanExecuteChanged();
        StopLiveCommand.RaiseCanExecuteChanged();
        _liveStatusState.SetMode(AppMode.Live, "Источник: bundled dump1090");
        RaisePropertyChanged(nameof(ModeText));
        RaisePropertyChanged(nameof(LiveSourceText));
        StatusText = "Запуск live-приёма...";
        LogEvent("Live запущен");
        UpdateCapabilitiesText();
        _ = Task.Run(async () =>
        {
            try
            {
                await InvokeOnUiAsync(() => StatusText = "Live-приём работает через bundled dump1090");
                await foreach (var message in ReadWithFallbackAsync(_liveDecoder, _liveCts.Token))
                {
                    RegisterLiveMessage();
                    AircraftTrack track;
                    var pointAppended = false;
                    lock (_trackerLock)
                    {
                        var before = _trackerService.TryGetTrack(message.Icao, out var existing) ? existing?.Points.Count ?? 0 : 0;
                        track = _trackerService.ProcessMessage(message, _recognitionLookup, _settings.MaxTrailPoints);
                        pointAppended = track.Points.Count > before;
                    }
                    await _storageService.UpsertTrackAsync(track, _liveCts.Token);
                    if (pointAppended) await _storageService.AppendTrackPointAsync(track.Icao, track.Points[^1], _liveCts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                var fallbackUsed = _settings.UseSimulationFallback && _liveCts is not null && !_liveCts.IsCancellationRequested && await TryRunSimulationFallbackAsync(_liveCts.Token);
                if (!fallbackUsed) await InvokeOnUiAsync(() => { StatusText = $"Ошибка live-приёма: {ex.Message}"; LogEvent($"Ошибка decoder: {ex.Message}"); });
            }
            finally
            {
                await InvokeOnUiAsync(() =>
                {
                    _isLiveRunning = false;
                    StartLiveCommand.RaiseCanExecuteChanged();
                    StopLiveCommand.RaiseCanExecuteChanged();
                    if (!_isPlaybackMode)
                    {
                        _liveStatusState.SetMode(AppMode.Idle);
                        RaisePropertyChanged(nameof(ModeText));
                    }
                    PublishTrackSnapshot();
                    UpdateCapabilitiesText();
                });
                await _decoderProcessService.StopAsync(CancellationToken.None);
                await RefreshEnvironmentStatusAsync();
                LogEvent("Live остановлен");
            }
        });
    }

    public Task StopLiveAsync()
    {
        _liveCts?.Cancel();
        _isLiveRunning = false;
        StartLiveCommand.RaiseCanExecuteChanged();
        StopLiveCommand.RaiseCanExecuteChanged();
        _liveStatusState.SetMode(AppMode.Idle);
        RaisePropertyChanged(nameof(ModeText));
        StatusText = "Live-приём остановлен";
        UpdateCapabilitiesText();
        LogEvent("Остановка live");
        return _decoderProcessService.StopAsync(CancellationToken.None);
    }

    public async Task<int> ImportRecognitionAsync(string path)
    {
        var imported = await _recognitionImportService.ImportAsync(path, CancellationToken.None);
        _recognitionLookup = await _storageService.GetRecognitionLookupAsync(CancellationToken.None);
        RecognitionStatusText = $"Распознавание: {_recognitionLookup.Count} записей";
        StatusText = $"Импортировано записей: {imported}";
        LogEvent($"Импорт recognition: {Path.GetFileName(path)}");
        return imported;
    }

    public async Task ExportTracksAsync(string path)
    {
        var filter = BuildHistoryFilter();
        await _trackExportService.ExportAsync(path, filter.Icao, filter.FromUtc, filter.ToUtc, filter.WithCoordinatesOnly, CancellationToken.None);
        StatusText = $"CSV экспортирован: {path}";
        LogEvent($"CSV экспорт: {Path.GetFileName(path)}");
    }

    public async Task StartPlaybackAsync()
    {
        await StopLiveAsync();
        var filter = BuildHistoryFilter();
        var tracks = await _storageService.GetStoredTracksAsync(filter.FromUtc, filter.ToUtc, filter.Icao, CancellationToken.None);
        if (filter.WithCoordinatesOnly) tracks = tracks.Where(track => track.Points.Count > 0).ToList();
        if (tracks.Count == 0) { StatusText = "Нет архивных треков для playback"; return; }
        _playbackCoordinator.Load(tracks, _settings.MaxTrailPoints);
        if (!_playbackCoordinator.HasFrames) { StatusText = "В истории нет точек с координатами для playback"; return; }
        _isPlaybackMode = true;
        RaisePropertyChanged(nameof(IsPlaybackMode));
        _liveStatusState.SetMode(AppMode.Playback);
        RaisePropertyChanged(nameof(ModeText));
        StatusText = "Playback запущен";
        LogEvent("Playback запущен");
        UpdateCapabilitiesText();
        _playbackTimer.Start();
    }

    public void PausePlayback() { _playbackTimer.Stop(); if (_isPlaybackMode) { StatusText = "Playback на паузе"; LogEvent("Playback на паузе"); } }

    public async Task RefreshMapPackagesAsync()
    {
        Directory.CreateDirectory(_workspace.MapsRoot);
        var packages = new List<MapPackageInfo>();
        foreach (var filePath in Directory.EnumerateFiles(_workspace.MapsRoot, "*.mbtiles", SearchOption.TopDirectoryOnly))
        {
            var package = await _mapTileService.InspectPackageAsync(filePath, CancellationToken.None);
            if (package is null) continue;
            await _storageService.SaveMapPackageAsync(package, CancellationToken.None);
            packages.Add(package);
        }
        if (packages.Count == 0) packages.AddRange((await _storageService.GetMapPackagesAsync(CancellationToken.None)).Where(package => File.Exists(package.FilePath)));
        CurrentMapPackage = packages.FirstOrDefault(package => package.LayerType == SelectedMapLayer) ?? packages.FirstOrDefault();
        MapStatusText = CurrentMapPackage is null ? $"Карты: не найдены в {_workspace.MapsRoot}. Используется фоновая сетка." : $"Карты: {CurrentMapPackage.Name}";
        UpdateCapabilitiesText();
        NotifyVisualStateChanged();
    }

    public async Task SaveSettingsAsync()
    {
        _settings.DefaultSortMode = SelectedSortMode.ToString();
        _settings.DefaultFilterWithPositionOnly = WithPositionOnly;
        _settings.DefaultFilterAirborneOnly = AirborneOnly;
        _mapViewState.CaptureObservationCenter(_settings.CenterLatitude, _settings.CenterLongitude);
        await _storageService.SaveSettingsAsync(_settings, CancellationToken.None);
        await RefreshEnvironmentStatusAsync();
        StatusText = "Настройки сохранены";
        LogEvent("Настройки сохранены");
    }

    public async Task<byte[]?> GetTileBytesAsync(int zoom, int x, int y, CancellationToken cancellationToken) => CurrentMapPackage is null ? null : await _mapTileService.GetTileBytesAsync(CurrentMapPackage, zoom, x, y, cancellationToken);
    public async Task<LiveEnvironmentStatus> PrepareLiveEnvironmentAsync(CancellationToken cancellationToken = default) { StatusText = "Диагностика live-режима..."; var environment = await _driverBootstrapService.EnsureReadyAsync(_settings, cancellationToken); ApplyEnvironmentStatus(environment); StatusText = environment.Message; UpdateCapabilitiesText(); return environment; }
    public bool ShouldDisplayLabel(TrackViewModel track) => track.IsSelected || (track.HasPosition && !track.IsStale && SelectedZoom >= 9);

    private async IAsyncEnumerable<AircraftMessage> ReadWithFallbackAsync(IAdsbDecoderAdapter preferredAdapter, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var useFallback = false;
        await using var enumerator = preferredAdapter.ReadMessagesAsync(_settings, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            AircraftMessage current;
            try { if (!await enumerator.MoveNextAsync()) yield break; current = enumerator.Current; }
            catch when (!ReferenceEquals(preferredAdapter, _simulationDecoder) && _settings.UseSimulationFallback) { useFallback = true; break; }
            yield return current;
        }
        if (!useFallback) yield break;
        await InvokeOnUiAsync(() =>
        {
            StatusText = "Основной источник live недоступен, включён simulation fallback";
            _liveStatusState.SetMode(AppMode.SimulationFallback, "Источник: simulation fallback");
            RaisePropertyChanged(nameof(ModeText));
            RaisePropertyChanged(nameof(LiveSourceText));
            LogEvent("Активирован simulation fallback");
        });
        await foreach (var message in _simulationDecoder.ReadMessagesAsync(_settings, cancellationToken)) yield return message;
    }

    private void AdvancePlayback()
    {
        if (!_playbackCoordinator.TryGetNextFrame(out var frame) || frame is null) { _playbackTimer.Stop(); _isPlaybackMode = false; RaisePropertyChanged(nameof(IsPlaybackMode)); StatusText = "Playback завершён"; _liveStatusState.SetMode(AppMode.Idle); RaisePropertyChanged(nameof(ModeText)); LogEvent("Playback завершён"); return; }
        ReplaceDisplayedTracks(frame.Tracks, frame.TimestampUtc);
        RaisePropertyChanged(nameof(ModeText));
    }

    private async Task RefreshEnvironmentStatusAsync() => ApplyEnvironmentStatus(await _driverBootstrapService.InspectAsync(_settings, CancellationToken.None));

    private void ApplyEnvironmentStatus(LiveEnvironmentStatus environment)
    {
        BackendReadinessText = $"Backend: {(environment.BackendAvailable ? "готов" : "не найден")}";
        DriverReadinessText = $"RTL-SDR driver: {(environment.DriverInstalled ? "готов" : "не готов")}";
        LiveReadinessText = $"Live: {(environment.CanStartLive ? "доступен" : "недоступен")} ({environment.Message})";
        SetupHeadlineText = environment.Issue switch { LiveEnvironmentIssue.None => "Portable-окружение готово", LiveEnvironmentIssue.NoCompatibleDevice => "RTL-SDR не обнаружен", LiveEnvironmentIssue.MultipleDevicesDetected => "Выберите одно устройство RTL-SDR", LiveEnvironmentIssue.DriverMissing => "Требуется установка драйвера RTL-SDR", LiveEnvironmentIssue.BackendMissing => "Bundled dump1090 отсутствует", LiveEnvironmentIssue.PortBusy => "SBS-1 порт уже занят", _ => "Live-режим требует внимания" };
        SetupGuidanceText = environment.Guidance ?? environment.Message;
        _liveStatusState.ApplyEnvironmentStatus(environment, DecoderHost, DecoderPort);
        RaisePropertyChanged(nameof(LiveSourceText));
        if (environment.DeviceDetected && !string.IsNullOrWhiteSpace(environment.DeviceName)) DeviceStatusText = $"RTL-SDR: {environment.DeviceName}";
        RaisePropertyChanged(nameof(SelectedDeviceSummary));
        UpdateCapabilitiesText(environment);
        UpdateSourceSummary();
    }

    private async Task<bool> TryRunSimulationFallbackAsync(CancellationToken cancellationToken)
    {
        try
        {
            await InvokeOnUiAsync(() => { StatusText = "Основной источник live завершился, включён simulation fallback"; _liveStatusState.SetMode(AppMode.SimulationFallback, "Источник: simulation fallback"); RaisePropertyChanged(nameof(ModeText)); RaisePropertyChanged(nameof(LiveSourceText)); LogEvent("Simulation fallback активирован"); });
            await foreach (var message in _simulationDecoder.ReadMessagesAsync(_settings, cancellationToken)) { RegisterLiveMessage(); lock (_trackerLock) _trackerService.ProcessMessage(message, _recognitionLookup, _settings.MaxTrailPoints); }
            return true;
        }
        catch { return false; }
    }

    private void PublishTrackSnapshot()
    {
        IReadOnlyList<AircraftTrack> visibleTracks;
        lock (_trackerLock) visibleTracks = _trackerService.GetVisibleTracks(DateTime.UtcNow, TimeSpan.FromMinutes(_settings.ActiveTargetTimeoutMinutes), TimeSpan.FromSeconds(_settings.StaleTargetDisplaySeconds));
        ReplaceDisplayedTracks(visibleTracks, DateTime.UtcNow);
    }

    private void ReplaceDisplayedTracks(IReadOnlyList<AircraftTrack> tracks, DateTime referenceUtc)
    {
        var selectedIcao = SelectedTrack?.Icao;
        var filterState = BuildFilterState();
        var activeWindow = TimeSpan.FromMinutes(_settings.ActiveTargetTimeoutMinutes);
        var staleWindow = TimeSpan.FromSeconds(_settings.StaleTargetDisplaySeconds);
        var projected = tracks.Select(track => new TrackViewModel(track, CenterLatitude, CenterLongitude, _trackerService.GetVisualState(track, referenceUtc, activeWindow, staleWindow), string.Equals(track.Icao, selectedIcao, StringComparison.OrdinalIgnoreCase))).Where(track => MatchesFilter(track, filterState, selectedIcao)).ToList();
        projected = SelectedSortMode switch { TrackSortMode.Distance => projected.OrderBy(track => track.DistanceKm ?? double.MaxValue).ThenByDescending(track => track.Model.LastSeenUtc).ToList(), TrackSortMode.Altitude => projected.OrderByDescending(track => track.Model.AltitudeFeet ?? int.MinValue).ThenByDescending(track => track.Model.LastSeenUtc).ToList(), TrackSortMode.Speed => projected.OrderByDescending(track => track.Model.GroundSpeedKnots ?? double.MinValue).ThenByDescending(track => track.Model.LastSeenUtc).ToList(), _ => projected.OrderByDescending(track => track.Model.LastSeenUtc).ToList() };
        _trackListState.ReplaceTracks(projected, selectedIcao);
        RaisePropertyChanged(nameof(Tracks));
        RaisePropertyChanged(nameof(SelectedTrack));
        RaisePropertyChanged(nameof(ActiveTrackCount));
        CenterOnSelectedCommand.RaiseCanExecuteChanged();
        UpdateTrackMetrics(referenceUtc);
        NotifyVisualStateChanged();
    }

    private TrackFilterState BuildFilterState() => new() { SearchText = SearchText.Trim(), WithPositionOnly = WithPositionOnly, AirborneOnly = AirborneOnly, MinAltitudeFeet = ParseNullableInt(MinAltitudeText), MaxAltitudeFeet = ParseNullableInt(MaxAltitudeText), MinSpeedKnots = ParseNullableDouble(MinSpeedText), MaxSpeedKnots = ParseNullableDouble(MaxSpeedText), MaxDistanceKm = ParseNullableDouble(MaxDistanceText), ShowSelectedOnly = ShowSelectedOnly };
    private bool MatchesFilter(TrackViewModel track, TrackFilterState filterState, string? selectedIcao) => (!filterState.ShowSelectedOnly || string.Equals(track.Icao, selectedIcao, StringComparison.OrdinalIgnoreCase)) && (!filterState.WithPositionOnly || track.HasPosition) && (!filterState.AirborneOnly || (track.Model.AltitudeFeet.HasValue && track.Model.AltitudeFeet > 0)) && (string.IsNullOrWhiteSpace(filterState.SearchText) || track.Icao.Contains(filterState.SearchText, StringComparison.OrdinalIgnoreCase) || track.Callsign.Contains(filterState.SearchText, StringComparison.OrdinalIgnoreCase)) && (!filterState.MinAltitudeFeet.HasValue || (track.Model.AltitudeFeet.HasValue && track.Model.AltitudeFeet >= filterState.MinAltitudeFeet)) && (!filterState.MaxAltitudeFeet.HasValue || (track.Model.AltitudeFeet.HasValue && track.Model.AltitudeFeet <= filterState.MaxAltitudeFeet)) && (!filterState.MinSpeedKnots.HasValue || (track.Model.GroundSpeedKnots.HasValue && track.Model.GroundSpeedKnots >= filterState.MinSpeedKnots)) && (!filterState.MaxSpeedKnots.HasValue || (track.Model.GroundSpeedKnots.HasValue && track.Model.GroundSpeedKnots <= filterState.MaxSpeedKnots)) && (!filterState.MaxDistanceKm.HasValue || (track.DistanceKm.HasValue && track.DistanceKm <= filterState.MaxDistanceKm));

    private void UpdateTrackMetrics(DateTime referenceUtc)
    {
        var active = 0; var stale = 0; var withPosition = 0;
        var activeWindow = TimeSpan.FromMinutes(_settings.ActiveTargetTimeoutMinutes);
        var staleWindow = TimeSpan.FromSeconds(_settings.StaleTargetDisplaySeconds);
        lock (_trackerLock)
        {
            foreach (var track in _trackerService.GetVisibleTracks(referenceUtc, activeWindow, staleWindow))
            {
                if (track.HasPosition) withPosition++;
                if (_trackerService.GetVisualState(track, referenceUtc, activeWindow, staleWindow) == TrackVisualState.Active) active++; else stale++;
            }
        }
        TrackMetricsText = $"Треки: {active} active / {stale} stale / {withPosition} with position";
        SourceSummaryText = $"{_liveStatusState.LiveSourceText}; stale targets: {stale}; visible with position: {withPosition}";
    }

    private void UpdateCapabilitiesText(LiveEnvironmentStatus? environment = null)
    {
        var liveReady = environment?.CanStartLive ?? LiveReadinessText.StartsWith("Live: доступен", StringComparison.OrdinalIgnoreCase);
        CapabilitiesText = $"Доступно сейчас: {string.Join(", ", new[] { liveReady ? "live" : "live недоступен", "playback", "история", CurrentMapPackage is null ? "карты отсутствуют" : "карты" })}";
    }

    private void RegisterLiveMessage()
    {
        _liveStatusState.RegisterMessage();
        RaisePropertyChanged(nameof(MessagesPerSecondText));
    }

    private void CenterOnSelectedTrack() { if (SelectedTrack?.Model.Latitude is double lat && SelectedTrack.Model.Longitude is double lon) { CenterLatitude = lat; CenterLongitude = lon; StatusText = $"Карта центрирована на {SelectedTrack.Icao}"; } }
    private void ResetCenterToObservationPoint() { var home = _mapViewState.GetHomeCenter(); CenterLatitude = home.Latitude; CenterLongitude = home.Longitude; StatusText = "Карта возвращена к observation center"; }
    private void ResetFilters() { SearchText = string.Empty; WithPositionOnly = false; AirborneOnly = false; ShowSelectedOnly = false; MinAltitudeText = string.Empty; MaxAltitudeText = string.Empty; MinSpeedText = string.Empty; MaxSpeedText = string.Empty; MaxDistanceText = string.Empty; StatusText = "Фильтры сброшены"; }
    private void LogEvent(string message) { _liveStatusState.LogEvent(message); RaisePropertyChanged(nameof(RecentEvents)); }
    private void NotifyVisualStateChanged() => VisualStateChanged?.Invoke(this, EventArgs.Empty);
    private void ApplyDecoderStatus(DecoderProcessStatus status)
    {
        DecoderStatusText = TranslateDecoderStatus(status.Message);
        DecoderFailureText = TranslateDecoderFailure(status);
        UpdateSourceSummary(status);
    }

    private void UpdateSourceSummary(DecoderProcessStatus? status = null)
    {
        var currentStatus = status ?? _decoderProcessService.CurrentStatus;
        var source = string.IsNullOrWhiteSpace(_liveStatusState.LiveSourceText) ? "Источник: idle" : _liveStatusState.LiveSourceText;
        var state = currentStatus.State switch
        {
            DecoderProcessState.Ready => "decoder ready",
            DecoderProcessState.Starting => "decoder starting",
            DecoderProcessState.Disabled => "decoder disabled",
            DecoderProcessState.Failed => "decoder failed",
            _ => "decoder stopped"
        };
        SourceSummaryText = $"{source}; {state}; port {(currentStatus.PortReachable ? "reachable" : "not reachable")}";
    }

    private HistoryFilter BuildHistoryFilter()
    {
        var selectedIcao = HistorySelectedOnly ? SelectedTrack?.Icao : null;
        var explicitIcao = string.IsNullOrWhiteSpace(HistoryIcaoText) ? null : HistoryIcaoText.Trim().ToUpperInvariant();
        return new HistoryFilter(
            selectedIcao ?? explicitIcao,
            ParseHistoryDate(HistoryFromText, isEndBoundary: false),
            ParseHistoryDate(HistoryToText, isEndBoundary: true),
            HistoryWithCoordinatesOnly);
    }

    private static DateTime? ParseHistoryDate(string text, bool isEndBoundary)
    {
        if (string.IsNullOrWhiteSpace(text) || !DateTime.TryParse(text, out var parsed))
        {
            return null;
        }

        if (parsed.TimeOfDay == TimeSpan.Zero)
        {
            var localBoundary = isEndBoundary ? parsed.Date.AddDays(1).AddTicks(-1) : parsed.Date;
            return DateTime.SpecifyKind(localBoundary, DateTimeKind.Local).ToUniversalTime();
        }

        return parsed.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Local).ToUniversalTime()
            : parsed.ToUniversalTime();
    }

    private static string TranslateDecoderFailure(DecoderProcessStatus status) => status.FailureReason switch
    {
        DecoderFailureReason.None when status.State == DecoderProcessState.Ready => "Decoder: backend готов и отвечает на порт",
        DecoderFailureReason.None => "Decoder: явных ошибок нет",
        DecoderFailureReason.AutoStartDisabled => "Decoder: автозапуск отключён в настройках",
        DecoderFailureReason.BackendMissing => $"Decoder: bundled backend не найден{(string.IsNullOrWhiteSpace(status.ExecutablePath) ? string.Empty : $" ({status.ExecutablePath})")}",
        DecoderFailureReason.PortBusy => "Decoder: SBS-1 порт уже занят другим процессом",
        DecoderFailureReason.PortUnavailable => "Decoder: процесс стартовал, но SBS-1 порт не поднялся вовремя",
        DecoderFailureReason.ProcessExitedEarly => $"Decoder: backend завершился до готовности порта. stderr: {status.LastErrorLine ?? "нет строки ошибки"}",
        DecoderFailureReason.StartFailed => "Decoder: backend не удалось запустить",
        _ => $"Decoder: {status.Message}"
    };

    private static int? ParseNullableInt(string text) => int.TryParse(text, out var value) ? value : null;
    private static double? ParseNullableDouble(string text) => double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var invariant) ? invariant : double.TryParse(text, out var current) ? current : null;
    private static string TranslateDecoderStatus(string message) => message.Replace("dump1090: stopped", "Источник: остановлен", StringComparison.Ordinal).Replace("dump1090: starting bundled backend", "Источник: запуск bundled dump1090", StringComparison.Ordinal).Replace("dump1090: ready", "Источник: decoder готов", StringComparison.Ordinal).Replace("dump1090: emitted errors", "Источник: backend сообщает об ошибках", StringComparison.Ordinal).Replace("dump1090: running", "Источник: backend работает", StringComparison.Ordinal).Replace("dump1090 exited", "Источник: backend завершился", StringComparison.Ordinal).Replace("dump1090: auto-start disabled", "Источник: автозапуск отключён", StringComparison.Ordinal).Replace("dump1090: bundled backend missing", "Источник: bundled dump1090 не найден", StringComparison.Ordinal);
    private static Task InvokeOnUiAsync(Action action) { var dispatcher = Application.Current?.Dispatcher; if (dispatcher is null) { action(); return Task.CompletedTask; } return dispatcher.InvokeAsync(action).Task; }

    private sealed record HistoryFilter(string? Icao, DateTime? FromUtc, DateTime? ToUtc, bool WithCoordinatesOnly);
}


