using AccEngineer.Telemetry;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// Inputs / gear panel: gear, speed and RPM plus throttle, brake and steering.
/// Each channel degrades independently - a missing channel shows "--" and its trace
/// simply stops, while the others keep rendering. Input traces are bounded rolling
/// buffers (no unbounded growth) rebuilt at the poll cadence.
/// </summary>
public sealed class InputsPanelViewModel : OverlayPanelViewModel
{
    /// <summary>Number of samples retained per input trace (~6 s at 50 ms).</summary>
    private const int TraceLength = 120;

    private readonly Queue<double> _throttle = new();
    private readonly Queue<double> _brake = new();
    private readonly Queue<double> _steering = new();

    public InputsPanelViewModel(IOverlayTelemetrySource telemetry) : base(telemetry, 50) { }

    public bool IsLive { get; private set; }

    public string StatusText { get; private set; } = "NO SIGNAL";

    public string GearText { get; private set; } = "--";

    public string SpeedText { get; private set; } = "--";

    public string RpmText { get; private set; } = "--";

    public string ThrottleText { get; private set; } = "--";

    public string BrakeText { get; private set; } = "--";

    public string SteeringText { get; private set; } = "--";

    /// <summary>Throttle fill 0..1 for the bar (0 when unavailable).</summary>
    public double ThrottleBar { get; private set; }

    /// <summary>Brake fill 0..1 for the bar (0 when unavailable).</summary>
    public double BrakeBar { get; private set; }

    /// <summary>Steering magnitude 0..1 for the bar (0 when unavailable).</summary>
    public double SteeringBar { get; private set; }

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
            SteeringText = "--";
            ThrottleBar = 0;
            BrakeBar = 0;
            SteeringBar = 0;

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

        if (sample.ThrottleInput is { } throttle)
        {
            ThrottleText = Display.Percent(throttle);
            ThrottleBar = Math.Clamp(throttle, 0, 1);
            Push(_throttle, throttle);
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
            Push(_steering, steering);
        }
        else
        {
            SteeringText = "--";
            SteeringBar = 0;
        }

        ThrottleTrace = _throttle.ToArray();
        BrakeTrace = _brake.ToArray();
        SteeringTrace = _steering.ToArray();

        OnAllPropertiesChanged();
    }

    private static void Push(Queue<double> queue, double value)
    {
        queue.Enqueue(value);
        while (queue.Count > TraceLength) queue.Dequeue();
    }
}
