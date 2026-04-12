using AdsbObserver.Core.Models;

namespace AdsbObserver.Core.Interfaces;

public interface IDecoderProcessService
{
    event EventHandler<DecoderProcessStatus>? StatusChanged;
    bool IsRunning { get; }
    DecoderProcessStatus CurrentStatus { get; }
    Task<DecoderProcessStatus> StartAsync(ObservationSettings settings, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
