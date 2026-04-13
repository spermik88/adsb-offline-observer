using System.Collections.ObjectModel;
using AdsbObserver.Core.Models;

namespace AdsbObserver.App.ViewModels;

internal sealed class TrackListState
{
    public ObservableCollection<TrackViewModel> Tracks { get; } = [];
    public TrackViewModel? SelectedTrack { get; private set; }

    public void ReplaceTracks(IEnumerable<TrackViewModel> tracks, string? selectedIcao)
    {
        Tracks.Clear();
        foreach (var track in tracks)
        {
            Tracks.Add(track);
        }

        SelectedTrack = Tracks.FirstOrDefault(track => string.Equals(track.Icao, selectedIcao, StringComparison.OrdinalIgnoreCase))
                        ?? Tracks.FirstOrDefault();
    }

    public void SetSelectedTrack(TrackViewModel? track) => SelectedTrack = track;
}
