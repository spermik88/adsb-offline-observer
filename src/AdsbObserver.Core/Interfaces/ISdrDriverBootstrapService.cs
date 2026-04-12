using AdsbObserver.Core.Models;

namespace AdsbObserver.Core.Interfaces;

public interface ISdrDriverBootstrapService
{
    Task<LiveEnvironmentStatus> InspectAsync(ObservationSettings settings, CancellationToken cancellationToken);
    Task<LiveEnvironmentStatus> EnsureReadyAsync(ObservationSettings settings, CancellationToken cancellationToken);
}
