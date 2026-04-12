using System.Runtime.CompilerServices;
using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Models;

namespace AdsbObserver.Infrastructure.Services;

public sealed class SimulatedDecoderAdapter : IAdsbDecoderAdapter
{
    public async IAsyncEnumerable<AircraftMessage> ReadMessagesAsync(
        ObservationSettings settings,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var random = new Random();
        var states = Enumerable.Range(0, 12)
            .Select(index => new SimAircraft(
                $"7C{index + 100:X3}",
                $"SIM{index + 1:000}",
                settings.CenterLatitude + random.NextDouble() * 2 - 1,
                settings.CenterLongitude + random.NextDouble() * 2 - 1,
                random.Next(12_000, 40_000),
                random.Next(250, 470),
                random.NextDouble() * 360))
            .ToList();

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            foreach (var aircraft in states)
            {
                aircraft.Step();
                yield return new AircraftMessage(
                    aircraft.Icao,
                    now,
                    aircraft.Callsign,
                    aircraft.Latitude,
                    aircraft.Longitude,
                    aircraft.AltitudeFeet,
                    aircraft.GroundSpeedKnots,
                    aircraft.HeadingDegrees,
                    aircraft.VerticalRateFeetPerMinute,
                    $"{aircraft.GroundSpeedKnots % 7777:0000}",
                    "SIM");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    private sealed class SimAircraft(
        string icao,
        string callsign,
        double latitude,
        double longitude,
        int altitudeFeet,
        int groundSpeedKnots,
        double headingDegrees)
    {
        private readonly Random _random = new();

        public string Icao { get; } = icao;
        public string Callsign { get; } = callsign;
        public double Latitude { get; private set; } = latitude;
        public double Longitude { get; private set; } = longitude;
        public int AltitudeFeet { get; private set; } = altitudeFeet;
        public int GroundSpeedKnots { get; private set; } = groundSpeedKnots;
        public double HeadingDegrees { get; private set; } = headingDegrees;
        public int VerticalRateFeetPerMinute { get; private set; }

        public void Step()
        {
            HeadingDegrees = (HeadingDegrees + _random.NextDouble() * 6 - 3 + 360) % 360;
            VerticalRateFeetPerMinute = _random.Next(-800, 900);
            AltitudeFeet = Math.Clamp(AltitudeFeet + VerticalRateFeetPerMinute / 6, 3_000, 43_000);

            var distanceDegrees = GroundSpeedKnots / 3600d / 60d;
            var headingRadians = HeadingDegrees * Math.PI / 180d;
            Latitude += Math.Cos(headingRadians) * distanceDegrees;
            Longitude += Math.Sin(headingRadians) * distanceDegrees / Math.Max(0.2, Math.Cos(Latitude * Math.PI / 180d));
        }
    }
}
