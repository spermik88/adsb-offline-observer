using AdsbObserver.Core.Models;

namespace AdsbObserver.Infrastructure.Services;

public static class BundledAssetPathResolver
{
    public static string? ResolveDecoderExecutable(ObservationSettings settings)
    {
        if (settings.PreferBundledDecoder)
        {
            return ResolveAppRelativePath(settings.BundledDecoderRelativePath);
        }

        return string.IsNullOrWhiteSpace(settings.DecoderExecutablePath)
            ? null
            : Path.GetFullPath(settings.DecoderExecutablePath);
    }

    public static string ResolveDecoderConfig(ObservationSettings settings) =>
        ResolveAppRelativePath(settings.BundledDecoderConfigRelativePath);

    public static string ResolveDecoderLog(ObservationSettings settings) =>
        ResolveAppRelativePath(settings.BundledDecoderLogRelativePath);

    public static string ResolveDriverSetupScript(ObservationSettings settings) =>
        ResolveAppRelativePath(settings.BundledDriverSetupRelativePath);

    public static string ResolveDriverInf(ObservationSettings settings) =>
        ResolveAppRelativePath(settings.BundledDriverInfRelativePath);

    private static string ResolveAppRelativePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return relativePath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));
    }
}
