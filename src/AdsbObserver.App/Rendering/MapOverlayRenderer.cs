using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AdsbObserver.App.ViewModels;

namespace AdsbObserver.App.Rendering;

internal sealed class MapOverlayRenderer
{
    public void Render(Canvas overlayCanvas, Canvas labelCanvas, IReadOnlyCollection<TrackViewModel> tracks, MapViewport viewport, Func<TrackViewModel, bool> shouldDisplayLabel)
    {
        overlayCanvas.Children.Clear();
        labelCanvas.Children.Clear();

        foreach (var track in tracks)
        {
            RenderTrackPath(overlayCanvas, track, viewport);
            RenderMarkerAndLabel(overlayCanvas, labelCanvas, track, viewport, shouldDisplayLabel);
        }
    }

    private static void RenderTrackPath(Canvas overlayCanvas, TrackViewModel track, MapViewport viewport)
    {
        var model = track.Model;
        if (model.Points.Count <= 1)
        {
            return;
        }

        var polyline = new Polyline
        {
            Stroke = new SolidColorBrush(track.IsSelected ? Color.FromRgb(255, 212, 59) : track.IsStale ? Color.FromArgb(110, 124, 153, 171) : Color.FromArgb(180, 88, 211, 255)),
            StrokeThickness = track.IsSelected ? 2.8 : 2
        };

        foreach (var point in model.Points)
        {
            polyline.Points.Add(viewport.Project(point.Latitude, point.Longitude));
        }

        overlayCanvas.Children.Add(polyline);
    }

    private static void RenderMarkerAndLabel(Canvas overlayCanvas, Canvas labelCanvas, TrackViewModel track, MapViewport viewport, Func<TrackViewModel, bool> shouldDisplayLabel)
    {
        var model = track.Model;
        if (!model.Latitude.HasValue || !model.Longitude.HasValue)
        {
            return;
        }

        var point = viewport.Project(model.Latitude.Value, model.Longitude.Value);
        var fill = track.IsEmergency
            ? Color.FromRgb(230, 92, 92)
            : track.IsSelected
                ? Color.FromRgb(255, 212, 59)
                : track.IsStale
                    ? Color.FromArgb(135, 117, 131, 145)
                    : Color.FromRgb(71, 201, 176);

        var marker = new Ellipse
        {
            Width = track.IsSelected ? 16 : 12,
            Height = track.IsSelected ? 16 : 12,
            Fill = new SolidColorBrush(fill),
            Stroke = Brushes.White,
            StrokeThickness = track.IsSelected ? 2 : 1.5,
            Opacity = track.IsStale ? 0.6 : 1
        };
        Canvas.SetLeft(marker, point.X - marker.Width / 2);
        Canvas.SetTop(marker, point.Y - marker.Height / 2);
        overlayCanvas.Children.Add(marker);

        if (track.IsSelected && model.HeadingDegrees.HasValue)
        {
            var radians = (model.HeadingDegrees.Value - 90) * Math.PI / 180d;
            overlayCanvas.Children.Add(new Line
            {
                X1 = point.X,
                Y1 = point.Y,
                X2 = point.X + Math.Cos(radians) * 32,
                Y2 = point.Y + Math.Sin(radians) * 32,
                Stroke = new SolidColorBrush(Color.FromRgb(255, 212, 59)),
                StrokeThickness = 2
            });
        }

        if (!shouldDisplayLabel(track))
        {
            return;
        }

        var label = new TextBlock
        {
            Text = $"{track.Callsign} • {track.AltitudeText}",
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(track.IsSelected ? (byte)190 : (byte)150, 0, 0, 0)),
            Padding = new Thickness(4, 1, 4, 1),
            FontWeight = track.IsSelected ? FontWeights.SemiBold : FontWeights.Normal
        };
        Canvas.SetLeft(label, point.X + 10);
        Canvas.SetTop(label, point.Y - 12);
        labelCanvas.Children.Add(label);
    }
}
