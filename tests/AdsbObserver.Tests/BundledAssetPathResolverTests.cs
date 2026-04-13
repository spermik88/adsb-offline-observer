using System.IO;
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

    [Fact]
    public void ResolveDecoderConfig_UsesBundledRelativePath()
    {
        var settings = new ObservationSettings
        {
            BundledDecoderConfigRelativePath = @"backend\dump1090\dump1090.cfg"
        };

        var resolvedPath = BundledAssetPathResolver.ResolveDecoderConfig(settings);

        Assert.EndsWith(Path.Combine("backend", "dump1090", "dump1090.cfg"), resolvedPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveDecoderLog_UsesPortableLogsRelativePath()
    {
        var settings = new ObservationSettings
        {
            BundledDecoderLogRelativePath = @"logs\dump1090.log"
        };

        var resolvedPath = BundledAssetPathResolver.ResolveDecoderLog(settings);

        Assert.EndsWith(Path.Combine("logs", "dump1090.log"), resolvedPath, StringComparison.OrdinalIgnoreCase);
    }
}
