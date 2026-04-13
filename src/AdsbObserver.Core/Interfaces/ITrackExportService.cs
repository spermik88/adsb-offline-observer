namespace AdsbObserver.Core.Interfaces;

public interface ITrackExportService
{
    Task ExportAsync(string path, string? icao, DateTime? fromUtc, DateTime? toUtc, bool withCoordinatesOnly, CancellationToken cancellationToken);
}
