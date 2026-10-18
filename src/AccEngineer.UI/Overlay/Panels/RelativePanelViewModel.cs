using System.Collections.ObjectModel;
using System.Globalization;
using AccEngineer.Analysis.Telemetry;
using AccEngineer.Telemetry;
using AccEngineer.Telemetry.Broadcasting;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// Compact broadcast-style relative leaderboard. Gaps are signed time gaps in seconds
/// (behind the player is positive, ahead is negative, the player reads 0.000). When a
/// source genuinely knows the time gap — the deterministic test feed — that value is
/// used directly; the real ACC Broadcasting API reports none, so live gaps are an
/// estimate obtained by integrating the player's own recent pace over the distance
/// derived from spline positions wrapped to the nearest lap position (see
/// <see cref="TrackPaceProfile"/>). Names, race numbers, vehicle classes, driver
/// categories and the official ordering come from broadcasting when it is connected;
/// without it the panel falls back to projecting cars onto the player's recent driven
/// line, where names are the explicit "CAR nn" placeholders.
/// </summary>
public sealed class RelativePanelViewModel : OverlayPanelViewModel
{
    private const int LinePoints = 900;

    /// <summary>Sanity bound for the broadcast track length. Real circuits are a few
    /// kilometres; the broken values in the wild were hundreds of thousands of metres,
    /// so anything beyond this is rejected rather than displayed.</summary>
    internal const double MaxPlausibleTrackMeters = 50_000;

    /// <summary>Sanity bound for a reported time gap (ten minutes — far wider than any
    /// real half-lap wrap, so only corrupt values are rejected). Beyond it the gap
    /// reads "--" instead of a wild number.</summary>
    internal const int MaxPlausibleGapMs = 600_000;

    private int _maxRows = 8;

    public RelativePanelViewModel(IOverlayTelemetrySource telemetry) : base(telemetry, 100) { }

    public bool IsLive { get; private set; }

    public string StatusText { get; private set; } = "NO SIGNAL";

    /// <summary>Header left: air temperature, e.g. "27c". Empty when unknown.</summary>
    public string HeaderLeftText { get; private set; } = string.Empty;

    /// <summary>Header centre: compact "TEST"/"NO SIGNAL" indicator, empty when live.
    /// Strength of field is omitted — the data model has no ratings.</summary>
    public string HeaderCentreText { get; private set; } = "NO SIGNAL";

