using System.Windows;

namespace AdsbObserver.App.Rendering;

internal readonly record struct MapViewport(double Width, double Height, int Zoom, double CenterLatitude, double CenterLongitude)
{
    public Point CenterPixel => LatLonToWorldPixel(CenterLatitude, CenterLongitude, Zoom);
    public double TopLeftX => CenterPixel.X - Width / 2;
    public double TopLeftY => CenterPixel.Y - Height / 2;

    public Point Project(double latitude, double longitude)
    {
        var pixel = LatLonToWorldPixel(latitude, longitude, Zoom);
        return new Point(pixel.X - TopLeftX, pixel.Y - TopLeftY);
    }

    public static Point LatLonToWorldPixel(double lat, double lon, int zoom)
    {
        var tileSize = 256d;
        var scale = (1 << zoom) * tileSize;
        var x = (lon + 180d) / 360d * scale;
        var sinLat = Math.Sin(lat * Math.PI / 180d);
        var y = (0.5 - Math.Log((1 + sinLat) / (1 - sinLat)) / (4 * Math.PI)) * scale;
        return new Point(x, y);
    }
}
