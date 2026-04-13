namespace AdsbObserver.Core.Models;

public sealed class PortableWorkspacePaths
{
    public string AppRoot { get; init; } = string.Empty;
    public string PortableRoot { get; init; } = string.Empty;
    public string DataRoot { get; init; } = string.Empty;
    public string MapsRoot { get; init; } = string.Empty;
    public string RecordingsRoot { get; init; } = string.Empty;
    public string LogsRoot { get; init; } = string.Empty;
}
