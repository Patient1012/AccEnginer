using System.Globalization;
using System.Windows.Threading;
using AccEngineer.UI.Mvvm;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Overlay;

/// <summary>
/// Minimal data source for the Phase 1 test overlay. Reads the latest sample from
/// the provided telemetry source at a fixed display cadence. Unavailable values
/// render as "--" and are never invented.
/// </summary>
public sealed class TestPanelViewModel : ObservableObject, IDisposable
{
    private readonly IOverlayTelemetrySource _telemetry;
    private readonly DispatcherTimer _timer;

    public TestPanelViewModel(IOverlayTelemetrySource telemetry)
    {
        _telemetry = telemetry;
        Refresh();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    public bool IsLive { get; private set; }

    public string StatusText { get; private set; } = "--";

    public string SessionText { get; private set; } = "--";

    public string LapText { get; private set; } = "--";

    public string LastLapText { get; private set; } = "--";

    public string BestLapText { get; private set; } = "--";

    public string SpeedText { get; private set; } = "--";

    public string GearText { get; private set; } = "--";

    public string RpmText { get; private set; } = "--";

    public void Dispose() => _timer.Stop();

    private void Refresh()
    {
        var sample = _telemetry.Latest;
        IsLive = sample is not null;
        StatusText = "LIVE";

        if (sample is null)
        {
            SessionText = "--";
            LapText = "--";
            LastLapText = "--";
            BestLapText = "--";
            SpeedText = "--";
            GearText = "--";
            RpmText = "--";
        }
        else
        {
            SessionText = sample.SessionType.ToString();
            LapText = sample.LapNumber.ToString(CultureInfo.InvariantCulture);
            LastLapText = Value(Display.LapTimeMs(sample.LastLapTimeMs));
            BestLapText = Value(Display.LapTimeMs(sample.BestLapTimeMs));
            SpeedText = Value(Display.Kmh(sample.SpeedKph));
            GearText = Value(Display.Gear(sample.Gear));
            RpmText = Value(Display.Rpm(sample.EngineRpm));
        }

        OnAllPropertiesChanged();
    }

    private static string Value(string formatted) =>
        string.IsNullOrEmpty(formatted) || formatted == Display.Missing ? "--" : formatted;
}
