using System.Text.Json;
using AdsbObserver.Core.Models;

namespace AdsbObserver.Infrastructure.Services;

public static class PortableWorkspacePathResolver
{
    private const string LayoutConfigFileName = "portable.layout.json";

    public static PortableWorkspacePaths Resolve(string appBaseDirectory)
    {
        var appRoot = Path.GetFullPath(appBaseDirectory);
        var portableRoot = ResolvePortableRoot(appRoot);

        return new PortableWorkspacePaths
        {
            AppRoot = appRoot,
            PortableRoot = portableRoot,
            DataRoot = EnsureDirectory(Path.Combine(portableRoot, "data")),
            MapsRoot = EnsureDirectory(Path.Combine(portableRoot, "maps")),
            RecordingsRoot = EnsureDirectory(Path.Combine(portableRoot, "recordings")),
            LogsRoot = EnsureDirectory(Path.Combine(portableRoot, "logs"))
        };
    }

    private static string ResolvePortableRoot(string appRoot)
    {
        var configPath = Path.Combine(appRoot, LayoutConfigFileName);
        if (File.Exists(configPath))
        {
            var config = JsonSerializer.Deserialize<PortableLayoutConfig>(File.ReadAllText(configPath));
            if (!string.IsNullOrWhiteSpace(config?.SharedRootRelativePath))
            {
                return EnsureDirectory(Path.GetFullPath(Path.Combine(appRoot, config.SharedRootRelativePath)));
            }
        }

        return EnsureDirectory(appRoot);
    }

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class PortableLayoutConfig
    {
        public string? SharedRootRelativePath { get; init; }
    }
}
