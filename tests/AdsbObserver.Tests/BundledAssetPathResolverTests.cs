using AdsbObserver.Core.Models;
using AdsbObserver.Infrastructure.Services;
using Xunit;

namespace AdsbObserver.Tests;

public sealed class BundledAssetPathResolverTests
{
    [Fact]
    public void ResolveDecoderExecutable_UsesBundledRelativePath()
    {
        var settings = new ObservationSettings
        {
            PreferBundledDecoder = true,
            BundledDecoderRelativePath = @"backend\readsb\readsb.exe"
        };

        var resolvedPath = BundledAssetPathResolver.ResolveDecoderExecutable(settings);

        Assert.NotNull(resolvedPath);
        Assert.EndsWith(Path.Combine("backend", "readsb", "readsb.exe"), resolvedPath, StringComparison.OrdinalIgnoreCase);
    }
}
