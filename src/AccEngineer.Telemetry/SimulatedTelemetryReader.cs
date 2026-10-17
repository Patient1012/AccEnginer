namespace AccEngineer.Telemetry;

/// <summary>
/// Marker for a <see cref="ITrackTelemetryReader"/> that produces synthetic test
/// data rather than reading a simulator. The UI uses it to reflect the actual
/// provider in effect (for example a "test data" indicator) instead of trusting a
/// saved preference.
/// </summary>
public interface ISimulatedTelemetryReader : ITrackTelemetryReader
{
}

/// <summary>
/// Options for <see cref="SimulatedTelemetryReader"/>. Defaults describe a plausible
/// GT3 practice stint at a ~50 ms update rate, matching the live reader cadence.
/// </summary>
public sealed class SimulatedTelemetryOptions
{
    /// <summary>Minimum wall-clock time between emitted samples.</summary>
    public TimeSpan UpdateInterval { get; init; } = TimeSpan.FromMilliseconds(50);

    /// <summary>Seed for the deterministic waveform, so two readers produce identical output.</summary>
    public int Seed { get; init; } = 20240607;

    /// <summary>Assumed lap length in metres used by the pace/timing model.</summary>
    public double LapLengthMeters { get; init; } = 4300;

    public SessionType SessionType { get; init; } = SessionType.Practice;

    public SessionState SessionState { get; init; } = SessionState.Live;

    /// <summary>Stable session id, so the shared pipeline does not see spurious session changes.</summary>
    public string SessionId { get; init; } = "testdata-session";

    public string TrackName { get; init; } = "Test Data Circuit";

    public string CarName { get; init; } = "Test Data GT3";

    public string TyreCompound { get; init; } = "Test";

    /// <summary>Number of synthetic opponents placed around the same loop.</summary>
    public int OtherCars { get; init; } = 11;

    public double StartingFuelKg { get; init; } = 62;
}

/// <summary>
/// A poll-based <see cref="ITrackTelemetryReader"/> that generates deterministic,
/// self-consistent test telemetry through the same contract as the real reader.
///
/// It emits a new sample once <see cref="SimulatedTelemetryOptions.UpdateInterval"/>
/// of wall-clock time has passed (using the injected <see cref="TimeProvider"/>), and
/// null in between, exactly like the shared-memory reader. No simulator, network or
/// shared memory is involved, and it starts and stops cleanly.
/// </summary>
public sealed class SimulatedTelemetryReader : ISimulatedTelemetryReader
{
    private readonly SimulatedTelemetryOptions _options;
    private readonly TimeProvider _time;
    private readonly TestTelemetrySimulator _simulator;
    private readonly object _gate = new();

    private DateTimeOffset? _lastEmitUtc;
    private long _sampleId;
    private bool _disposed;

    public SimulatedTelemetryReader(SimulatedTelemetryOptions? options = null, TimeProvider? timeProvider = null)
    {
        _options = options ?? new SimulatedTelemetryOptions();
        _time = timeProvider ?? TimeProvider.System;
        _simulator = new TestTelemetrySimulator(_options);
    }

    /// <summary>Always true until disposed: the generator is always "on".</summary>
    public bool IsConnected
    {
        get
        {
            lock (_gate) return !_disposed;
        }
    }

    public TelemetrySample? ReadLatest()
    {
        lock (_gate)
        {
            if (_disposed) return null;

            var now = _time.GetUtcNow();
            if (_lastEmitUtc is { } last && now - last < _options.UpdateInterval) return null;

            _lastEmitUtc = now;
            return _simulator.Advance(_options.UpdateInterval, ++_sampleId, now, now.UtcTicks);
        }
    }

    public void Dispose()
    {
        lock (_gate) _disposed = true;
    }
}
