using System.Collections.ObjectModel;
using AdsbObserver.Core.Models;

namespace AdsbObserver.App.ViewModels;

internal enum AppMode
{
    Idle,
    Live,
    Playback,
    SimulationFallback
}

internal sealed class LiveStatusState
{
    private int _messageCounter;
    private DateTime _metricsWindowStartedUtc = DateTime.UtcNow;

    public AppMode Mode { get; private set; } = AppMode.Idle;
    public string LiveSourceText { get; private set; } = "Источник: bundled dump1090";
    public string StatusText { get; private set; } = "Подготовка окружения...";
    public ObservableCollection<string> RecentEvents { get; } = [];
    public string MessagesPerSecondText { get; private set; } = "Messages/sec: 0";

    public void SetMode(AppMode mode, string? sourceText = null, string? statusText = null)
    {
        Mode = mode;
        if (!string.IsNullOrWhiteSpace(sourceText))
        {
            LiveSourceText = sourceText;
        }

        if (!string.IsNullOrWhiteSpace(statusText))
        {
            StatusText = statusText;
        }
    }

    public void SetStatus(string statusText) => StatusText = statusText;

    public void SetSource(string sourceText) => LiveSourceText = sourceText;

    public void RegisterMessage()
    {
        _messageCounter++;
        var now = DateTime.UtcNow;
        if ((now - _metricsWindowStartedUtc).TotalSeconds >= 1)
        {
            MessagesPerSecondText = $"Messages/sec: {_messageCounter}";
            _messageCounter = 0;
            _metricsWindowStartedUtc = now;
        }
    }

    public void LogEvent(string message)
    {
        RecentEvents.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
        while (RecentEvents.Count > 8)
        {
            RecentEvents.RemoveAt(RecentEvents.Count - 1);
        }
    }

    public static string FormatMode(AppMode mode, DateTime? playbackTimestampUtc = null) => mode switch
    {
        AppMode.Live => "Режим: live",
        AppMode.Playback when playbackTimestampUtc.HasValue => $"Режим: playback @ {playbackTimestampUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}",
        AppMode.Playback => "Режим: playback",
        AppMode.SimulationFallback => "Режим: simulation fallback",
        _ => "Режим: idle"
    };

    public void ApplyEnvironmentStatus(LiveEnvironmentStatus environment, string decoderHost, int decoderPort)
    {
        LiveSourceText = environment.Issue == LiveEnvironmentIssue.PortBusy
            ? $"Источник: внешний SBS-1 {decoderHost}:{decoderPort}"
            : "Источник: bundled dump1090";
    }
}
