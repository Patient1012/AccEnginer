using AccEngineer.Telemetry;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// Inputs / gear panel: gear, speed plus throttle/brake/clutch inputs and steering.
/// Each channel degrades independently - a missing channel shows "--" and its trace
/// simply stops, while the others keep rendering. Input traces are bounded rolling
/// buffers (no unbounded growth) rebuilt at the poll cadence.
/// </summary>
public sealed class InputsPanelViewModel : OverlayPanelViewModel
{
    private readonly Queue<double> _throttle = new();
    private readonly Queue<double> _brake = new();
    private readonly Queue<double> _steering = new();

    /// <summary>Number of samples retained per input trace (~6 s at 50 ms by default).</summary>
    private int _traceLength = 120;

    public InputsPanelViewModel(IOverlayTelemetrySource telemetry) : base(telemetry, 50) { }

    /// <inheritdoc />
    public override void ApplyAppearance(OverlayAppearance appearance)
    {
        _traceLength = Math.Clamp(appearance.TraceLength, 30, 400);
        Trim(_throttle);
        Trim(_brake);
        Trim(_steering);
        ThrottleTrace = _throttle.ToArray();
        BrakeTrace = _brake.ToArray();
        SteeringTrace = _steering.ToArray();
        OnAllPropertiesChanged();
    }

    public bool IsLive { get; private set; }

    public string StatusText { get; private set; } = "NO SIGNAL";

    public string GearText { get; private set; } = "--";

    public string SpeedText { get; private set; } = "--";

    public string RpmText { get; private set; } = "--";

    public string ThrottleText { get; private set; } = "--";

    public string BrakeText { get; private set; } = "--";

    public string ClutchText { get; private set; } = "--";

    public string SteeringText { get; private set; } = "--";

    /// <summary>Throttle fill 0..1 for the bar (0 when unavailable).</summary>
    public double ThrottleBar { get; private set; }

    /// <summary>Brake fill 0..1 for the bar (0 when unavailable).</summary>
    public double BrakeBar { get; private set; }

    public double ClutchBar { get; private set; }

    /// <summary>Steering magnitude 0..1 for the bar (0 when unavailable).</summary>
    public double SteeringBar { get; private set; }

    /// <summary>Signed steering input -1..1 (left negative, right positive; 0 when unavailable).</summary>
    public double SteeringAngle { get; private set; }

    public IReadOnlyList<double> ThrottleTrace { get; private set; } = Array.Empty<double>();

    public IReadOnlyList<double> BrakeTrace { get; private set; } = Array.Empty<double>();

    public IReadOnlyList<double> SteeringTrace { get; private set; } = Array.Empty<double>();

    protected override void Refresh(TelemetrySample? sample)
    {
        if (sample is null)
        {
            IsLive = false;
            StatusText = "NO SIGNAL";
            GearText = "--";
            SpeedText = "--";
            RpmText = "--";
            ThrottleText = "--";
            BrakeText = "--";
            ClutchText = "--";
            SteeringText = "--";
            ThrottleBar = 0;
            BrakeBar = 0;
            ClutchBar = 0;
            SteeringBar = 0;
            SteeringAngle = 0;

            // Drop history on signal loss so we never draw a stale line as if live.
            _throttle.Clear();
            _brake.Clear();
            _steering.Clear();
            ThrottleTrace = Array.Empty<double>();
            BrakeTrace = Array.Empty<double>();
            SteeringTrace = Array.Empty<double>();

            OnAllPropertiesChanged();
            return;
        }

        IsLive = true;
        StatusText = "LIVE";
        GearText = Display.Gear(sample.Gear);
        SpeedText = Display.Kmh(sample.SpeedKph);
        RpmText = Display.Rpm(sample.EngineRpm);

        if (sample.ClutchInput is { } clutch)
        {
            ClutchText = Display.Percent(clutch);
            ClutchBar = Math.Clamp(clutch, 0, 1);
        }
        else
        {
            ClutchText = "--";
            ClutchBar = 0;
        }

        if (sample.ThrottleInput is { } throttle)
        {
            ThrottleText = Display.Percent(throttle);
            ThrottleBar = Math.Clamp(throttle, 0, 1);
            Push(_throttle, throttle);
            Trim(_throttle);
        }
        else
        {
            ThrottleText = "--";
            ThrottleBar = 0;
        }

        if (sample.BrakeInput is { } brake)
        {
            BrakeText = Display.Percent(brake);
            BrakeBar = Math.Clamp(brake, 0, 1);
            Push(_brake, brake);
            Trim(_brake);
        }
        else
        {
            BrakeText = "--";
            BrakeBar = 0;
        }

        if (sample.SteeringInput is { } steering)
        {
            SteeringText = Display.Steering(steering);
            SteeringBar = Math.Clamp(Math.Abs(steering), 0, 1);
            SteeringAngle = Math.Clamp(steering, -1, 1);
            Push(_steering, steering);
            Trim(_steering);
        }
        else
        {
            SteeringText = "--";
            SteeringBar = 0;
            SteeringAngle = 0;
        }

        ThrottleTrace = _throttle.ToArray();
        BrakeTrace = _brake.ToArray();
        SteeringTrace = _steering.ToArray();

        OnAllPropertiesChanged();
    }

    private static void Push(Queue<double> queue, double value) => queue.Enqueue(value);

    private void Trim(Queue<double> queue)
    {
        while (queue.Count > _traceLength) queue.Dequeue();
    }
}
