using AccEngineer.Telemetry;
using AccEngineer.UI.Controls;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.ViewModels;

/// <summary>
/// Live, read-only presentation of the newest sample plus short traces. Shared by
/// the Overview and Live Data pages so both stay consistent. Values are refreshed
/// by the shell pump, not by per-sample events.
/// </summary>
public sealed class LiveDataViewModel : PageViewModel
{
    private const int TracePoints = 240;
    private const int TraceSeconds = 12;
    private const int MapPathPoints = 900;

    private readonly LiveTelemetryService _telemetry;

    public LiveDataViewModel(LiveTelemetryService telemetry)
        : base("Live Data") =>
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));

    public bool HasData { get; private set; }

    /// <summary>"LIVE" when a sample is present, "NO SIGNAL" otherwise.</summary>
    public string LiveStatusText { get; private set; } = "NO SIGNAL";

    /// <summary>Explains what the page is showing while waiting for data.</summary>
    public string StatusHintText { get; private set; } =
        "Waiting for ACC shared memory or a replay. The pit wall below shows placeholders (—) until data arrives.";

    public string SessionStateText { get; private set; } = Display.Missing;

    public string LapText { get; private set; } = Display.Missing;

    public string LapTimeText { get; private set; } = Display.Missing;

    public string LastLapText { get; private set; } = Display.Missing;

    public string BestLapText { get; private set; } = Display.Missing;

    public bool LapValid { get; private set; } = true;

    public string SpeedText { get; private set; } = Display.Missing;

    public string GearText { get; private set; } = Display.Missing;

    public string RpmText { get; private set; } = Display.Missing;

    public string ThrottleText { get; private set; } = Display.Missing;

    public string BrakeText { get; private set; } = Display.Missing;

    public string SteeringText { get; private set; } = Display.Missing;

    public string FuelText { get; private set; } = Display.Missing;

    public string DistanceText { get; private set; } = Display.Missing;

    public string TyreCompoundText { get; private set; } = Display.Missing;

    public string PitText { get; private set; } = Display.Missing;

    public string PressureFl { get; private set; } = Display.Missing;

    public string PressureFr { get; private set; } = Display.Missing;

    public string PressureRl { get; private set; } = Display.Missing;

    public string PressureRr { get; private set; } = Display.Missing;

    public string TempFl { get; private set; } = Display.Missing;

    public string TempFr { get; private set; } = Display.Missing;

    public string TempRl { get; private set; } = Display.Missing;

    public string TempRr { get; private set; } = Display.Missing;

    public double[] SpeedTrace { get; private set; } = Array.Empty<double>();

    public double[] ThrottleTrace { get; private set; } = Array.Empty<double>();

    public double[] BrakeTrace { get; private set; } = Array.Empty<double>();

    public double SpeedTraceMax { get; private set; } = 300;

    public IReadOnlyList<MapPoint> MapPoints { get; private set; } = Array.Empty<MapPoint>();

    public IReadOnlyList<MapPoint> MapPath { get; private set; } = Array.Empty<MapPoint>();

    public bool HasMap { get; private set; }

    public string MapNote { get; private set; } = string.Empty;

    public bool HasMapNote => !string.IsNullOrEmpty(MapNote);

    public IReadOnlyList<CarRow> Cars { get; private set; } = Array.Empty<CarRow>();

    public bool HasCars { get; private set; }

    public IReadOnlyList<LeaderboardRowViewModel> Leaderboard { get; private set; } = Array.Empty<LeaderboardRowViewModel>();

    public bool HasLeaderboard { get; private set; }

    /// <summary>Length of the position window the leaderboard estimate uses.</summary>
    public string LeaderboardWindowText { get; private set; } = Display.Missing;

    /// <summary>ACC-reported player position, e.g. "ACC P7" or "ACC P7 / 24".</summary>
    public string LeaderboardAccPositionText { get; private set; } = Display.Missing;

    public string SessionTypeText { get; private set; } = Display.Missing;

    public string TrackText { get; private set; } = Display.Missing;

    public string CarText { get; private set; } = Display.Missing;

    public string LapValidText { get; private set; } = Display.Missing;

    public string DeltaToBestText { get; private set; } = Display.Missing;

    public bool IsAheadOfBest { get; private set; }

    public string PositionText { get; private set; } = Display.Missing;

    public string ActiveCarsText { get; private set; } = Display.Missing;

    public string BrakeTempFl { get; private set; } = Display.Missing;

    public string BrakeTempFr { get; private set; } = Display.Missing;

    public string BrakeTempRl { get; private set; } = Display.Missing;

    public string BrakeTempRr { get; private set; } = Display.Missing;

    public string TyreWearFl { get; private set; } = Display.Missing;

    public string TyreWearFr { get; private set; } = Display.Missing;

    public string TyreWearRl { get; private set; } = Display.Missing;

    public string TyreWearRr { get; private set; } = Display.Missing;

    public string SuspensionFl { get; private set; } = Display.Missing;

    public string SuspensionFr { get; private set; } = Display.Missing;

    public string SuspensionRl { get; private set; } = Display.Missing;

    public string SuspensionRr { get; private set; } = Display.Missing;

    public string LateralAccelerationText { get; private set; } = Display.Missing;

    public string LongitudinalAccelerationText { get; private set; } = Display.Missing;

    public double ThrottlePercent { get; private set; }

    public double BrakePercent { get; private set; }

    public override void Refresh()
    {
        var sample = _telemetry.Latest;
        if (sample is null)
        {
            if (HasData) Clear();
            return;
        }

        HasData = true;
        LiveStatusText = "LIVE";
        StatusHintText = "Live feed from ACC shared memory.";
        SessionStateText = sample.SessionState.ToString();
        SessionTypeText = sample.SessionType.ToString();
        TrackText = sample.TrackName ?? Display.Missing;
        CarText = sample.CarName ?? Display.Missing;
        LapText = sample.LapNumber.ToString();
        LapValid = sample.IsLapValid && !sample.Quality.HasFlag(DataQualityFlags.InvalidLap);
        LapValidText = sample.Quality.HasFlag(DataQualityFlags.InvalidLap) ? "INVALID" : sample.IsLapValid ? "VALID" : "INVALID";
        LapTimeText = Display.LapTimeMs(sample.CurrentLapTimeMs);
        LastLapText = Display.LapTimeMs(sample.LastLapTimeMs);
        BestLapText = Display.LapTimeMs(sample.BestLapTimeMs);
        DeltaToBestText = DeltaToBest(sample);
        SpeedText = Display.Kmh(sample.SpeedKph);
        GearText = Display.Gear(sample.Gear);
        RpmText = Display.Rpm(sample.EngineRpm);
        ThrottleText = Display.Percent(sample.ThrottleInput);
        BrakeText = Display.Percent(sample.BrakeInput);
        ThrottlePercent = sample.ThrottleInput is { } throttle ? Math.Clamp(throttle, 0, 1) * 100 : 0;
        BrakePercent = sample.BrakeInput is { } brake ? Math.Clamp(brake, 0, 1) * 100 : 0;
        SteeringText = Display.Steering(sample.SteeringInput);
        FuelText = Display.Fuel(sample.FuelLevel);
        DistanceText = Display.Metres(sample.TrackProgressMeters);
        LateralAccelerationText = Display.Number(sample.LateralAccelerationG);
        LongitudinalAccelerationText = Display.Number(sample.LongitudinalAccelerationG);
        TyreCompoundText = sample.TyreCompound ?? Display.Missing;
        PitText = sample.IsInPitLane is true || sample.PitLimiterOn is true ? "IN PIT" : "ON TRACK";

        var pressures = sample.WheelPressure;
        PressureFl = At(pressures, 0);
        PressureFr = At(pressures, 1);
        PressureRl = At(pressures, 2);
        PressureRr = At(pressures, 3);

        var temps = sample.TyreCoreTempC;
        TempFl = At(temps, 0);
        TempFr = At(temps, 1);
        TempRl = At(temps, 2);
        TempRr = At(temps, 3);

        var brakeTemps = sample.BrakeTempC;
        BrakeTempFl = At(brakeTemps, 0);
        BrakeTempFr = At(brakeTemps, 1);
        BrakeTempRl = At(brakeTemps, 2);
        BrakeTempRr = At(brakeTemps, 3);

        var wear = sample.TyreWear;
        TyreWearFl = WearAt(wear, 0);
        TyreWearFr = WearAt(wear, 1);
        TyreWearRl = WearAt(wear, 2);
        TyreWearRr = WearAt(wear, 3);

        var suspension = sample.SuspensionTravelMeters;
        SuspensionFl = TravelAt(suspension, 0);
        SuspensionFr = TravelAt(suspension, 1);
        SuspensionRl = TravelAt(suspension, 2);
        SuspensionRr = TravelAt(suspension, 3);

        BuildTraces();
        BuildMap(sample);
        BuildStandings(sample);
        BuildLeaderboard(sample);
        OnAllPropertiesChanged();
    }

    private string DeltaToBest(TelemetrySample sample)
    {
        if (sample.CurrentLapTimeMs is not { } current || sample.BestLapTimeMs is not { } best)
        {
            IsAheadOfBest = false;
            return Display.Missing;
        }

        var deltaSeconds = (current - best) / 1000.0;
        IsAheadOfBest = deltaSeconds < 0;
        return Display.Delta(deltaSeconds);
    }

    private void BuildMap(TelemetrySample sample)
    {
        var markers = new List<MapPoint>();
        var cars = sample.CarsOnTrack;
        if (cars is { Count: > 0 })
        {
            var px = sample.PositionXMeters;
            var pz = sample.PositionZMeters;
            foreach (var car in cars)
            {
                var isPlayer = px is { } x && pz is { } z
                    && Math.Abs(car.XMeters - x) < 4f
                    && Math.Abs(car.ZMeters - z) < 4f;
                markers.Add(new MapPoint(car.XMeters, car.ZMeters, isPlayer));
            }
        }

        var recent = _telemetry.SnapshotRecent(MapPathPoints);
        var path = new List<MapPoint>(recent.Count);
        foreach (var s in recent)
        {
            if (s.PositionXMeters is { } x && s.PositionZMeters is { } z)
            {
                path.Add(new MapPoint(x, z, false));
            }
        }

        MapPoints = markers;
        MapPath = path;
        HasMap = markers.Count > 0 || path.Count >= 2;
        MapNote = markers.Count > 0
            ? string.Empty
            : path.Count >= 2
                ? "Showing the player's path only. Other cars are not available in this session (replay, or no other cars on track)."
                : string.Empty;
    }

    private void BuildStandings(TelemetrySample sample)
    {
        PositionText = sample.PlayerPosition is { } position
            ? sample.ActiveCars is { } count ? $"{position} / {count}" : position.ToString()
            : Display.Missing;
        ActiveCarsText = sample.ActiveCars?.ToString() ?? Display.Missing;

        var cars = sample.CarsOnTrack;
        if (cars is not { Count: > 0 })
        {
            Cars = Array.Empty<CarRow>();
            HasCars = false;
            return;
        }

        var px = sample.PositionXMeters;
        var pz = sample.PositionZMeters;
        var rows = new List<CarRow>(cars.Count);
        var slot = 0;
        foreach (var car in cars)
        {
            double? distance = px is { } x && pz is { } z
                ? Math.Sqrt(((car.XMeters - x) * (car.XMeters - x)) + ((car.ZMeters - z) * (car.ZMeters - z)))
                : null;
            var isPlayer = distance is { } d && d < 4;
            rows.Add(new CarRow(car.CarId, slot, isPlayer, distance));
            slot++;
        }

        Cars = rows
            .OrderByDescending(r => r.IsPlayer)
            .ThenBy(r => r.DistanceMeters ?? double.MaxValue)
            .ToArray();
        HasCars = true;
    }

    /// <summary>
    /// Builds the race timing tower. ACC exposes no opponent timing or names, so
    /// each car's world position is projected onto the player's recent line and the
    /// rows are ordered by the track progress of the nearest line point. Gaps are
    /// metre estimates and are presented as such, never as official times.
    /// </summary>
    private void BuildLeaderboard(TelemetrySample sample)
    {
        LeaderboardAccPositionText = sample.PlayerPosition is { } position
            ? sample.ActiveCars is { } count
                ? $"ACC P{position} / {count}"
                : $"ACC P{position}"
            : Display.Missing;

        var cars = sample.CarsOnTrack;
        if (cars is not { Count: > 0 })
        {
            Leaderboard = Array.Empty<LeaderboardRowViewModel>();
            HasLeaderboard = false;
            LeaderboardWindowText = Display.Missing;
            return;
        }

        var recent = _telemetry.SnapshotRecent(MapPathPoints);
        var line = TrackLineProjection.BuildLine(recent);

        if (line.Count < 2)
        {
            Leaderboard = Array.Empty<LeaderboardRowViewModel>();
            HasLeaderboard = false;
            LeaderboardWindowText = Display.Missing;
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
                : playerX is { } byX && playerZ is { } byZ
                    && Math.Abs(car.XMeters - byX) < 4f && Math.Abs(car.ZMeters - byZ) < 4f;
            if (isPlayer) hasPlayer = true;
            entries.Add((car.CarId, isPlayer, TrackLineProjection.ProjectToLine(car.XMeters, car.ZMeters, line)));
        }

        if (!hasPlayer && playerX is { } x && playerZ is { } z)
        {
            entries.Add((playerId ?? 0, true, sample.TrackProgressMeters is { } p ? p : TrackLineProjection.ProjectToLine(x, z, line)));
        }

        var sorted = entries
            .OrderBy(e => e.Progress ?? double.MaxValue)
            .ToArray();

        var rows = new List<LeaderboardRowViewModel>(sorted.Length);
        double? leader = sorted.Length > 0 ? sorted[0].Progress : null;
        for (var i = 0; i < sorted.Length; i++)
        {
            var entry = sorted[i];
            double? gap = null;
            double? interval = null;
            if (entry.Progress is { } progress)
            {
                if (leader is { } lead) gap = Math.Max(0, progress - lead);
                if (i > 0 && sorted[i - 1].Progress is { } previous) interval = Math.Max(0, progress - previous);
            }

            rows.Add(new LeaderboardRowViewModel(i + 1, entry.CarId, entry.IsPlayer, gap, interval));
        }

        Leaderboard = rows;
        HasLeaderboard = true;
        LeaderboardWindowText = $"last {MapPathPoints} positions";
    }

    private void BuildTraces()
    {
        var recent = _telemetry.SnapshotRecent(TracePoints);
        var speed = new double[recent.Count];
        var throttle = new double[recent.Count];
        var brake = new double[recent.Count];
        var max = 100.0;

        for (var i = 0; i < recent.Count; i++)
        {
            var sample = recent[i];
            speed[i] = sample.SpeedKph ?? 0;
            throttle[i] = sample.ThrottleInput is { } t ? Math.Clamp(t, 0, 1) : 0;
            brake[i] = sample.BrakeInput is { } b ? Math.Clamp(b, 0, 1) : 0;
            if (speed[i] > max) max = speed[i];
        }

        SpeedTrace = speed;
        ThrottleTrace = throttle;
        BrakeTrace = brake;
        SpeedTraceMax = Math.Max(10, Math.Ceiling(max / 50.0) * 50.0);
    }

    private void Clear()
    {
        HasData = false;
        LiveStatusText = "NO SIGNAL";
        StatusHintText = "Waiting for ACC shared memory or a replay. The pit wall below shows placeholders (—) until data arrives.";
        SessionStateText = Display.Missing;
        SessionTypeText = Display.Missing;
        TrackText = Display.Missing;
        CarText = Display.Missing;
        LapText = Display.Missing;
        LapValidText = Display.Missing;
        LapTimeText = Display.Missing;
        LastLapText = Display.Missing;
        BestLapText = Display.Missing;
        DeltaToBestText = Display.Missing;
        IsAheadOfBest = false;
        SpeedText = Display.Missing;
        GearText = Display.Missing;
        RpmText = Display.Missing;
        ThrottleText = Display.Missing;
        BrakeText = Display.Missing;
        SteeringText = Display.Missing;
        FuelText = Display.Missing;
        DistanceText = Display.Missing;
        LateralAccelerationText = Display.Missing;
        LongitudinalAccelerationText = Display.Missing;
        TyreCompoundText = Display.Missing;
        PitText = Display.Missing;
        PressureFl = PressureFr = PressureRl = PressureRr = Display.Missing;
        TempFl = TempFr = TempRl = TempRr = Display.Missing;
        BrakeTempFl = BrakeTempFr = BrakeTempRl = BrakeTempRr = Display.Missing;
        TyreWearFl = TyreWearFr = TyreWearRl = TyreWearRr = Display.Missing;
        SuspensionFl = SuspensionFr = SuspensionRl = SuspensionRr = Display.Missing;
        SpeedTrace = ThrottleTrace = BrakeTrace = Array.Empty<double>();
        MapPoints = MapPath = Array.Empty<MapPoint>();
        Cars = Array.Empty<CarRow>();
        Leaderboard = Array.Empty<LeaderboardRowViewModel>();
        HasMap = HasCars = HasLeaderboard = false;
        LeaderboardWindowText = Display.Missing;
        LeaderboardAccPositionText = Display.Missing;
        MapNote = string.Empty;
        PositionText = ActiveCarsText = Display.Missing;
        ThrottlePercent = BrakePercent = 0;
        OnAllPropertiesChanged();
    }

    public static int TraceWindowSeconds => TraceSeconds;

    private static string WearAt(float[]? values, int index) =>
        values is not null && index < values.Length ? Display.Percent(values[index]) : Display.Missing;

    private static string TravelAt(float[]? values, int index) =>
        values is not null && index < values.Length ? Display.Millimetres(values[index]) : Display.Missing;

    private static string At(float[]? values, int index) =>
        values is not null && index < values.Length ? Display.Pressure(values[index]) : Display.Missing;
}
