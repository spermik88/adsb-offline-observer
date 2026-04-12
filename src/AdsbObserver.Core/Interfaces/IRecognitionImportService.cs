namespace AdsbObserver.Core.Interfaces;

public interface IRecognitionImportService
{
    Task<int> ImportAsync(string path, CancellationToken cancellationToken);
}
