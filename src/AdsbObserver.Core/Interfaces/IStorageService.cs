using AdsbObserver.Core.Models;

namespace AdsbObserver.Core.Interfaces;

public interface IStorageService
{
    Task<StorageCompatibilityStatus> InitializeAsync(CancellationToken cancellationToken);
    Task<ObservationSettings> GetSettingsAsync(CancellationToken cancellationToken);
    Task SaveSettingsAsync(ObservationSettings settings, CancellationToken cancellationToken);
    Task UpsertTrackAsync(AircraftTrack track, CancellationToken cancellationToken);
    Task AppendTrackPointAsync(string icao, AircraftTrackPoint point, CancellationToken cancellationToken);
    Task<IReadOnlyList<AircraftTrack>> GetStoredTracksAsync(DateTime? fromUtc, DateTime? toUtc, string? icao, CancellationToken cancellationToken);
    Task<IReadOnlyList<AircraftTrackPoint>> GetTrackPointsAsync(string icao, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);
    Task UpsertRecognitionAsync(IEnumerable<AircraftRecognitionRecord> records, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, AircraftRecognitionRecord>> GetRecognitionLookupAsync(CancellationToken cancellationToken);
    Task SaveMapPackageAsync(MapPackageInfo package, CancellationToken cancellationToken);
    Task<IReadOnlyList<MapPackageInfo>> GetMapPackagesAsync(CancellationToken cancellationToken);
}
