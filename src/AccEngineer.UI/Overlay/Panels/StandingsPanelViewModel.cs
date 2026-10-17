using System.Collections.ObjectModel;
using System.Globalization;
using AccEngineer.Telemetry;
using AccEngineer.Telemetry.Broadcasting;
using AccEngineer.UI.Overlay.Controls;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// Full-field standings tower. Names, race numbers and order come from the ACC
/// Broadcasting API; the interval column is a metre estimate derived from each car's
/// spline position and the track length (ACC does not broadcast official time gaps),
/// so it is labelled as an estimate. When broadcasting is unavailable the panel shows
/// the explicit reason rather than inventing driver names or gaps.
/// </summary>
public sealed class StandingsPanelViewModel : OverlayPanelViewModel
{
    private int _maxRows = 12;

    public StandingsPanelViewModel(IOverlayTelemetrySource telemetry) : base(telemetry, 200) { }

    public bool IsLive { get; private set; }

    public string StatusText { get; private set; } = "NO SIGNAL";

    public string HeaderText { get; private set; } = "STANDINGS";

    /// <summary>Track name when known, otherwise blank.</summary>
    public string SubtitleText { get; private set; } = string.Empty;

    public ObservableCollection<StandingsRow> Rows { get; } = new();

    /// <inheritdoc />
    public override void ApplyAppearance(OverlayAppearance appearance)
    {
        _maxRows = Math.Clamp(appearance.StandingsRows, 3, 30);
        OnAllPropertiesChanged();
    }

    protected override void Refresh(TelemetrySample? sample)
    {
        Rows.Clear();

        var standings = Telemetry.Standings;
        if (standings is null)
        {
            IsLive = false;
            StatusText = Telemetry.StandingsStatusText;
            HeaderText = "STANDINGS";
            SubtitleText = string.Empty;
            OnAllPropertiesChanged();
            return;
        }

        IsLive = true;
        StatusText = "LIVE";
        HeaderText = "STANDINGS · gap (m est.)";
        SubtitleText = string.IsNullOrWhiteSpace(standings.TrackName) ? string.Empty : standings.TrackName;

        var competitors = standings.Competitors;
        for (var i = 0; i < competitors.Count && i < _maxRows; i++)
        {
            var car = competitors[i];
            var ahead = i > 0 ? competitors[i - 1] : null;
            Rows.Add(new StandingsRow
            {
                CarIndex = car.CarIndex,
                Position = car.Position ?? 0,
                IsPlayer = car.IsPlayer,
                InPit = car.IsInPit,
                RaceNumber = car.RaceNumber,
                Name = car.DisplayName,
                LastLapText = car.LastLapMs is { } lastMs and > 0 ? Display.LapTimeMs(lastMs) : "--",
                GapText = FormatGap(ahead, car, standings.TrackMeters),
                Stripe = CarColour.ForCar(car.CarIndex)
            });
        }

        OnAllPropertiesChanged();
    }

    /// <summary>
    /// Interval to the car ahead: a laps-down indicator when the lap counts differ,
    /// otherwise a metre estimate from spline positions and the track length. Returns
    /// "--" for the leader and whenever the source cannot support an estimate.
    /// </summary>
    private static string FormatGap(Competitor? ahead, Competitor car, int? trackMeters)
    {
        if (ahead is null) return "--";

        var aheadLaps = ahead.LapCount ?? 0;
        var carLaps = car.LapCount ?? 0;
        var lapDiff = aheadLaps - carLaps;
        if (lapDiff >= 1) return $"+{lapDiff.ToString(CultureInfo.InvariantCulture)}L";

        if (trackMeters is not { } meters) return "--";
        if (meters <= 0) return "--";

        var aheadCum = aheadLaps + ahead.SplinePosition;
        var carCum = carLaps + car.SplinePosition;
        var gapMeters = (aheadCum - carCum) * meters;
        if (gapMeters < 0 || gapMeters > meters) return "--";

        return gapMeters.ToString("0", CultureInfo.InvariantCulture) + " m";
    }
}
