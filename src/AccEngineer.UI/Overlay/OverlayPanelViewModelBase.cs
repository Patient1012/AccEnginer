using System.Windows.Threading;
using AccEngineer.UI.Mvvm;

namespace AccEngineer.UI.Overlay;

/// <summary>
/// Base for overlay panel VMs that poll the telemetry source at a fixed cadence.
/// Handles disposal of the timer and empty-state population.
/// </summary>
public abstract class OverlayPanelViewModel : ObservableObject, IDisposable
{
    private readonly IOverlayTelemetrySource _telemetry;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    protected OverlayPanelViewModel(IOverlayTelemetrySource telemetry, int refreshMs = 50)
    {
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(refreshMs) };
        _timer.Tick += (_, _) => Refresh(_telemetry.Latest);
        _timer.Start();
        Refresh(_telemetry.Latest);
    }

    /// <summary>Called on each tick with the latest sample. The sample may be null.</summary>
    protected abstract void Refresh(AccEngineer.Telemetry.TelemetrySample? sample);

    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
    }
}
