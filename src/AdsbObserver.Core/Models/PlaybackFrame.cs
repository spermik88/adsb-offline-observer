namespace AdsbObserver.Core.Models;

public sealed record PlaybackFrame(DateTime TimestampUtc, IReadOnlyList<AircraftTrack> Tracks);
