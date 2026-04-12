using System.Text;
using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Models;

namespace AdsbObserver.Infrastructure.Services;

public sealed class CsvRecognitionImportService(IStorageService storageService) : IRecognitionImportService
{
    public async Task<int> ImportAsync(string path, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, true);

        var header = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(header))
        {
            return 0;
        }

        var delimiter = header.Contains('\t') ? '\t' : ',';
        var columns = header.Split(delimiter).Select(static value => value.Trim().ToLowerInvariant()).ToArray();

        var icaoIndex = Array.FindIndex(columns, static column => column is "icao" or "icao24");
        if (icaoIndex < 0)
        {
            throw new InvalidOperationException("The input file must contain an ICAO column.");
        }

        var registrationIndex = Array.FindIndex(columns, static column => column is "registration" or "reg");
        var typeIndex = Array.FindIndex(columns, static column => column is "aircraft type" or "aircraft_type" or "type");
        var operatorIndex = Array.FindIndex(columns, static column => column is "operator" or "owner");
        var countryIndex = Array.FindIndex(columns, static column => column is "country");

        var records = new List<AircraftRecognitionRecord>();

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = line.Split(delimiter);
            var icao = Read(cells, icaoIndex)?.ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(icao))
            {
                continue;
            }

            records.Add(new AircraftRecognitionRecord(
                icao,
                Read(cells, registrationIndex),
                Read(cells, typeIndex),
                Read(cells, operatorIndex),
                Read(cells, countryIndex)));
        }

        await storageService.UpsertRecognitionAsync(records, cancellationToken);
        return records.Count;
    }

    private static string? Read(string[] cells, int index)
    {
        if (index < 0 || index >= cells.Length)
        {
            return null;
        }

        var value = cells[index].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
