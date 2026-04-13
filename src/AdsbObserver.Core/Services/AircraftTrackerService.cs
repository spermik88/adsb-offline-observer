using AdsbObserver.Core.Models;

namespace AdsbObserver.Core.Services;

public sealed class AircraftTrackerService
{
    private readonly Dictionary<string, AircraftTrack> _tracks = new(StringComparer.OrdinalIgnoreCase);

    public AircraftTrack ProcessMessage(
        AircraftMessage message,
        IReadOnlyDictionary<string, AircraftRecognitionRecord> recognitionLookup,
        int maxTrailPoints = int.MaxValue)
    {
        if (!_tracks.TryGetValue(message.Icao, out var track))
        {
            track = new AircraftTrack
            {
                Icao = message.Icao,
                FirstSeenUtc = message.TimestampUtc,
                LastSeenUtc = message.TimestampUtc
            };
            _tracks[message.Icao] = track;
        }

        track.LastSeenUtc = message.TimestampUtc;
        track.Callsign = string.IsNullOrWhiteSpace(message.Callsign) ? track.Callsign : message.Callsign.Trim();
        track.Latitude = message.Latitude ?? track.Latitude;
        track.Longitude = message.Longitude ?? track.Longitude;
        track.AltitudeFeet = message.AltitudeFeet ?? track.AltitudeFeet;
        track.GroundSpeedKnots = message.GroundSpeedKnots ?? track.GroundSpeedKnots;
        track.HeadingDegrees = message.HeadingDegrees ?? track.HeadingDegrees;
        track.VerticalRateFeetPerMinute = message.VerticalRateFeetPerMinute ?? track.VerticalRateFeetPerMinute;
        track.Squawk = string.IsNullOrWhiteSpace(message.Squawk) ? track.Squawk : message.Squawk.Trim();
        track.EmitterCategory = string.IsNullOrWhiteSpace(message.EmitterCategory) ? track.EmitterCategory : message.EmitterCategory.Trim();

        if (recognitionLookup.TryGetValue(message.Icao, out var recognition))
        {
            track.Recognition = recognition;
        }

        if (message.Latitude.HasValue && message.Longitude.HasValue)
        {
            var point = new AircraftTrackPoint(
                message.TimestampUtc,
                message.Latitude.Value,
                message.Longitude.Value,
                message.AltitudeFeet,
                message.GroundSpeedKnots,
                message.HeadingDegrees,
                message.VerticalRateFeetPerMinute);

            if (track.Points.Count == 0 || track.Points[^1].Latitude != point.Latitude || track.Points[^1].Longitude != point.Longitude)
            {
                track.Points.Add(point);
                if (track.Points.Count > maxTrailPoints)
                {
                    track.Points.RemoveRange(0, track.Points.Count - maxTrailPoints);
                }
            }
        }

        return track;
    }

    public bool TryGetTrack(string icao, out AircraftTrack? track) => _tracks.TryGetValue(icao, out track);

    public TrackVisualState GetVisualState(AircraftTrack track, DateTime utcNow, TimeSpan activeWindow, TimeSpan staleWindow)
    {
        var age = utcNow - track.LastSeenUtc;
        if (age <= activeWindow)
        {
            return TrackVisualState.Active;
        }

        return age <= activeWindow + staleWindow
            ? TrackVisualState.Stale
            : TrackVisualState.Hidden;
    }

    public IReadOnlyList<AircraftTrack> GetVisibleTracks(DateTime utcNow, TimeSpan activeWindow, TimeSpan staleWindow)
    {
        return _tracks.Values
            .Where(track => GetVisualState(track, utcNow, activeWindow, staleWindow) != TrackVisualState.Hidden)
            .OrderByDescending(track => track.LastSeenUtc)
            .ToList();
    }

    public IReadOnlyList<AircraftTrack> GetAllTracks()
    {
        return _tracks.Values.OrderBy(track => track.Icao).ToList();
    }

    public void ReplaceTracks(IEnumerable<AircraftTrack> tracks)
    {
        _tracks.Clear();
        foreach (var track in tracks)
        {
            _tracks[track.Icao] = track;
        }
    }
}
