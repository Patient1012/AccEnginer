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

    public string PositionText { get; private set; } = Display.Missing;

    public string ActiveCarsText { get; private set; } = Display.Missing;

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
        SessionStateText = sample.SessionState.ToString();
        LapText = sample.LapNumber.ToString();
        LapValid = sample.IsLapValid && !sample.Quality.HasFlag(DataQualityFlags.InvalidLap);
        LapTimeText = Display.LapTimeMs(sample.CurrentLapTimeMs);
        LastLapText = Display.LapTimeMs(sample.LastLapTimeMs);
        BestLapText = Display.LapTimeMs(sample.BestLapTimeMs);
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

        BuildTraces();
        BuildMap(sample);
        BuildStandings(sample);
        OnAllPropertiesChanged();
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
        SessionStateText = Display.Missing;
        LapText = Display.Missing;
        LapTimeText = Display.Missing;
        LastLapText = Display.Missing;
        BestLapText = Display.Missing;
        SpeedText = Display.Missing;
        GearText = Display.Missing;
        RpmText = Display.Missing;
        ThrottleText = Display.Missing;
        BrakeText = Display.Missing;
        SteeringText = Display.Missing;
        FuelText = Display.Missing;
        DistanceText = Display.Missing;
        TyreCompoundText = Display.Missing;
        PitText = Display.Missing;
        PressureFl = PressureFr = PressureRl = PressureRr = Display.Missing;
        TempFl = TempFr = TempRl = TempRr = Display.Missing;
        SpeedTrace = ThrottleTrace = BrakeTrace = Array.Empty<double>();
        MapPoints = MapPath = Array.Empty<MapPoint>();
        Cars = Array.Empty<CarRow>();
        HasMap = HasCars = false;
        MapNote = string.Empty;
        PositionText = ActiveCarsText = Display.Missing;
        ThrottlePercent = BrakePercent = 0;
        OnAllPropertiesChanged();
    }

    private static string At(float[]? values, int index) =>
        values is not null && index < values.Length ? Display.Pressure(values[index]) : Display.Missing;

    public static int TraceWindowSeconds => TraceSeconds;
}
