using AccEngineer.Telemetry;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>One corner of the car in the tyres panel. Immutable; rebuilt each tick.</summary>
public sealed class TyreWheel
{
    public string Label { get; init; } = "--";

    public string TempText { get; init; } = "--";

    public string PressureText { get; init; } = "--";

    public string BrakeText { get; init; } = "--";
}

/// <summary>
/// Four-wheel tyre panel: per-corner core temperature, pressure and brake
/// temperature (order FL, FR, RL, RR, matching the ACC shared-memory arrays). Values
/// are shown exactly as reported with their units; missing channels render as "--".
/// </summary>
public sealed class TyresPanelViewModel : OverlayPanelViewModel
{
    public TyresPanelViewModel(IOverlayTelemetrySource telemetry) : base(telemetry, 100) { }

    public bool IsLive { get; private set; }

    public string StatusText { get; private set; } = "NO SIGNAL";

    public string HeaderText { get; private set; } = "TYRES";

    /// <summary>Tyre compound name when reported, otherwise blank.</summary>
    public string CompoundText { get; private set; } = string.Empty;

    public TyreWheel FrontLeft { get; private set; } = new() { Label = "FL" };
    public TyreWheel FrontRight { get; private set; } = new() { Label = "FR" };
    public TyreWheel RearLeft { get; private set; } = new() { Label = "RL" };
    public TyreWheel RearRight { get; private set; } = new() { Label = "RR" };

    protected override void Refresh(TelemetrySample? sample)
    {
        if (sample is null)
        {
            IsLive = false;
            StatusText = "NO SIGNAL";
            CompoundText = string.Empty;
            FrontLeft = new TyreWheel { Label = "FL" };
            FrontRight = new TyreWheel { Label = "FR" };
            RearLeft = new TyreWheel { Label = "RL" };
            RearRight = new TyreWheel { Label = "RR" };
            OnAllPropertiesChanged();
            return;
        }

        IsLive = true;
        StatusText = "LIVE";
        CompoundText = string.IsNullOrWhiteSpace(sample.TyreCompound) ? string.Empty : sample.TyreCompound;

        FrontLeft = Wheel(sample, 0, "FL");
        FrontRight = Wheel(sample, 1, "FR");
        RearLeft = Wheel(sample, 2, "RL");
        RearRight = Wheel(sample, 3, "RR");

        OnAllPropertiesChanged();
    }

    private static TyreWheel Wheel(TelemetrySample sample, int index, string label) => new()
    {
        Label = label,
        TempText = Temperature(At(sample.TyreCoreTempC, index)),
        PressureText = Pressure(At(sample.WheelPressure, index)),
        BrakeText = Temperature(At(sample.BrakeTempC, index))
    };

    private static float? At(float[]? values, int index) =>
        values != null && index >= 0 && index < values.Length ? values[index] : null;

    private static string Temperature(float? celsius) =>
        celsius is null ? "--" : Display.Degrees(celsius) + " °C";

    private static string Pressure(float? value) =>
        value is null ? "--" : Display.Pressure(value);
}
