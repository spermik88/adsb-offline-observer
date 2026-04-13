using AdsbObserver.Core.Models;

namespace AdsbObserver.App.ViewModels;

internal sealed class MapViewState
{
    private double _homeCenterLatitude;
    private double _homeCenterLongitude;

    public int SelectedZoom { get; set; } = 8;
    public MapLayerType SelectedMapLayer { get; set; } = MapLayerType.Osm;
    public MapPackageInfo? CurrentMapPackage { get; set; }

    public void CaptureObservationCenter(double latitude, double longitude)
    {
        _homeCenterLatitude = latitude;
        _homeCenterLongitude = longitude;
    }

    public (double Latitude, double Longitude) GetHomeCenter() => (_homeCenterLatitude, _homeCenterLongitude);
}
