using AdsbObserver.Core.Models;

namespace AdsbObserver.Core.Interfaces;

public interface IDeviceDetector
{
    Task<IReadOnlyList<SdrDeviceInfo>> DetectAsync(CancellationToken cancellationToken);
}
