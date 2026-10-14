using System.Windows.Threading;
using AccEngineer.Telemetry;
using AccEngineer.UI.Mvvm;

namespace AccEngineer.UI.Overlay;

/// <summary>
/// Base for overlay panel VMs that poll the telemetry source at a fixed cadence.
/// Handles disposal of the timer and empty-state population.
/// </summary>
public abstract class OverlayPanelViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    private string? _lastSessionId;

    protected OverlayPanelViewModel(IOverlayTelemetrySource telemetry, int refreshMs = 50)
    {
        Telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(refreshMs) };
        _timer.Tick += (_, _) => Tick(Telemetry.Latest);
        _timer.Start();
        Tick(Telemetry.Latest);
    }

    private void Tick(TelemetrySample? sample)
    {
        var sessionId = sample?.SessionId;
        if (sessionId is { Length: > 0 } && !string.Equals(sessionId, _lastSessionId, StringComparison.Ordinal))
        {
            _lastSessionId = sessionId;
            OnSessionChanged();
        }
        else if (sessionId is null)
        {
            _lastSessionId = null;
        }

        Refresh(sample);
    }

    /// <summary>The telemetry source this panel reads from (also exposes recent samples).</summary>
    protected IOverlayTelemetrySource Telemetry { get; }

    /// <summary>Called on each tick with the latest sample. The sample may be null.</summary>
    protected abstract void Refresh(AccEngineer.Telemetry.TelemetrySample? sample);

    /// <summary>Invoked when the telemetry session identity changes; panels may clear transient state.</summary>
    protected virtual void OnSessionChanged() { }

    /// <summary>
    /// Applies layout-wide appearance settings (trace length, row/column counts, ...).
    /// Called when the layout is (re)applied and when the editor changes a setting.
    /// The default is a no-op; panels that honour a setting override this.
    /// </summary>
    public virtual void ApplyAppearance(OverlayAppearance appearance) { }

    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
    }
}
