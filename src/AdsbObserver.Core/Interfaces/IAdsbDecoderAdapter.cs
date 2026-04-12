using AdsbObserver.Core.Models;

namespace AdsbObserver.Core.Interfaces;

public interface IAdsbDecoderAdapter
{
    IAsyncEnumerable<AircraftMessage> ReadMessagesAsync(ObservationSettings settings, CancellationToken cancellationToken);
}
