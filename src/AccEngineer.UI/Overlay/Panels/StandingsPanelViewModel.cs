using System.Collections.ObjectModel;
using System.Globalization;
using AccEngineer.Telemetry;
using AccEngineer.Telemetry.Broadcasting;
using AccEngineer.UI.Overlay.Controls;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// Full-field standings tower. Names, race numbers, class categories and order come from
/// the ACC Broadcasting API; the interval column is a metre estimate derived from each
/// car's spline position and the track length (ACC does not broadcast official time gaps),
/// so it is labelled as an estimate. When broadcasting is unavailable the panel shows the
/// explicit reason rather than inventing driver names, gaps or lap times.
///
/// Drivers are grouped under class headers using the ACC cup category; within a group they
/// keep their official classified position. The interval is always computed against the car
/// ahead in the overall classification, so regrouping never changes a gap value.
/// </summary>
public sealed class StandingsPanelViewModel : OverlayPanelViewModel
{
    private int _maxRows = 12;

    public StandingsPanelViewModel(IOverlayTelemetrySource telemetry) : base(telemetry, 200) { }

    public bool IsLive { get; private set; }

    public string StatusText { get; private set; } = "NO SIGNAL";

    /// <summary>Elapsed session clock (H:MM:SS), or "--:--:--" when unknown.</summary>
    public string SessionTimerText { get; private set; } = "--:--:--";

    /// <summary>Session type and track, for example "RACE  ·  Spa-Francorchamps". Empty when unknown.</summary>
    public string SessionContextText { get; private set; } = string.Empty;

    /// <summary>Player's classified position, for example "P6". Empty when unknown.</summary>
    public string PlayerPositionText { get; private set; } = string.Empty;

    /// <summary>Player's class label, for example "OVERALL". Empty when unknown.</summary>
    public string PlayerClassText { get; private set; } = string.Empty;

    /// <summary>Class headers and driver rows, in display order. Rebuilt every tick.</summary>
    public ObservableCollection<StandingsListItem> Items { get; } = new();

    /// <inheritdoc />
    public override void ApplyAppearance(OverlayAppearance appearance)
    {
        _maxRows = Math.Clamp(appearance.StandingsRows, 3, 30);
        // Rebuild immediately so an editor row-count change is reflected without
        // waiting for the next poll tick.
        Refresh(Telemetry.Latest);
    }

    protected override void Refresh(TelemetrySample? sample)
    {
        Items.Clear();

        var standings = Telemetry.Standings;
        if (standings is null)
        {
            IsLive = false;
            StatusText = Telemetry.StandingsStatusText;
            SessionTimerText = "--:--:--";
            SessionContextText = string.Empty;
            PlayerPositionText = string.Empty;
            PlayerClassText = string.Empty;
            OnAllPropertiesChanged();
            return;
        }

        IsLive = true;
        StatusText = "LIVE";
        SessionTimerText = FormatSessionClock(standings.SessionTimeMs);
        SessionContextText = BuildContext(sample, standings);
        PlayerPositionText = standings.Player?.Position is { } position
            ? "P" + position.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        PlayerClassText = StandingsClass.Label(standings.Player?.CupCategory);

        var competitors = standings.Competitors;

        // Genuine overall fastest lap: the minimum best lap that actually exists in the
        // field. Used only to colour a value; nothing is invented for cars without one.
        int? fieldBest = null;
        foreach (var car in competitors)
        {
            if (car.BestLapMs is { } best and > 0 && (fieldBest is null || best < fieldBest))
            {
                fieldBest = best;
            }
        }

        // Official running order. The source list is not guaranteed to be classified order
        // (the test feed is car-index ordered), so sort by class position, keeping the
        // original order as a stable tie-break.
        var ordered = competitors
            .Select((car, index) => (Car: car, Index: index))
            .OrderBy(t => t.Car.Position ?? int.MaxValue)
            .ThenBy(t => t.Index)
            .Select(t => t.Car)
            .ToList();

        // Interval to the car physically ahead in the classification, independent of how the
        // list is later grouped by class.
        var gapByCar = new Dictionary<int, string>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            gapByCar[ordered[i].CarIndex] = FormatGap(
                i > 0 ? ordered[i - 1] : null, ordered[i], standings.TrackMeters);
        }

