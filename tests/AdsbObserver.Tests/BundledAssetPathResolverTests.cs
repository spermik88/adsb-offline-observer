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
            BundledDecoderRelativePath = @"backend\dump1090\dump1090.exe"
        };

        var resolvedPath = BundledAssetPathResolver.ResolveDecoderExecutable(settings);

        Assert.NotNull(resolvedPath);
        Assert.EndsWith(Path.Combine("backend", "dump1090", "dump1090.exe"), resolvedPath, StringComparison.OrdinalIgnoreCase);
    }
}
