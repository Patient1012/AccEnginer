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
}
