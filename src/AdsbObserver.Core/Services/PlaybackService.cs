using AdsbObserver.Core.Models;

namespace AdsbObserver.Core.Services;

public sealed class PlaybackService
{
    public IReadOnlyList<PlaybackFrame> BuildFrames(IReadOnlyList<AircraftTrack> tracks)
    {
        var events = new SortedDictionary<DateTime, List<AircraftTrackPoint>>();
        var index = new Dictionary<AircraftTrackPoint, string>();

        foreach (var track in tracks)
        {
            foreach (var point in track.Points.OrderBy(point => point.TimestampUtc))
            {
                if (!events.TryGetValue(point.TimestampUtc, out var list))
                {
                    list = [];
                    events[point.TimestampUtc] = list;
                }

                list.Add(point);
                index[point] = track.Icao;
            }
        }

        var current = new Dictionary<string, AircraftTrack>(StringComparer.OrdinalIgnoreCase);
        var frames = new List<PlaybackFrame>();

        foreach (var item in events)
        {
            foreach (var point in item.Value)
            {
                var icao = index[point];
                if (!current.TryGetValue(icao, out var track))
                {
                    track = tracks.First(source => source.Icao.Equals(icao, StringComparison.OrdinalIgnoreCase));
                    current[icao] = new AircraftTrack
                    {
                        Icao = track.Icao,
                        Callsign = track.Callsign,
                        FirstSeenUtc = track.FirstSeenUtc,
                        Recognition = track.Recognition
                    };
                }

                track.LastSeenUtc = point.TimestampUtc;
                track.Latitude = point.Latitude;
                track.Longitude = point.Longitude;
                track.AltitudeFeet = point.AltitudeFeet;
                track.GroundSpeedKnots = point.GroundSpeedKnots;
                track.HeadingDegrees = point.HeadingDegrees;
                track.VerticalRateFeetPerMinute = point.VerticalRateFeetPerMinute;
                track.Points.Add(point);
            }

            frames.Add(new PlaybackFrame(item.Key, current.Values.Select(CloneTrack).ToList()));
        }

        return frames;
    }

    private static AircraftTrack CloneTrack(AircraftTrack source)
    {
        var track = new AircraftTrack
        {
            Icao = source.Icao,
            Callsign = source.Callsign,
            FirstSeenUtc = source.FirstSeenUtc,
            LastSeenUtc = source.LastSeenUtc,
            Latitude = source.Latitude,
            Longitude = source.Longitude,
            AltitudeFeet = source.AltitudeFeet,
            GroundSpeedKnots = source.GroundSpeedKnots,
            HeadingDegrees = source.HeadingDegrees,
            VerticalRateFeetPerMinute = source.VerticalRateFeetPerMinute,
            Squawk = source.Squawk,
            EmitterCategory = source.EmitterCategory,
            Recognition = source.Recognition
        };

        track.Points.AddRange(source.Points);
        return track;
    }
}
