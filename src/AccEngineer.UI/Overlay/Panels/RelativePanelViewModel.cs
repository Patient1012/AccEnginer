using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using AccEngineer.Telemetry;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// Compact relative/nearby panel. ACC exposes no opponent names or timing, so each
/// other car is projected onto the player's recent driven line and ordered by that
/// progress; gaps are metre estimates and labelled as such. The player row is
/// highlighted.
/// </summary>
public sealed class RelativePanelViewModel : OverlayPanelViewModel
{
    private const int LinePoints = 900;
    private const int MaxRows = 8;

    public RelativePanelViewModel(IOverlayTelemetrySource telemetry) : base(telemetry, 100) { }

    public bool IsLive { get; private set; }

    public string StatusText { get; private set; } = "NO SIGNAL";

    public string HeaderText { get; private set; } = "NEARBY";

    public ObservableCollection<RelativeRow> Rows { get; } = new();

    protected override void Refresh(TelemetrySample? sample)
    {
        // Rebuilding the whole collection each tick keeps ordering stable and avoids
        // the flicker of mutating rows in place. The list is tiny (<= MaxRows).
        Rows.Clear();

        if (sample is null)
        {
            IsLive = false;
            StatusText = "NO SIGNAL";
            HeaderText = "NEARBY";
            OnAllPropertiesChanged();
            return;
        }

        IsLive = true;
        StatusText = "LIVE";
        // Make the estimate explicit: these are metres, not official gaps.
        HeaderText = "NEARBY · gap (m est.)";

        var cars = sample.CarsOnTrack;
        if (cars is not { Count: > 0 })
        {
            OnAllPropertiesChanged();
            return;
        }

        var line = TrackLineProjection.BuildLine(Telemetry.Recent(LinePoints));
        if (line.Count < 2)
        {
            OnAllPropertiesChanged();
            return;
        }

        var playerId = sample.PlayerCarId;
        var playerX = sample.PositionXMeters;
        var playerZ = sample.PositionZMeters;

        var entries = new List<(int CarId, bool IsPlayer, double? Progress)>(cars.Count + 1);
        var hasPlayer = false;
        foreach (var car in cars)
        {
            var isPlayer = playerId is { } id
                ? car.CarId == id
                : playerX is { } bx && playerZ is { } bz
                    && Math.Abs(car.XMeters - bx) < 4f && Math.Abs(car.ZMeters - bz) < 4f;
            if (isPlayer) hasPlayer = true;
            entries.Add((car.CarId, isPlayer, TrackLineProjection.ProjectToLine(car.XMeters, car.ZMeters, line)));
        }

        if (!hasPlayer && playerX is { } px && playerZ is { } pz)
        {
            entries.Add((playerId ?? 0, true, sample.TrackProgressMeters ?? TrackLineProjection.ProjectToLine(px, pz, line)));
        }

        var sorted = entries.OrderBy(e => e.Progress ?? double.MaxValue).Take(MaxRows).ToArray();
        var leader = sorted.Length > 0 ? sorted[0].Progress : null;
        for (var i = 0; i < sorted.Length; i++)
        {
            var entry = sorted[i];
            double? gap = entry.Progress is { } progress && leader is { } lead ? Math.Max(0, progress - lead) : null;
            Rows.Add(new RelativeRow
            {
                CarId = entry.CarId,
                IsPlayer = entry.IsPlayer,
                Position = i + 1,
                GapText = gap is { } g ? g.ToString("0", CultureInfo.InvariantCulture) + " m" : "--",
                Stripe = CarColour(entry.CarId)
            });
        }

        OnAllPropertiesChanged();
    }

    /// <summary>
    /// Stable per-car colour derived from the car id. ACC supplies no class/driver
    /// colours, so we generate a consistent one per id to keep identity readable.
    /// </summary>
    private static SolidColorBrush CarColour(int carId)
    {
        var hue = (carId * 47) % 360;
        var brush = new SolidColorBrush(HsvToRgb(hue / 360.0, 0.55, 0.85));
        brush.Freeze();
        return brush;
    }

    private static System.Windows.Media.Color HsvToRgb(double h, double s, double v)
    {
        var i = (int)(h * 6);
        var f = (h * 6) - i;
        var p = v * (1 - s);
        var q = v * (1 - (f * s));
        var t = v * (1 - ((1 - f) * s));
        var (r, g, b) = (i % 6) switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q)
        };
        return System.Windows.Media.Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}
