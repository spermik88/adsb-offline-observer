using AdsbObserver.Core.Models;

namespace AdsbObserver.Core.Interfaces;

public interface IDecoderProcessService
{
    event EventHandler<string>? StatusChanged;
    bool IsRunning { get; }
    Task StartAsync(ObservationSettings settings, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
