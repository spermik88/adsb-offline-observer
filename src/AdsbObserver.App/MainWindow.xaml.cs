using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AdsbObserver.App.ViewModels;
using Microsoft.Win32;

namespace AdsbObserver.App;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, BitmapImage> _tileCache = new(StringComparer.Ordinal);
    private CancellationTokenSource? _renderCts;
    private (double Width, double Height, int Zoom, double Lat, double Lon, string? Layer)? _lastBaseState;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += async (_, _) => await RenderBaseAsync();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.VisualStateChanged += async (_, _) =>
        {
            await RenderBaseAsync();
            RenderOverlay();
        };
        await RenderBaseAsync();
        RenderOverlay();
    }

    private async void ImportRecognition_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "CSV/TSV files|*.csv;*.tsv|All files|*.*" };
        if (dialog.ShowDialog(this) == true) await ViewModel.ImportRecognitionAsync(dialog.FileName);
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "CSV files|*.csv", FileName = ViewModel.SelectedTrack?.Icao is { Length: > 0 } icao ? $"{icao}.csv" : "tracks.csv" };
        if (dialog.ShowDialog(this) == true) await ViewModel.ExportTracksAsync(dialog.FileName);
    }

    private async Task RenderBaseAsync()
    {
        if (!IsLoaded || BackgroundCanvas.ActualWidth < 20 || BackgroundCanvas.ActualHeight < 20 || DataContext is not MainViewModel vm) return;
        var state = (BackgroundCanvas.ActualWidth, BackgroundCanvas.ActualHeight, vm.SelectedZoom, vm.CenterLatitude, vm.CenterLongitude, vm.CurrentMapPackage?.Id ?? vm.SelectedMapLayer.ToString());
        if (_lastBaseState == state) return;
        _lastBaseState = state;
        _renderCts?.Cancel();
        _renderCts = new CancellationTokenSource();
        var cancellationToken = _renderCts.Token;

        BackgroundCanvas.Children.Clear();
        TileCanvas.Children.Clear();
        DrawFallbackBackground();
        DrawRangeRings();
        if (vm.CurrentMapPackage is not null)
        {
            try { await DrawTilesAsync(cancellationToken); } catch (OperationCanceledException) { }
        }
    }

    private void DrawFallbackBackground()
    {
        BackgroundCanvas.Children.Add(new Rectangle { Width = BackgroundCanvas.ActualWidth, Height = BackgroundCanvas.ActualHeight, Fill = new SolidColorBrush(Color.FromRgb(13, 20, 26)) });
    }

    private void DrawRangeRings()
    {
        var centerX = BackgroundCanvas.ActualWidth / 2;
        var centerY = BackgroundCanvas.ActualHeight / 2;
        var maxRadius = Math.Min(BackgroundCanvas.ActualWidth, BackgroundCanvas.ActualHeight) / 2;
        var ringCount = 4;
        for (var i = 1; i <= ringCount; i++)
        {
            var radius = maxRadius * i / ringCount;
            var ring = new Ellipse { Width = radius * 2, Height = radius * 2, Stroke = new SolidColorBrush(Color.FromArgb(90, 120, 150, 170)), StrokeThickness = 1 };
            Canvas.SetLeft(ring, centerX - radius);
            Canvas.SetTop(ring, centerY - radius);
            BackgroundCanvas.Children.Add(ring);
            var label = new TextBlock { Text = $"{ViewModel.RadiusKilometers * i / ringCount:F0} km", Foreground = new SolidColorBrush(Color.FromRgb(160, 188, 205)), FontSize = 11 };
            Canvas.SetLeft(label, centerX + 6);
            Canvas.SetTop(label, centerY - radius - 10);
            BackgroundCanvas.Children.Add(label);
        }
    }

    private async Task DrawTilesAsync(CancellationToken cancellationToken)
    {
        var width = TileCanvas.ActualWidth;
        var height = TileCanvas.ActualHeight;
        var zoom = ViewModel.SelectedZoom;
        var centerPixel = LatLonToWorldPixel(ViewModel.CenterLatitude, ViewModel.CenterLongitude, zoom);
        var topLeftX = centerPixel.X - width / 2;
        var topLeftY = centerPixel.Y - height / 2;
        var fromTileX = (int)Math.Floor(topLeftX / 256d);
        var toTileX = (int)Math.Floor((topLeftX + width) / 256d);
        var fromTileY = (int)Math.Floor(topLeftY / 256d);
        var toTileY = (int)Math.Floor((topLeftY + height) / 256d);

        for (var tileX = fromTileX; tileX <= toTileX; tileX++)
        for (var tileY = fromTileY; tileY <= toTileY; tileY++)
        {
            if (tileX < 0 || tileY < 0) continue;
            var bytes = await ViewModel.GetTileBytesAsync(zoom, tileX, tileY, cancellationToken);
            if (bytes is null) continue;
            var key = $"{ViewModel.CurrentMapPackage?.Id}:{zoom}/{tileX}/{tileY}";
            if (!_tileCache.TryGetValue(key, out var bitmap)) { bitmap = CreateBitmap(bytes); _tileCache[key] = bitmap; }
            var image = new Image { Width = 256, Height = 256, Source = bitmap };
            Canvas.SetLeft(image, tileX * 256 - topLeftX);
            Canvas.SetTop(image, tileY * 256 - topLeftY);
            TileCanvas.Children.Add(image);
        }
    }

    private void RenderOverlay()
    {
        if (DataContext is not MainViewModel) return;
        OverlayCanvas.Children.Clear();
        LabelCanvas.Children.Clear();
        var zoom = ViewModel.SelectedZoom;
        var centerPixel = LatLonToWorldPixel(ViewModel.CenterLatitude, ViewModel.CenterLongitude, zoom);
        var topLeftX = centerPixel.X - OverlayCanvas.ActualWidth / 2;
        var topLeftY = centerPixel.Y - OverlayCanvas.ActualHeight / 2;

        foreach (var track in ViewModel.Tracks)
        {
            var model = track.Model;
            if (model.Points.Count > 1)
            {
                var polyline = new Polyline { Stroke = new SolidColorBrush(track.IsSelected ? Color.FromRgb(255, 212, 59) : track.IsStale ? Color.FromArgb(110, 124, 153, 171) : Color.FromArgb(180, 88, 211, 255)), StrokeThickness = track.IsSelected ? 2.8 : 2 };
                foreach (var point in model.Points)
                {
                    var pixel = LatLonToWorldPixel(point.Latitude, point.Longitude, zoom);
                    polyline.Points.Add(new Point(pixel.X - topLeftX, pixel.Y - topLeftY));
                }
                OverlayCanvas.Children.Add(polyline);
            }

            if (!model.Latitude.HasValue || !model.Longitude.HasValue) continue;
            var pixelNow = LatLonToWorldPixel(model.Latitude.Value, model.Longitude.Value, zoom);
            var x = pixelNow.X - topLeftX;
            var y = pixelNow.Y - topLeftY;
            var fill = track.IsEmergency ? Color.FromRgb(230, 92, 92) : track.IsSelected ? Color.FromRgb(255, 212, 59) : track.IsStale ? Color.FromArgb(120, 130, 145, 158) : Color.FromRgb(71, 201, 176);
            var marker = new Ellipse { Width = track.IsSelected ? 16 : 12, Height = track.IsSelected ? 16 : 12, Fill = new SolidColorBrush(fill), Stroke = Brushes.White, StrokeThickness = track.IsSelected ? 2 : 1.5, Opacity = track.IsStale ? 0.55 : 1 };
            Canvas.SetLeft(marker, x - marker.Width / 2);
            Canvas.SetTop(marker, y - marker.Height / 2);
            OverlayCanvas.Children.Add(marker);

            if (track.IsSelected && model.HeadingDegrees.HasValue)
            {
                var radians = (model.HeadingDegrees.Value - 90) * Math.PI / 180d;
                var vector = new Line { X1 = x, Y1 = y, X2 = x + Math.Cos(radians) * 32, Y2 = y + Math.Sin(radians) * 32, Stroke = new SolidColorBrush(Color.FromRgb(255, 212, 59)), StrokeThickness = 2 };
                OverlayCanvas.Children.Add(vector);
            }

            if (!ViewModel.ShouldDisplayLabel(track)) continue;
            var label = new TextBlock { Text = $"{track.Callsign} • {track.AltitudeText}", Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromArgb(track.IsSelected ? (byte)190 : (byte)150, 0, 0, 0)), Padding = new Thickness(4, 1, 4, 1), FontWeight = track.IsSelected ? FontWeights.SemiBold : FontWeights.Normal };
            Canvas.SetLeft(label, x + 10);
            Canvas.SetTop(label, y - 12);
            LabelCanvas.Children.Add(label);
        }
    }

    private static Point LatLonToWorldPixel(double lat, double lon, int zoom)
    {
        var tileSize = 256d;
        var scale = (1 << zoom) * tileSize;
        var x = (lon + 180d) / 360d * scale;
        var sinLat = Math.Sin(lat * Math.PI / 180d);
        var y = (0.5 - Math.Log((1 + sinLat) / (1 - sinLat)) / (4 * Math.PI)) * scale;
        return new Point(x, y);
    }

    private static BitmapImage CreateBitmap(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
