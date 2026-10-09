using AccEngineer.Telemetry;
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
        OnAllPropertiesChanged();
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
        OnAllPropertiesChanged();
    }

    private static string At(float[]? values, int index) =>
        values is not null && index < values.Length ? Display.Pressure(values[index]) : Display.Missing;

    public static int TraceWindowSeconds => TraceSeconds;
}
