namespace AdsbObserver.Core.Models;

public sealed class MapPackageInfo
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public MapLayerType LayerType { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int MinZoom { get; set; }
    public int MaxZoom { get; set; }
    public double North { get; set; }
    public double South { get; set; }
    public double East { get; set; }
    public double West { get; set; }
    public DateTime DownloadedUtc { get; set; } = DateTime.UtcNow;
}
