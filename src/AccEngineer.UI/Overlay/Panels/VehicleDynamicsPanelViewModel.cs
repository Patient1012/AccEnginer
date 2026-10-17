using System.Globalization;
using AccEngineer.Telemetry;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>One corner's suspension travel. Immutable; rebuilt each tick.</summary>
public sealed class SuspensionCorner
{
    public string Label { get; init; } = "--";

    public string TravelText { get; init; } = "--";
}

/// <summary>
/// Vehicle-dynamics panel: speed, lateral/longitudinal acceleration and yaw rate, plus
/// a 2x2 per-corner suspension-travel layout. All values come from the ACC shared-memory
/// physics page; missing channels render as "--". The G-force axis conventions are as
/// documented on <see cref="TelemetrySample"/> and are not re-derived here.
/// </summary>
public sealed class VehicleDynamicsPanelViewModel : OverlayPanelViewModel
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public VehicleDynamicsPanelViewModel(IOverlayTelemetrySource telemetry) : base(telemetry, 50) { }

    public bool IsLive { get; private set; }

    public string StatusText { get; private set; } = "NO SIGNAL";

    public string HeaderText { get; private set; } = "DYNAMICS";

    public string SpeedText { get; private set; } = "--";

    public string LateralGText { get; private set; } = "--";

    public string LongitudinalGText { get; private set; } = "--";

    public string YawText { get; private set; } = "--";

    public SuspensionCorner FrontLeft { get; private set; } = new() { Label = "FL" };
    public SuspensionCorner FrontRight { get; private set; } = new() { Label = "FR" };
    public SuspensionCorner RearLeft { get; private set; } = new() { Label = "RL" };
    public SuspensionCorner RearRight { get; private set; } = new() { Label = "RR" };

    protected override void Refresh(TelemetrySample? sample)
    {
        if (sample is null)
        {
            IsLive = false;
            StatusText = "NO SIGNAL";
            SpeedText = "--";
            LateralGText = "--";
            LongitudinalGText = "--";
            YawText = "--";
            FrontLeft = new SuspensionCorner { Label = "FL" };
            FrontRight = new SuspensionCorner { Label = "FR" };
            RearLeft = new SuspensionCorner { Label = "RL" };
            RearRight = new SuspensionCorner { Label = "RR" };
            OnAllPropertiesChanged();
            return;
        }

        IsLive = true;
        StatusText = "LIVE";
        SpeedText = sample.SpeedKph is { } speed ? Display.Kmh(speed) + " km/h" : "--";
        LateralGText = Signed(sample.LateralAccelerationG, "g");
        LongitudinalGText = Signed(sample.LongitudinalAccelerationG, "g");
        YawText = Signed(sample.AngularVelocityYawRadPerSec, "rad/s");

        FrontLeft = Corner(sample, 0, "FL");
        FrontRight = Corner(sample, 1, "FR");
        RearLeft = Corner(sample, 2, "RL");
        RearRight = Corner(sample, 3, "RR");

        OnAllPropertiesChanged();
    }

    private static SuspensionCorner Corner(TelemetrySample sample, int index, string label) => new()
    {
        Label = label,
        TravelText = sample.SuspensionTravelMeters is { } travel && index < travel.Length
            ? Display.Millimetres(travel[index]) + " mm"
            : "--"
    };

    private static string Signed(float? value, string unit) =>
        value is null ? "--" : value.Value.ToString("+0.00;-0.00;0.00", Inv) + " " + unit;
}
