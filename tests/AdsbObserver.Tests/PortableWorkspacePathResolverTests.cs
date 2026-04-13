using System.IO;
using AdsbObserver.Infrastructure.Services;
using Xunit;

namespace AdsbObserver.Tests;

public sealed class PortableWorkspacePathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "adsbobserver-layout-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_UsesSharedRootFromPortableLayoutConfig()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "portable.layout.json"), "{ \"SharedRootRelativePath\": \"..\\\\SharedData\" }");

        var paths = PortableWorkspacePathResolver.Resolve(_root);

        Assert.Equal(Path.GetFullPath(Path.Combine(_root, "..", "SharedData")), paths.PortableRoot);
        Assert.Equal(Path.Combine(paths.PortableRoot, "logs"), paths.LogsRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
