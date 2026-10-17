using System.Windows;
using System.Windows.Media;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Controls;

/// <summary>
/// One point on the live map (world X/Z) and whether it is the player. An optional
/// per-marker brush lets callers give each car a stable colour; when omitted the
/// control's shared marker/player brushes are used.
/// </summary>
public readonly record struct MapPoint(double X, double Z, bool IsPlayer, Brush? Brush = null);

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

    /// <summary>
    /// Optional geometry used only for fitting. Set it to the cached static outline so
    /// the transform stays fixed while live markers move (avoids fit jitter). When null
    /// the fit uses <see cref="Path"/> plus <see cref="Markers"/>.
    /// </summary>
    public static readonly DependencyProperty FitPathProperty = DependencyProperty.Register(
        nameof(FitPath), typeof(IEnumerable<MapPoint>), typeof(TrackMap),
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

    public IEnumerable<MapPoint>? FitPath
    {
        get => (IEnumerable<MapPoint>?)GetValue(FitPathProperty);
        set => SetValue(FitPathProperty, value);
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
        if (markers.Count == 0 && path.Count == 0) return;

        // Fit against the cached static outline when supplied so live markers moving
        // does not shift the transform (no jitter); otherwise fit everything.
        var fitSource = FitPath as IReadOnlyList<MapPoint> ?? FitPath?.ToArray();
        var fitPoints = new List<(double X, double Z)>();
        if (fitSource is { Count: > 0 })
        {
            foreach (var p in fitSource) fitPoints.Add((p.X, p.Z));
        }
        else
        {
            foreach (var p in path) fitPoints.Add((p.X, p.Z));
            foreach (var m in markers) fitPoints.Add((m.X, m.Z));
        }

        var calibration = TrackMapCalibration.TryCreate(fitPoints, width, height);
        if (calibration is not { } map) return;

        System.Windows.Point Project(double x, double z)
        {
            var c = map.Project(x, z);
            return new System.Windows.Point(c.X, c.Y);
        }

        if (path.Count >= 2)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                var start = Project(path[0].X, path[0].Z);
                context.BeginFigure(start, isFilled: false, isClosed: false);
                for (var i = 1; i < path.Count; i++)
                {
                    context.LineTo(Project(path[i].X, path[i].Z), isStroked: true, isSmoothJoin: true);
                }
            }

            geometry.Freeze();
            var pen = new Pen(PathBrush, 1.5);
            if (pen.CanFreeze) pen.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }

        foreach (var marker in markers)
        {
            var point = Project(marker.X, marker.Z);
            var brush = marker.Brush ?? (marker.IsPlayer ? PlayerBrush : MarkerBrush);
            var radius = marker.IsPlayer ? 6.0 : 4.0;
            dc.DrawEllipse(brush, null, point, radius, radius);
        }
    }
}
