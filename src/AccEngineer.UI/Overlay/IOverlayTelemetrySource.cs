using AccEngineer.Telemetry;

namespace AccEngineer.UI.Overlay;

/// <summary>
/// Abstraction over where overlay panels read their data from. Panels poll
/// <see cref="Latest"/> at their own cadence so this stays testable and lets us
/// feed synthetic data in demo mode.
/// </summary>
public interface IOverlayTelemetrySource
{
    TelemetrySample? Latest { get; }
    /// <summary>Recent samples for projection/trace purposes (may be empty if unsupported).</summary>
    IReadOnlyList<TelemetrySample> Recent(int maxCount);

    /// <summary>
    /// True when the source is the built-in generated test provider rather than a
    /// live simulator session, so panels can show a clear test-data indicator.
    /// </summary>
    bool IsTestData { get; }
}

/// <summary>
/// Adapts the existing <see cref="Services.LiveTelemetryService"/> to the overlay
/// source contract.
/// </summary>
public sealed class LiveOverlayTelemetrySource : IOverlayTelemetrySource
{
    private readonly Services.LiveTelemetryService _telemetry;

    public LiveOverlayTelemetrySource(Services.LiveTelemetryService telemetry)
    {
        _telemetry = telemetry;
    }

    public TelemetrySample? Latest => _telemetry.Latest;
    public IReadOnlyList<TelemetrySample> Recent(int maxCount) => _telemetry.SnapshotRecent(maxCount);
    public bool IsTestData => _telemetry.IsTestData;
}