    /// <summary>Header right: player position over active cars, e.g. "6/25". Empty when unknown.</summary>
    public string HeaderRightText { get; private set; } = string.Empty;

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
            HeaderLeftText = string.Empty;
            HeaderCentreText = "NO SIGNAL";
            HeaderRightText = string.Empty;
            OnAllPropertiesChanged();
            return;
        }

        IsLive = true;
        StatusText = "LIVE";

        HeaderLeftText = sample.AirTempC is { } air
            ? air.ToString("0", CultureInfo.InvariantCulture) + "c"
            : string.Empty;
        HeaderCentreText = IsTestData ? "TEST" : string.Empty;
        HeaderRightText = sample.PlayerPosition is { } position && sample.ActiveCars is { } activeCars
            ? position.ToString(CultureInfo.InvariantCulture) + "/" + activeCars.ToString(CultureInfo.InvariantCulture)
            : string.Empty;

        // Pace profile for distance→time conversion, built once per tick from the same
        // recent window the line projection uses. Null when history is too short, in
        // which case gaps fall back to "--" rather than a fabricated number.
        var profile = TrackPaceProfile.Build(Telemetry.Recent(LinePoints));

        // Prefer the ACC Broadcasting API: it is the only source of opponent names,
        // race numbers, cup classes and the official running order.
        if (Telemetry.Standings is { Competitors.Count: > 0 } standings
            && TryBuildFromStandings(standings, profile))
        {
            OnAllPropertiesChanged();
            return;
        }

        BuildFromProjection(sample, profile);
        OnAllPropertiesChanged();
    }

    /// <summary>
    /// Builds the nearby list from broadcast standings: a window of cars centred on the
    /// player, ordered as the game reports, with names/numbers/classes from the API and
    /// signed time gaps — the feed's genuine value when it reports one, otherwise an
    /// estimate from the player's pace profile. Returns false when the player's car is
    /// not present in the feed so the caller can fall back to line projection.
    /// </summary>
    private bool TryBuildFromStandings(CompetitorStandings standings, TrackPaceProfile? profile)
    {
        var competitors = standings.Competitors;
        var playerIndex = -1;
        for (var i = 0; i < competitors.Count; i++)
        {
            if (competitors[i].CarIndex == standings.FocusedCarIndex) { playerIndex = i; break; }
        }

        if (playerIndex < 0) return false;

        var player = competitors[playerIndex];
        var trackMeters = IsPlausibleTrackLength(standings.TrackMeters) ? standings.TrackMeters!.Value : 0d;
        var playerHasLaps = player.LapCount is not null;
        var playerSplineValid = player.SplinePosition is >= 0f and <= 1f;
        var playerCum = player.LapCount is { } playerLaps ? playerLaps + player.SplinePosition : 0d;
        var playerProgressMeters = playerSplineValid && trackMeters > 0
            ? player.SplinePosition * trackMeters
            : 0d;

        var start = Math.Clamp(playerIndex - _maxRows / 2, 0, Math.Max(0, competitors.Count - _maxRows));
        var end = Math.Min(competitors.Count, start + _maxRows);
        for (var i = start; i < end; i++)
        {
            var car = competitors[i];
            var isPlayer = i == playerIndex;

            double? deltaMeters = null;
            if (!isPlayer
                && trackMeters > 0
                && playerHasLaps
                && playerSplineValid
                && car.LapCount is { } laps
                && car.SplinePosition is >= 0f and <= 1f)
            {
                var carCum = laps + car.SplinePosition;

                // Wrap to the nearest equivalent lap position: a car a full lap ahead
                // beside you is a proximity of ~0 m, not a whole lap of metres. This
                // also structurally bounds every displayed gap to half a lap.
                var deltaLaps = WrapToHalfLap(carCum - playerCum);
                deltaMeters = deltaLaps * trackMeters;
            }

            var gapSeconds = isPlayer ? 0d : GapSecondsFor(car, deltaMeters, playerProgressMeters, trackMeters, profile);

            Rows.Add(new RelativeRow
            {
                CarId = car.CarIndex,
                IsPlayer = isPlayer,
                Position = car.Position ?? i + 1,
                CarNumberText = car.RaceNumber is { } raceNumber and > 0 ? "#" + raceNumber : string.Empty,
                Name = DisplayName(car),
                CarClassText = Display.CarClassCode(car.CarClass),
                DriverCategoryText = Display.DriverCategoryCode(car.DriverCategory),
                GapSeconds = gapSeconds,
                GapText = Display.RelativeGapSeconds(gapSeconds)
            });
        }

        return true;
    }

    /// <summary>
    /// Signed time gap for one car, in seconds with the panel's convention (behind is
    /// positive, ahead is negative). Preference order: the source's genuine time gap
    /// (the test feed reports one, real ACC does not), then an estimate that integrates
    /// the player's recent pace over the wrapped distance, then null → "--".
    /// </summary>
    internal static double? GapSecondsFor(
        Competitor car, double? deltaMeters, double playerProgressMeters, double trackMeters, TrackPaceProfile? profile)
    {
        // Genuine source value: trusted within a generous plausibility bound so a
        // corrupt feed shows "--" instead of a wild number.
        if (car.RelativeTimeGapMs is { } reportedMs
            && reportedMs is >= -MaxPlausibleGapMs and <= MaxPlausibleGapMs)
        {
            return reportedMs / 1000d;
        }

        return EstimateGapSeconds(profile, deltaMeters, playerProgressMeters, trackMeters);
    }

    /// <summary>
    /// Converts a wrapped distance gap (positive = further along the lap than the
    /// reference point) into a signed time gap using the pace profile: the duration
    /// comes back non-negative from the integral, and the sign follows the panel's
    /// convention (ahead → negative). Null when there is no usable profile or the
    /// source data was already rejected upstream.
    /// </summary>
    internal static double? EstimateGapSeconds(
        TrackPaceProfile? profile, double? deltaMeters, double fromProgressMeters, double lapMeters)
    {
        if (profile is null || deltaMeters is not { } metres) return null;

        var duration = profile.TimeDeltaSeconds(fromProgressMeters, metres, lapMeters > 0 ? lapMeters : null);
        if (duration is not { } seconds) return null;

        if (metres > 0) return -seconds;
        if (metres < 0) return seconds;
        return 0d;
    }

    /// <summary>
    /// Fallback when broadcasting is off: project each car onto the player's recent
    /// driven line, take the cars nearest the player and show signed time gaps to them
    /// via the same pace profile (with no broadcast track length to wrap against).
    /// Names are the explicit "CAR nn" placeholders; race numbers and cup classes are
    /// unknown so those cells stay empty.
    /// </summary>
    private void BuildFromProjection(TelemetrySample sample, TrackPaceProfile? profile)
    {
        var cars = sample.CarsOnTrack;
        if (cars is not { Count: > 0 })
        {
            return;
        }

        var line = TrackLineProjection.BuildLine(Telemetry.Recent(LinePoints));
        if (line.Count < 2)
        {
            return;
        }

        var playerId = sample.PlayerCarId;
        var playerX = sample.PositionXMeters;
        var playerZ = sample.PositionZMeters;

        var entries = new List<(int CarId, bool IsPlayer, double? Progress)>(cars.Count + 1);
        foreach (var car in cars)
        {
            var isPlayer = playerId is { } id
                ? car.CarId == id
                : playerX is { } bx && playerZ is { } bz
                    && Math.Abs(car.XMeters - bx) < 4f && Math.Abs(car.ZMeters - bz) < 4f;
            entries.Add((car.CarId, isPlayer, TrackLineProjection.ProjectToLine(car.XMeters, car.ZMeters, line)));
        }

        double? playerProgress = null;
        foreach (var entry in entries)
        {
            if (entry.IsPlayer) { playerProgress = entry.Progress; break; }
        }

        if (playerProgress is null && playerX is { } px && playerZ is { } pz)
        {
            playerProgress = sample.TrackProgressMeters ?? TrackLineProjection.ProjectToLine(px, pz, line);
            entries.Add((playerId ?? 0, true, playerProgress));
        }

        if (playerProgress is not { } playerAt)
        {
            return;
        }

        // Window: the cars nearest the player along the line, ahead of the player first.
        var window = entries
            .Where(e => e.Progress is not null)
            .OrderBy(e => Math.Abs(e.Progress!.Value - playerAt))
            .Take(_maxRows)
            .OrderByDescending(e => e.Progress!.Value - playerAt)
            .ToArray();

        for (var i = 0; i < window.Length; i++)
        {
            var entry = window[i];
            double? deltaMeters = entry.Progress is { } progress ? progress - playerAt : null;
            var gapSeconds = entry.IsPlayer
                ? 0d
                : EstimateGapSeconds(profile, deltaMeters, playerAt, lapMeters: 0d);
            Rows.Add(new RelativeRow
            {
                CarId = entry.CarId,
                IsPlayer = entry.IsPlayer,
                Position = i + 1,
                Name = $"CAR {entry.CarId:00}",
                GapSeconds = gapSeconds,
                GapText = Display.RelativeGapSeconds(gapSeconds)
            });
        }
    }

    /// <summary>Track length is trusted only within a plausible band; anything else
    /// makes every gap "--" instead of a fabricated or absurd number.</summary>
    public static bool IsPlausibleTrackLength(int? meters) =>
        meters is { } m && m > 0 && m <= MaxPlausibleTrackMeters;

    /// <summary>Wraps a lap-count difference into (-0.5, +0.5] laps, i.e. the nearest
    /// equivalent position on the lap.</summary>
    internal static double WrapToHalfLap(double deltaLaps) =>
        deltaLaps - Math.Floor(deltaLaps + 0.5);

    /// <summary>Best available name: full broadcast name first (the primary element
    /// the eye lands on), then short name, then the explicit placeholder.</summary>
    internal static string DisplayName(Competitor car) =>
        !string.IsNullOrWhiteSpace(car.DriverFullName) ? car.DriverFullName!
        : !string.IsNullOrWhiteSpace(car.DriverShortName) ? car.DriverShortName!
        : $"CAR {car.CarIndex:00}";
}
