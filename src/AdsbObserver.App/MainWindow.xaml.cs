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

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += async (_, _) => await RenderMapAsync();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.VisualStateChanged += async (_, _) => await RenderMapAsync();
        await RenderMapAsync();
    }

    private async void ImportRecognition_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV/TSV files|*.csv;*.tsv|All files|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            await ViewModel.ImportRecognitionAsync(dialog.FileName);
        }
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files|*.csv",
            FileName = ViewModel.SelectedTrack?.Icao is { Length: > 0 } icao ? $"{icao}.csv" : "tracks.csv"
        };

        if (dialog.ShowDialog(this) == true)
        {
            await ViewModel.ExportTracksAsync(dialog.FileName);
        }
    }

    private async Task RenderMapAsync()
    {
        if (!IsLoaded || MapCanvas.ActualWidth < 20 || MapCanvas.ActualHeight < 20 || DataContext is not MainViewModel vm)
        {
            return;
        }

        _renderCts?.Cancel();
        _renderCts = new CancellationTokenSource();
        var cancellationToken = _renderCts.Token;

        try
        {
            MapCanvas.Children.Clear();
            DrawFallbackBackground();

            if (vm.CurrentMapPackage is not null)
            {
                await DrawTilesAsync(cancellationToken);
            }

            DrawOverlay();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void DrawFallbackBackground()
    {
        MapCanvas.Children.Add(new Rectangle
        {
            Width = MapCanvas.ActualWidth,
            Height = MapCanvas.ActualHeight,
            Fill = new SolidColorBrush(Color.FromRgb(13, 20, 26))
        });

        var centerX = MapCanvas.ActualWidth / 2;
        var centerY = MapCanvas.ActualHeight / 2;
        foreach (var scale in new[] { 0.25, 0.5, 0.75, 1.0 })
        {
            var radius = Math.Min(MapCanvas.ActualWidth, MapCanvas.ActualHeight) / 2 * scale;
            MapCanvas.Children.Add(new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = new SolidColorBrush(Color.FromArgb(90, 120, 150, 170)),
                StrokeThickness = 1
            });
            Canvas.SetLeft(MapCanvas.Children[^1], centerX - radius);
            Canvas.SetTop(MapCanvas.Children[^1], centerY - radius);
        }
    }

    private async Task DrawTilesAsync(CancellationToken cancellationToken)
    {
        var width = MapCanvas.ActualWidth;
        var height = MapCanvas.ActualHeight;
        var zoom = ViewModel.SelectedZoom;
        var centerPixel = LatLonToWorldPixel(ViewModel.CenterLatitude, ViewModel.CenterLongitude, zoom);
        var topLeftX = centerPixel.X - width / 2;
        var topLeftY = centerPixel.Y - height / 2;

        var fromTileX = (int)Math.Floor(topLeftX / 256d);
        var toTileX = (int)Math.Floor((topLeftX + width) / 256d);
        var fromTileY = (int)Math.Floor(topLeftY / 256d);
        var toTileY = (int)Math.Floor((topLeftY + height) / 256d);

        for (var tileX = fromTileX; tileX <= toTileX; tileX++)
        {
            for (var tileY = fromTileY; tileY <= toTileY; tileY++)
            {
                if (tileX < 0 || tileY < 0)
                {
                    continue;
                }

                var bytes = await ViewModel.GetTileBytesAsync(zoom, tileX, tileY, cancellationToken);
                if (bytes is null)
                {
                    continue;
                }

                var key = $"{zoom}/{tileX}/{tileY}";
                if (!_tileCache.TryGetValue(key, out var bitmap))
                {
                    bitmap = CreateBitmap(bytes);
                    _tileCache[key] = bitmap;
                }

                var image = new Image
                {
                    Width = 256,
                    Height = 256,
                    Source = bitmap
                };
                Canvas.SetLeft(image, tileX * 256 - topLeftX);
                Canvas.SetTop(image, tileY * 256 - topLeftY);
                MapCanvas.Children.Add(image);
            }
        }
    }

    private void DrawOverlay()
    {
        var zoom = ViewModel.SelectedZoom;
        var centerPixel = LatLonToWorldPixel(ViewModel.CenterLatitude, ViewModel.CenterLongitude, zoom);
        var topLeftX = centerPixel.X - MapCanvas.ActualWidth / 2;
        var topLeftY = centerPixel.Y - MapCanvas.ActualHeight / 2;

        foreach (var track in ViewModel.Tracks.Select(item => item.Model))
        {
            if (track.Points.Count > 1)
            {
                var polyline = new Polyline
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(190, 255, 193, 7)),
                    StrokeThickness = 2
                };

                foreach (var point in track.Points)
                {
                    var pixel = LatLonToWorldPixel(point.Latitude, point.Longitude, zoom);
                    polyline.Points.Add(new Point(pixel.X - topLeftX, pixel.Y - topLeftY));
                }

                MapCanvas.Children.Add(polyline);
            }

            if (track.Latitude.HasValue && track.Longitude.HasValue)
            {
                var pixel = LatLonToWorldPixel(track.Latitude.Value, track.Longitude.Value, zoom);
                var x = pixel.X - topLeftX;
                var y = pixel.Y - topLeftY;

                var marker = new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Fill = new SolidColorBrush(Color.FromRgb(75, 192, 192)),
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5
                };
                Canvas.SetLeft(marker, x - 6);
                Canvas.SetTop(marker, y - 6);
                MapCanvas.Children.Add(marker);

                var label = new TextBlock
                {
                    Text = track.Callsign ?? track.Icao,
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
                    Padding = new Thickness(4, 1, 4, 1)
                };
                Canvas.SetLeft(label, x + 8);
                Canvas.SetTop(label, y - 10);
                MapCanvas.Children.Add(label);
            }
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