        var shown = ordered.Take(_maxRows).ToList();

        if (shown.Any(c => c.CupCategory is not null))
        {
            var rowIndex = 0;
            foreach (var category in StandingsClass.DisplayOrder)
            {
                var members = shown
                    .Where(c => c.CupCategory == category)
                    .OrderBy(c => c.Position ?? int.MaxValue)
                    .ToList();
                if (members.Count == 0) continue;

                Items.Add(new StandingsClassHeader
                {
                    ClassLabel = StandingsClass.Label(category),
                    CountText = members.Count.ToString(CultureInfo.InvariantCulture),
                    Accent = StandingsClass.Accent(category)
                });

                foreach (var car in members)
                {
                    Items.Add(BuildRow(car, gapByCar, fieldBest, rowIndex++));
                }
            }
        }
        else
        {
            // Fallback: no class metadata at all, so show a single ungrouped list.
            for (var i = 0; i < shown.Count; i++)
            {
                Items.Add(BuildRow(shown[i], gapByCar, fieldBest, i));
            }
        }

        OnAllPropertiesChanged();
    }

    private static StandingsRow BuildRow(
        Competitor car, IReadOnlyDictionary<int, string> gaps, int? fieldBest, int rowIndex)
    {
        var hasFastest = car.BestLapMs is { } best and > 0;
        var hasLast = car.LastLapMs is { } last and > 0;
        var isFieldBest = hasFastest && fieldBest is { } fb && car.BestLapMs == fb;

        return new StandingsRow
        {
            CarIndex = car.CarIndex,
            Position = car.Position ?? 0,
            IsPlayer = car.IsPlayer,
            InPit = car.IsInPit,
            Alternate = rowIndex % 2 == 1,
            RaceNumber = car.RaceNumber,
            Name = car.DisplayName,
            GapText = gaps.TryGetValue(car.CarIndex, out var gap) ? gap : "--",
            FastestText = hasFastest ? Display.LapTimeMs(car.BestLapMs) : "--",
            FastestState = isFieldBest ? TimingState.SessionBest
                : hasFastest ? TimingState.Normal
                : TimingState.Unavailable,
            LastText = hasLast ? Display.LapTimeMs(car.LastLapMs) : "--",
            LastState = hasLast ? TimingState.Normal : TimingState.Unavailable,
            ClassAccent = StandingsClass.Accent(car.CupCategory)
        };
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

    /// <summary>Formats the elapsed session time as <c>H:MM:SS</c>; "--:--:--" when invalid.</summary>
    private static string FormatSessionClock(float sessionTimeMs)
    {
        if (!float.IsFinite(sessionTimeMs) || sessionTimeMs < 0) return "--:--:--";
        var span = TimeSpan.FromMilliseconds(sessionTimeMs);
        return $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}";
    }

    /// <summary>Session type and track for the header, joined with a separator; empty parts dropped.</summary>
    private static string BuildContext(TelemetrySample? sample, CompetitorStandings standings)
    {
        var parts = new List<string>(2);

        var type = sample is null ? string.Empty : SessionTypeLabel(sample.SessionType);
        if (type.Length > 0) parts.Add(type);

        var track = !string.IsNullOrWhiteSpace(standings.TrackName) ? standings.TrackName!
            : !string.IsNullOrWhiteSpace(sample?.TrackName) ? sample!.TrackName!
            : string.Empty;
        if (track.Length > 0) parts.Add(track);

        return string.Join("  ·  ", parts);
    }

    /// <summary>Uppercase session-type label for the header; empty when unknown.</summary>
    public static string SessionTypeLabel(SessionType type) => type switch
    {
        SessionType.Practice => "PRACTICE",
        SessionType.Qualify => "QUALIFY",
        SessionType.Race => "RACE",
        SessionType.Hotlap => "HOTLAP",
        SessionType.TimeAttack => "TIME ATTACK",
        SessionType.Drift => "DRIFT",
        SessionType.Drag => "DRAG",
        SessionType.HotStint => "HOT STINT",
        SessionType.HotStintSuperpole => "SUPERPOLE",
        _ => string.Empty
    };
}
