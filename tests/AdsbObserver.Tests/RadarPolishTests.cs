using AdsbObserver.App.ViewModels;
using AdsbObserver.Core.Models;
using AdsbObserver.Core.Services;
using Xunit;

namespace AdsbObserver.Tests;

public sealed class RadarPolishTests
{
    [Fact]
    public void AircraftTrackerService_DeduplicatesPoints_AndTrimsTrail()
    {
        var tracker = new AircraftTrackerService();
        var lookup = new Dictionary<string, AircraftRecognitionRecord>(StringComparer.OrdinalIgnoreCase);
        var timestamp = DateTime.UtcNow;

        tracker.ProcessMessage(new AircraftMessage("ABC123", timestamp, "TEST01", 25.0, 55.0, 10000, 220, 90, 0, null, null), lookup, maxTrailPoints: 2);
        tracker.ProcessMessage(new AircraftMessage("ABC123", timestamp.AddSeconds(1), "TEST01", 25.0, 55.0, 10100, 225, 91, 0, null, null), lookup, maxTrailPoints: 2);
        tracker.ProcessMessage(new AircraftMessage("ABC123", timestamp.AddSeconds(2), "TEST01", 25.1, 55.1, 10200, 230, 92, 0, null, null), lookup, maxTrailPoints: 2);
        tracker.ProcessMessage(new AircraftMessage("ABC123", timestamp.AddSeconds(3), "TEST01", 25.2, 55.2, 10300, 235, 93, 0, null, null), lookup, maxTrailPoints: 2);

        Assert.True(tracker.TryGetTrack("ABC123", out var track));
        Assert.NotNull(track);
        Assert.Equal(2, track!.Points.Count);
        Assert.Equal(25.1, track.Points[0].Latitude);
        Assert.Equal(25.2, track.Points[1].Latitude);
    }

    [Fact]
    public void AircraftTrackerService_ReturnsVisualStates()
    {
        var tracker = new AircraftTrackerService();
        var now = DateTime.UtcNow;
        var track = new AircraftTrack { Icao = "ABC123", FirstSeenUtc = now.AddMinutes(-10), LastSeenUtc = now.AddMinutes(-6) };

        Assert.Equal(TrackVisualState.Hidden, tracker.GetVisualState(track, now, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30)));

        track.LastSeenUtc = now.AddMinutes(-5).AddSeconds(-10);
        Assert.Equal(TrackVisualState.Stale, tracker.GetVisualState(track, now, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30)));

        track.LastSeenUtc = now.AddMinutes(-2);
        Assert.Equal(TrackVisualState.Active, tracker.GetVisualState(track, now, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void PlaybackCoordinator_AdvancesFrames_AndResets()
    {
        var coordinator = new PlaybackCoordinator(new PlaybackService());
        var now = DateTime.UtcNow;
        var track = new AircraftTrack { Icao = "ABC123", FirstSeenUtc = now, LastSeenUtc = now };
        track.Points.Add(new AircraftTrackPoint(now, 25.0, 55.0, 10000, 200, 90, 0));
        track.Points.Add(new AircraftTrackPoint(now.AddSeconds(1), 25.1, 55.1, 10100, 210, 91, 0));

        coordinator.Load([track], maxTrailPoints: 10);

        Assert.True(coordinator.HasFrames);
        Assert.True(coordinator.TryGetNextFrame(out var first));
        Assert.NotNull(first);
        Assert.True(coordinator.TryGetNextFrame(out var second));
        Assert.NotNull(second);
        Assert.False(coordinator.TryGetNextFrame(out _));

        coordinator.Reset();
        Assert.False(coordinator.HasFrames);
    }

    [Fact]
    public void TrackListState_PreservesSelection_WhenStillVisible()
    {
        var state = new TrackListState();
        var first = CreateTrackViewModel("ABC123");
        var second = CreateTrackViewModel("DEF456");

        state.ReplaceTracks([first, second], "DEF456");
        Assert.Equal("DEF456", state.SelectedTrack?.Icao);

        var replacement = CreateTrackViewModel("DEF456");
        state.ReplaceTracks([replacement], "DEF456");
        Assert.Equal("DEF456", state.SelectedTrack?.Icao);
    }

    [Fact]
    public void LiveStatusState_TracksMode_Metrics_AndEvents()
    {
        var state = new LiveStatusState();

        state.SetMode(AppMode.Live, "Источник: встроенный dump1090", "Запуск");
        state.LogEvent("Запуск");

        Assert.Equal(AppMode.Live, state.Mode);
        Assert.Equal("Источник: встроенный dump1090", state.LiveSourceText);
        Assert.Single(state.RecentEvents);

        state.RegisterMessage();
        Assert.StartsWith("Сообщений/с:", state.MessagesPerSecondText, StringComparison.Ordinal);
    }

    private static TrackViewModel CreateTrackViewModel(string icao)
    {
        var track = new AircraftTrack
        {
            Icao = icao,
            Callsign = icao,
            FirstSeenUtc = DateTime.UtcNow,
            LastSeenUtc = DateTime.UtcNow,
            Latitude = 25.0,
            Longitude = 55.0
        };
        return new TrackViewModel(track, 25.0, 55.0, TrackVisualState.Active, false);
    }
}
