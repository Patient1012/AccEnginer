using System.Windows;
using System.Windows.Media;

namespace AccEngineer.UI.Controls;

/// <summary>One point on the live map (world X/Z) and whether it is the player.</summary>
public readonly record struct MapPoint(double X, double Z, bool IsPlayer);

/// <summary>
/// Lightweight top-down map of the cars currently on track. Draws only the points
/// it is given and auto-fits them to the available space, so an empty or partial
/// set never renders invented geometry.
/// </summary>
public sealed class TrackMap : FrameworkElement
{
    public static readonly DependencyProperty MarkersProperty = DependencyProperty.Register(
        nameof(Markers), typeof(IEnumerable<MapPoint>), typeof(TrackMap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PathProperty = DependencyProperty.Register(
        nameof(Path), typeof(IEnumerable<MapPoint>), typeof(TrackMap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MarkerBrushProperty = DependencyProperty.Register(
        nameof(MarkerBrush), typeof(Brush), typeof(TrackMap),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PlayerBrushProperty = DependencyProperty.Register(
        nameof(PlayerBrush), typeof(Brush), typeof(TrackMap),
        new FrameworkPropertyMetadata(Brushes.Lime, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PathBrushProperty = DependencyProperty.Register(
        nameof(PathBrush), typeof(Brush), typeof(TrackMap),
        new FrameworkPropertyMetadata(Brushes.SlateGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<MapPoint>? Markers
    {
        get => (IEnumerable<MapPoint>?)GetValue(MarkersProperty);
        set => SetValue(MarkersProperty, value);
    }

    public IEnumerable<MapPoint>? Path
    {
        get => (IEnumerable<MapPoint>?)GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    public Brush MarkerBrush
    {
        get => (Brush)GetValue(MarkerBrushProperty);
        set => SetValue(MarkerBrushProperty, value);
    }

    public Brush PlayerBrush
    {
        get => (Brush)GetValue(PlayerBrushProperty);
        set => SetValue(PlayerBrushProperty, value);
    }

    public Brush PathBrush
    {
        get => (Brush)GetValue(PathBrushProperty);
        set => SetValue(PathBrushProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 2 || height <= 2) return;

        var markers = Markers as IReadOnlyList<MapPoint> ?? Markers?.ToArray() ?? Array.Empty<MapPoint>();
        var path = Path as IReadOnlyList<MapPoint> ?? Path?.ToArray() ?? Array.Empty<MapPoint>();
        if (markers.Count == 0 && Path is null) return;

        var minX = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var minZ = double.PositiveInfinity;
        var maxZ = double.NegativeInfinity;

        void Extend(double x, double z)
        {
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (z < minZ) minZ = z;
            if (z > maxZ) maxZ = z;
        }

        foreach (var p in Path ?? Array.Empty<MapPoint>())
        {
            if (double.IsNaN(p.X) || double.IsNaN(p.Z)) continue;
            Extend(p.X, p.Z);
        }

        foreach (var m in Markers ?? Array.Empty<MapPoint>())
        {
            Extend(m.X, m.Z);
        }

        if (double.IsInfinity(minX) || double.IsInfinity(minZ)) return;

        var spanX = Math.Max(Max(50, maxX - minX), 1);
        var spanZ = Math.Max(maxZ - minZ, 1);
        const double pad = 8.0;

        var scale = Math.Min(
            (width - pad * 2) / spanX,
            (height - pad * 2) / spanZ);

        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
        {
            scale = 1;
        }

        var offsetX = (width - spanX * scale) / 2.0 - minX * scale;
        var offsetY = (height - spanZ * scale) / 2.0 - minZ * scale;

        Point Map(double x, double z) => new(x * scale + offsetX, z * scale + offsetY);

        if (Path is { } pathEnum)
        {
            var pathList = pathEnum as IReadOnlyList<MapPoint> ?? pathEnum.ToArray();
            if (pathList.Count >= 2)
            {
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    var start = Map(pathList[0].X, pathList[0].Z);
                    context.BeginFigure(start, isFilled: false, isClosed: false);
                    for (var i = 1; i < pathList.Count; i++)
                    {
                        context.LineTo(Map(pathList[i].X, pathList[i].Z), isStroked: true, isSmoothJoin: true);
                    }
                }

                geometry.Freeze();
                var pen = new Pen(PathBrush, 1.5);
                pen.Freeze();
                dc.DrawGeometry(null, pen, geometry);
            }
        }

        if (Markers is { } markerEnum)
        {
            foreach (var marker in markerEnum)
            {
                var point = Map(marker.X, marker.Z);
                var brush = marker.IsPlayer ? PlayerBrush : MarkerBrush;
                var radius = marker.IsPlayer ? 6.0 : 4.0;
                dc.DrawEllipse(brush, null, point, radius, radius);
            }
        }
    }

    private static double Max(double a, double b) => a > b ? a : b;
}
