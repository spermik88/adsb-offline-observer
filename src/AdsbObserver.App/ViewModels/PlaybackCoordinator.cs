using AdsbObserver.Core.Models;
using AdsbObserver.Core.Services;

namespace AdsbObserver.App.ViewModels;

internal sealed class PlaybackCoordinator(PlaybackService playbackService)
{
    private IReadOnlyList<PlaybackFrame> _frames = [];
    private int _index;

    public bool HasFrames => _frames.Count > 0;

    public void Load(IReadOnlyList<AircraftTrack> tracks, int maxTrailPoints)
    {
        _frames = playbackService.BuildFrames(tracks, maxTrailPoints);
        _index = 0;
    }

    public void Reset()
    {
        _frames = [];
        _index = 0;
    }

    public bool TryGetNextFrame(out PlaybackFrame? frame)
    {
        if (_index >= _frames.Count)
        {
            frame = null;
            return false;
        }

        frame = _frames[_index++];
        return true;
    }
}
