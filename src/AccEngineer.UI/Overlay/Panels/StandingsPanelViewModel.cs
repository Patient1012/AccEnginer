using System.Collections.ObjectModel;
using System.Globalization;
using AccEngineer.Analysis.Telemetry;
using AccEngineer.Telemetry;
using AccEngineer.Telemetry.Broadcasting;
using AccEngineer.UI.Overlay.Controls;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// Full-field standings tower. Names, race numbers, class categories and order come from
/// the ACC Broadcasting API; the interval column shows signed seconds and is labelled as
/// an estimate, because ACC broadcasts no official car-to-car time gaps: the source's own
/// value is used when it genuinely reports one (the test feed), otherwise the interval is
/// estimated by integrating the player's recent pace over the distance to the car ahead
/// (see <see cref="TrackPaceProfile"/>). When broadcasting is unavailable the panel shows
/// the explicit reason rather than inventing driver names, gaps or lap times.
///
/// Drivers are grouped under class headers using the ACC cup category; within a group they
/// keep their official classified position. The interval is always computed against the car
/// ahead in the overall classification, so regrouping never changes a gap value.
/// </summary>
public sealed class StandingsPanelViewModel : OverlayPanelViewModel
{
    /// <summary>Recent-sample window for the pace profile, matching the relative panel.</summary>
    private const int PaceWindowPoints = 900;

    /// <summary>Beyond this, an interval derived from two reported time gaps cannot be
    /// trusted (the values are wrapped to half a lap, so a wrap straddle reads roughly a
    /// lap apart) and the pace estimate is used instead.</summary>
    private const double MaxReportedIntervalSeconds = 60;

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

        // Official running order, sorted defensively: sources are contracted to send
        // classified order (the ACC client and the test feed both do), so this is a
        // no-op for well-behaved sources but keeps the tower correct regardless.
        var ordered = competitors
            .Select((car, index) => (Car: car, Index: index))
            .OrderBy(t => t.Car.Position ?? int.MaxValue)
            .ThenBy(t => t.Index)
            .Select(t => t.Car)
            .ToList();

        // Pace profile used for the interval estimate, built once per tick from the
        // player's recent samples. Null on a short history → intervals read "--".
        var profile = TrackPaceProfile.Build(Telemetry.Recent(PaceWindowPoints));

        // Interval to the car physically ahead in the classification, independent of how the
        // list is later grouped by class.
        var gapByCar = new Dictionary<int, (double? Seconds, string Text)>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            gapByCar[ordered[i].CarIndex] = FormatInterval(
                i > 0 ? ordered[i - 1] : null, ordered[i], standings.TrackMeters, profile);
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
        Competitor car, IReadOnlyDictionary<int, (double? Seconds, string Text)> gaps,
        int? fieldBest, int rowIndex)
    {
        var hasFastest = car.BestLapMs is { } best and > 0;
        var hasLast = car.LastLapMs is { } last and > 0;
        var isFieldBest = hasFastest && fieldBest is { } fb && car.BestLapMs == fb;
        var gap = gaps.TryGetValue(car.CarIndex, out var found) ? found : (null, "--");

        return new StandingsRow
        {
            CarIndex = car.CarIndex,
            Position = car.Position ?? 0,
            IsPlayer = car.IsPlayer,
            InPit = car.IsInPit,
            Alternate = rowIndex % 2 == 1,
            RaceNumber = car.RaceNumber,
            Name = car.DisplayName,
            GapSeconds = gap.Seconds,
            GapText = gap.Text,
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
    /// Interval to the car ahead, in seconds: a laps-down indicator when the lap counts
    /// differ, otherwise the sources' own time gaps when both cars report one (test
    /// feed only), otherwise an estimate from the player's recent pace over the distance
    /// between the two cars. Returns "--" for the leader and whenever the data cannot
    /// support an estimate; the number is always positive (how far the car trails).
    /// </summary>
    private static (double? Seconds, string Text) FormatInterval(
        Competitor? ahead, Competitor car, int? trackMeters, TrackPaceProfile? profile)
    {
        if (ahead is null) return (null, "--");

        var aheadLaps = ahead.LapCount ?? 0;
        var carLaps = car.LapCount ?? 0;
        var lapDiff = aheadLaps - carLaps;
        if (lapDiff >= 1) return (null, $"+{lapDiff.ToString(CultureInfo.InvariantCulture)}L");

        // Genuine reported gaps (signed distance to the player, negative = ahead). The
        // difference between two of them is the true interval — but both are wrapped to
        // half a lap, so anything implausibly large for adjacent cars is a wrap artefact
        // and falls through to the estimate.
        if (ahead.RelativeTimeGapMs is { } aheadMs && car.RelativeTimeGapMs is { } carMs
            && aheadMs is >= -RelativePanelViewModel.MaxPlausibleGapMs and <= RelativePanelViewModel.MaxPlausibleGapMs
            && carMs is >= -RelativePanelViewModel.MaxPlausibleGapMs and <= RelativePanelViewModel.MaxPlausibleGapMs)
        {
            var reported = (carMs - aheadMs) / 1000d;
            if (reported > 0 && reported <= MaxReportedIntervalSeconds)
            {
                return (reported, Display.RelativeGapSeconds(reported));
            }
        }

        // Estimate: integrate the player's pace over the distance from the car ahead to
        // this car. The gap must be a real forward distance (0 .. one lap) on a credible
        // track length, otherwise it reads "--" instead of a fabricated number.
        if (profile is not null
            && trackMeters is { } meters && meters > 0 && meters <= RelativePanelViewModel.MaxPlausibleTrackMeters
            && ahead.SplinePosition is >= 0f and <= 1f
            && car.SplinePosition is >= 0f and <= 1f)
        {
            var aheadCum = aheadLaps + ahead.SplinePosition;
            var carCum = carLaps + car.SplinePosition;
            var gapMeters = (aheadCum - carCum) * meters;
            if (gapMeters >= 0 && gapMeters <= meters)
            {
                var duration = profile.TimeDeltaSeconds(ahead.SplinePosition * meters, gapMeters, meters);
                if (duration is { } seconds)
                {
                    return (seconds, Display.RelativeGapSeconds(seconds));
                }
            }
        }

        return (null, "--");
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
