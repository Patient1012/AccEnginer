namespace AccEngineer.Telemetry;

public sealed class TelemetryValidatorOptions
{
    /// <summary>A receive gap larger than this flags the sample with GapDetected.</summary>
    public TimeSpan GapThreshold { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>A sample older than this when validated is flagged Stale.</summary>
    public TimeSpan StalenessThreshold { get; init; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Stream-level data-quality checks across consecutive samples: receive gaps,
/// out-of-order arrival, and staleness. Pure and stateful only over the previous
/// sample, so behaviour is deterministic and unit-testable without ACC.
///
/// Flags returned here are meant to be merged with flags already set by the
/// reader/builder (see <c>TelemetrySample.Quality</c>).
/// </summary>
public sealed class TelemetryValidator
{
    private readonly TelemetryValidatorOptions _options;
    private TelemetrySample? _previous;

    public TelemetryValidator(TelemetryValidatorOptions? options = null)
    {
        _options = options ?? new TelemetryValidatorOptions();
    }

    public DataQualityFlags Validate(TelemetrySample sample, DateTimeOffset nowUtc)
    {
        var flags = DataQualityFlags.None;

        if (_previous is not null)
        {
            if (sample.ReceivedAtUtc < _previous.ReceivedAtUtc ||
                sample.SampleId <= _previous.SampleId)
            {
                flags |= DataQualityFlags.OutOfOrder;
            }

            if (sample.ReceivedAtUtc - _previous.ReceivedAtUtc > _options.GapThreshold)
            {
                flags |= DataQualityFlags.GapDetected;
            }
        }

        if (nowUtc - sample.ReceivedAtUtc > _options.StalenessThreshold)
        {
            flags |= DataQualityFlags.Stale;
        }

        _previous = sample;
        return flags;
    }
}
