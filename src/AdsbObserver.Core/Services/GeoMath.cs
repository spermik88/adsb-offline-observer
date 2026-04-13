namespace AdsbObserver.Core.Services;

public static class GeoMath
{
    private const double EarthRadiusKilometers = 6371.0;

    public static double DistanceKilometers(double fromLat, double fromLon, double toLat, double toLon)
    {
        var lat1 = DegreesToRadians(fromLat);
        var lon1 = DegreesToRadians(fromLon);
        var lat2 = DegreesToRadians(toLat);
        var lon2 = DegreesToRadians(toLon);

        var deltaLat = lat2 - lat1;
        var deltaLon = lon2 - lon1;
        var a = Math.Pow(Math.Sin(deltaLat / 2), 2) +
                Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(deltaLon / 2), 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKilometers * c;
    }

    public static double BearingDegrees(double fromLat, double fromLon, double toLat, double toLon)
    {
        var lat1 = DegreesToRadians(fromLat);
        var lat2 = DegreesToRadians(toLat);
        var deltaLon = DegreesToRadians(toLon - fromLon);

        var y = Math.Sin(deltaLon) * Math.Cos(lat2);
        var x = Math.Cos(lat1) * Math.Sin(lat2) -
                Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(deltaLon);
        var bearing = RadiansToDegrees(Math.Atan2(y, x));
        return (bearing + 360) % 360;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
    private static double RadiansToDegrees(double radians) => radians * 180d / Math.PI;
}
