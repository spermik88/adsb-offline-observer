namespace AdsbObserver.App.ViewModels;

public sealed class TrackFilterState
{
    public string SearchText { get; init; } = string.Empty;
    public bool WithPositionOnly { get; init; }
    public bool AirborneOnly { get; init; }
    public int? MinAltitudeFeet { get; init; }
    public int? MaxAltitudeFeet { get; init; }
    public double? MinSpeedKnots { get; init; }
    public double? MaxSpeedKnots { get; init; }
    public double? MaxDistanceKm { get; init; }
    public bool ShowSelectedOnly { get; init; }
}
