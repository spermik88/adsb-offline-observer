using AdsbObserver.Core.Models;

namespace AdsbObserver.Core.Interfaces;

public interface IMapTileService
{
    Task DownloadPackageAsync(MapPackageInfo package, string urlTemplate, IProgress<int>? progress, CancellationToken cancellationToken);
    Task<byte[]?> GetTileBytesAsync(MapPackageInfo package, int zoom, int x, int y, CancellationToken cancellationToken);
    Task<MapPackageInfo?> InspectPackageAsync(string filePath, CancellationToken cancellationToken);
}
