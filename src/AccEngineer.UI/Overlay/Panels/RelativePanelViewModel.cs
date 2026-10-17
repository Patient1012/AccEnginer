using System.Collections.ObjectModel;
using System.Globalization;
using AccEngineer.Telemetry;
using AccEngineer.Telemetry.Broadcasting;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// Compact relative/nearby panel. When the ACC Broadcasting API is connected the
/// driver names, race numbers and official ordering come from it, and the interval
/// column stays a metre estimate derived from spline positions (ACC does not broadcast
/// official time gaps) and is labelled as such. Without broadcasting the panel falls
/// back to projecting each car onto the player's recent driven line, in which case
/// names are the explicit "CAR nn" placeholders.
/// </summary>
public sealed class RelativePanelViewModel : OverlayPanelViewModel
{
    private const int LinePoints = 900;

    private int _maxRows = 8;

    public RelativePanelViewModel(IOverlayTelemetrySource telemetry) : base(telemetry, 100) { }

    public bool IsLive { get; private set; }

    public string StatusText { get; private set; } = "NO SIGNAL";

    public string HeaderText { get; private set; } = "NEARBY";

    /// <summary>Whether the gap column is shown (configurable in the layout editor).</summary>
    public bool ShowGapColumn { get; private set; } = true;

    /// <summary>Whether the driver/name column is shown (configurable in the layout editor).</summary>
    public bool ShowNameColumn { get; private set; } = true;

    public ObservableCollection<RelativeRow> Rows { get; } = new();

    /// <inheritdoc />
    public override void ApplyAppearance(OverlayAppearance appearance)
    {
        _maxRows = Math.Clamp(appearance.RelativeRows, 3, 20);
        ShowGapColumn = appearance.RelativeShowGap;
        ShowNameColumn = appearance.RelativeShowName;
        OnAllPropertiesChanged();
    }

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

        // Prefer the ACC Broadcasting API: it is the only source of opponent names,
        // race numbers and the official running order.
        if (Telemetry.Standings is { Competitors.Count: > 0 } standings
            && TryBuildFromStandings(standings))
        {
            OnAllPropertiesChanged();
            return;
        }

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

        var sorted = entries.OrderBy(e => e.Progress ?? double.MaxValue).Take(_maxRows).ToArray();
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
                Name = $"CAR {entry.CarId:00}",
                GapText = gap is { } g ? g.ToString("0", CultureInfo.InvariantCulture) + " m" : "--",
                Stripe = CarColour.ForCar(entry.CarId)
            });
        }

        OnAllPropertiesChanged();
    }

    /// <summary>
    /// Builds the nearby list from broadcast standings: a window of cars centred on the
    /// player, ordered as the game reports, with names/numbers from the API and metre
    /// intervals derived from spline positions. Returns false when the player's car is
    /// not present in the feed so the caller can fall back to line projection.
    /// </summary>
    private bool TryBuildFromStandings(CompetitorStandings standings)
    {
        var competitors = standings.Competitors;
        var playerIndex = -1;
        for (var i = 0; i < competitors.Count; i++)
        {
            if (competitors[i].CarIndex == standings.FocusedCarIndex) { playerIndex = i; break; }
        }

        if (playerIndex < 0) return false;

        HeaderText = "RELATIVE · gap (m est.)";

        var player = competitors[playerIndex];
        var playerCum = (player.LapCount ?? 0) + player.SplinePosition;
        var trackMeters = standings.TrackMeters is { } meters and > 0 ? meters : (int?)null;

        var start = Math.Clamp(playerIndex - _maxRows / 2, 0, Math.Max(0, competitors.Count - _maxRows));
        var end = Math.Min(competitors.Count, start + _maxRows);
        for (var i = start; i < end; i++)
        {
            var car = competitors[i];
            var isPlayer = i == playerIndex;
            var carCum = (car.LapCount ?? 0) + car.SplinePosition;

            string gapText;
            if (isPlayer)
            {
                gapText = "0 m";
            }
            else if (trackMeters is { } tm)
            {
                // Positive means the car is further along the lap, i.e. ahead of the player.
                var deltaMeters = (carCum - playerCum) * tm;
                var sign = deltaMeters >= 0 ? "-" : "+";
                gapText = sign + Math.Abs(deltaMeters).ToString("0", CultureInfo.InvariantCulture) + " m";
            }
            else
            {
                gapText = "--";
            }

            Rows.Add(new RelativeRow
            {
                CarId = car.CarIndex,
                IsPlayer = isPlayer,
                Position = car.Position ?? i + 1,
                Name = car.DisplayName,
                GapText = gapText,
                Stripe = CarColour.ForCar(car.CarIndex)
            });
        }

        return true;
    }
}
