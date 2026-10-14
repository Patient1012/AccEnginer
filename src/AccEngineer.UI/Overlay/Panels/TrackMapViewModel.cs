using AccEngineer.Telemetry;
using AccEngineer.UI.Controls;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// Top-down track map. There is no official track-outline source in the shared memory
/// API, so the static outline is built from the player's own driven line and cached
/// separately from the live car markers (which are rebuilt every tick). The same
/// calibration transform is applied to both so markers stay aligned to the outline.
///
/// Handles the honest empty states: unknown track, no other cars, and "waiting for the
/// car to move" - it never invents geometry.
/// </summary>
public sealed class TrackMapViewModel : OverlayPanelViewModel
{
    /// <summary>Samples of the driven line used to build the cached outline (~90 s at 50 ms).</summary>
    private const int OutlineSamples = 1800;

    /// <summary>Rebuild the cached outline roughly once a second.</summary>
    private const int OutlineRefreshTicks = 20;

    /// <summary>Cap the outline point count so per-frame geometry stays cheap.</summary>
    private const int MaxOutlinePoints = 600;

    private int _tick;
    private IReadOnlyList<MapPoint> _outline = Array.Empty<MapPoint>();

    public TrackMapViewModel(IOverlayTelemetrySource telemetry) : base(telemetry, 50) { }

    public bool IsLive { get; private set; }

    public string StatusText { get; private set; } = "NO SIGNAL";

    public string TrackText { get; private set; } = "--";

    public string NoteText { get; private set; } = string.Empty;

    public bool HasNote { get; private set; }

    public bool HasMap { get; private set; }

    /// <summary>Cached static outline (from the player's driven line).</summary>
    public IReadOnlyList<MapPoint> Outline { get; private set; } = Array.Empty<MapPoint>();

    /// <summary>Live car markers for the current tick.</summary>
    public IReadOnlyList<MapPoint> Markers { get; private set; } = Array.Empty<MapPoint>();

    protected override void Refresh(TelemetrySample? sample)
    {
        if (sample is null)
        {
            IsLive = false;
            StatusText = "NO SIGNAL";
            TrackText = "--";
            _outline = Array.Empty<MapPoint>();
            Outline = Array.Empty<MapPoint>();
            Markers = Array.Empty<MapPoint>();
            HasMap = false;
            NoteText = string.Empty;
            HasNote = false;
            OnAllPropertiesChanged();
            return;
        }

        IsLive = true;
        StatusText = "LIVE";
        TrackText = string.IsNullOrEmpty(sample.TrackName) ? "--" : sample.TrackName;

        // Static geometry is cached and refreshed on a slower cadence than the markers.
        if (_tick++ % OutlineRefreshTicks == 0)
        {
            _outline = BuildOutline();
            Outline = _outline;
        }

        Markers = BuildMarkers(sample);

        var hasOutline = _outline.Count >= 2;
        var hasCars = Markers.Count > 0;
        HasMap = hasOutline || hasCars;

        NoteText = (hasOutline, hasCars) switch
        {
            (false, false) => "Outline unavailable - waiting for the car to move.",
            (false, true) => "Outline unavailable - showing current cars only.",
            (true, false) => "No other cars on track.",
            _ => string.Empty
        };
        HasNote = NoteText.Length > 0;

        OnAllPropertiesChanged();
    }

    /// <summary>
    /// Builds the cached outline from the player's recent driven positions, downsampled
    /// to keep the geometry cheap. Returns empty when there is not yet a usable line.
    /// </summary>
    private IReadOnlyList<MapPoint> BuildOutline()
    {
        var recent = Telemetry.Recent(OutlineSamples);
        var points = new List<MapPoint>(recent.Count);
        foreach (var s in recent)
        {
            if (s.PositionXMeters is { } x && s.PositionZMeters is { } z)
            {
                points.Add(new MapPoint(x, z, false));
            }
        }

        if (points.Count <= MaxOutlinePoints) return points;

        // Uniform downsample so the busiest outline still renders smoothly.
        var stride = (int)Math.Ceiling(points.Count / (double)MaxOutlinePoints);
        var reduced = new List<MapPoint>(MaxOutlinePoints + 1);
        for (var i = 0; i < points.Count; i += stride) reduced.Add(points[i]);
        // Always keep the last point so the line reaches the current position.
        if (reduced[^1] != points[^1]) reduced.Add(points[^1]);
        return reduced;
    }

    private IReadOnlyList<MapPoint> BuildMarkers(TelemetrySample sample)
    {
        var cars = sample.CarsOnTrack;
        var markers = new List<MapPoint>(cars?.Count ?? 0);

        if (cars is { Count: > 0 })
        {
            var playerId = sample.PlayerCarId;
            var px = sample.PositionXMeters;
            var pz = sample.PositionZMeters;
            foreach (var car in cars)
            {
                var isPlayer = playerId is { } id
                    ? car.CarId == id
                    : px is { } x && pz is { } z
                        && Math.Abs(car.XMeters - x) < 4f && Math.Abs(car.ZMeters - z) < 4f;
                // Opponents get their stable colour; the player is left to the control's
                // distinct player brush so it always stands out.
                markers.Add(new MapPoint(car.XMeters, car.ZMeters, isPlayer, isPlayer ? null : CarColour.ForCar(car.CarId)));
            }

            return markers;
        }

        // No car list (replay or missing opponents): still show the player if we can.
        if (sample.PositionXMeters is { } soloX && sample.PositionZMeters is { } soloZ)
        {
            markers.Add(new MapPoint(soloX, soloZ, true));
        }

        return markers;
    }
}
