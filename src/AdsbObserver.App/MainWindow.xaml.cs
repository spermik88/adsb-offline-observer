using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AdsbObserver.App.Rendering;
using AdsbObserver.App.ViewModels;
using Microsoft.Win32;

namespace AdsbObserver.App;

public partial class MainWindow : Window
{
    private const int MaxCachedTiles = 512;
    private readonly Dictionary<string, BitmapImage> _tileCache = new(StringComparer.Ordinal);
    private readonly Queue<string> _tileCacheOrder = new();
    private readonly MapOverlayRenderer _overlayRenderer = new();
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
        if (dialog.ShowDialog(this) == true)
        {
            await ViewModel.ImportRecognitionAsync(dialog.FileName);
        }
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "CSV files|*.csv", FileName = ViewModel.SelectedTrack?.Icao is { Length: > 0 } icao ? $"{icao}.csv" : "tracks.csv" };
        if (dialog.ShowDialog(this) == true)
        {
            await ViewModel.ExportTracksAsync(dialog.FileName);
        }
    }

    private async Task RenderBaseAsync()
    {
        if (!IsLoaded || BackgroundCanvas.ActualWidth < 20 || BackgroundCanvas.ActualHeight < 20 || DataContext is not MainViewModel vm)
        {
            return;
        }

        var state = (
            Width: BackgroundCanvas.ActualWidth,
            Height: BackgroundCanvas.ActualHeight,
            Zoom: vm.SelectedZoom,
            Lat: vm.CenterLatitude,
            Lon: vm.CenterLongitude,
            Layer: vm.CurrentMapPackage?.Id ?? vm.SelectedMapLayer.ToString());
        if (_lastBaseState == state)
        {
            return;
        }

        if (_lastBaseState?.Layer != state.Layer)
        {
            ClearTileCache();
        }

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
            try
            {
                await DrawTilesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private void DrawFallbackBackground()
    {
        BackgroundCanvas.Children.Add(new Rectangle
        {
            Width = BackgroundCanvas.ActualWidth,
            Height = BackgroundCanvas.ActualHeight,
            Fill = new SolidColorBrush(Color.FromRgb(13, 20, 26))
        });
    }

    private void DrawRangeRings()
    {
        var centerX = BackgroundCanvas.ActualWidth / 2;
        var centerY = BackgroundCanvas.ActualHeight / 2;
        var maxRadius = Math.Min(BackgroundCanvas.ActualWidth, BackgroundCanvas.ActualHeight) / 2;
        for (var i = 1; i <= 4; i++)
        {
            var radius = maxRadius * i / 4;
            var ring = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = new SolidColorBrush(Color.FromArgb(90, 120, 150, 170)),
                StrokeThickness = 1
            };
            Canvas.SetLeft(ring, centerX - radius);
            Canvas.SetTop(ring, centerY - radius);
            BackgroundCanvas.Children.Add(ring);

            var label = new TextBlock
            {
                Text = $"{ViewModel.RadiusKilometers * i / 4:F0} km",
                Foreground = new SolidColorBrush(Color.FromRgb(160, 188, 205)),
                FontSize = 11
            };
            Canvas.SetLeft(label, centerX + 6);
            Canvas.SetTop(label, centerY - radius - 10);
            BackgroundCanvas.Children.Add(label);
        }
    }

    private async Task DrawTilesAsync(CancellationToken cancellationToken)
    {
        var viewport = new MapViewport(TileCanvas.ActualWidth, TileCanvas.ActualHeight, ViewModel.SelectedZoom, ViewModel.CenterLatitude, ViewModel.CenterLongitude);
        var fromTileX = (int)Math.Floor(viewport.TopLeftX / 256d);
        var toTileX = (int)Math.Floor((viewport.TopLeftX + viewport.Width) / 256d);
        var fromTileY = (int)Math.Floor(viewport.TopLeftY / 256d);
        var toTileY = (int)Math.Floor((viewport.TopLeftY + viewport.Height) / 256d);

        for (var tileX = fromTileX; tileX <= toTileX; tileX++)
        {
            for (var tileY = fromTileY; tileY <= toTileY; tileY++)
            {
                if (tileX < 0 || tileY < 0)
                {
                    continue;
                }

                var bytes = await ViewModel.GetTileBytesAsync(viewport.Zoom, tileX, tileY, cancellationToken);
                if (bytes is null)
                {
                    continue;
                }

                var key = $"{ViewModel.CurrentMapPackage?.Id}:{viewport.Zoom}/{tileX}/{tileY}";
                if (!_tileCache.TryGetValue(key, out var bitmap))
                {
                    bitmap = CreateBitmap(bytes);
                    _tileCache[key] = bitmap;
                    _tileCacheOrder.Enqueue(key);
                    TrimTileCache();
                }

                var image = new Image { Width = 256, Height = 256, Source = bitmap };
                Canvas.SetLeft(image, tileX * 256 - viewport.TopLeftX);
                Canvas.SetTop(image, tileY * 256 - viewport.TopLeftY);
                TileCanvas.Children.Add(image);
            }
        }
    }

    private void RenderOverlay()
    {
        if (DataContext is not MainViewModel)
        {
            return;
        }

        var viewport = new MapViewport(OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight, ViewModel.SelectedZoom, ViewModel.CenterLatitude, ViewModel.CenterLongitude);
        _overlayRenderer.Render(OverlayCanvas, LabelCanvas, ViewModel.Tracks, viewport, ViewModel.ShouldDisplayLabel);
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

    private void TrimTileCache()
    {
        while (_tileCache.Count > MaxCachedTiles && _tileCacheOrder.Count > 0)
        {
            var key = _tileCacheOrder.Dequeue();
            _tileCache.Remove(key);
        }
    }

    private void ClearTileCache()
    {
        _tileCache.Clear();
        _tileCacheOrder.Clear();
    }
}
