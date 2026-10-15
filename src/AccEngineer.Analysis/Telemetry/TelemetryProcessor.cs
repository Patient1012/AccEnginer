using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Telemetry;

public sealed class TelemetryProcessorOptions
{
    public TimeSpan GapThreshold { get; init; } = TimeSpan.FromMilliseconds(500);
    public bool InterpolateGaps { get; init; }
    public int InterpolationMaxSteps { get; init; } = 10;
}

public sealed class TelemetryProcessor
{
    private readonly TelemetryValidator _validator;
    private readonly TelemetryProcessorOptions _options;

    public TelemetryProcessor(TelemetryProcessorOptions? options = null)
    {
        _options = options ?? new TelemetryProcessorOptions();
        _validator = new TelemetryValidator(new TelemetryValidatorOptions
        {
            GapThreshold = _options.GapThreshold,
            StalenessThreshold = TimeSpan.FromSeconds(1)
        });
    }

    public ProcessedSample Process(TelemetrySample sample, DateTimeOffset nowUtc)
    {
        var gapFlags = _validator.Validate(sample, nowUtc);
        var hasGap = gapFlags.HasFlag(DataQualityFlags.GapDetected);

        var speedKph = sample.SpeedKph ?? (sample.SpeedMetersPerSecond.HasValue ? (float)(sample.SpeedMetersPerSecond.Value * 3.6f) : null);
        var speedMs = sample.SpeedMetersPerSecond ?? (sample.SpeedKph.HasValue ? (float)(sample.SpeedKph.Value / 3.6f) : null);

        return new ProcessedSample
        {
            Raw = sample,
            IsProcessed = true,
            HasGap = hasGap,
            IsInterpolated = false,
            TrackDistanceMeters = sample.TrackProgressMeters,
            SpeedKph = speedKph,
            SpeedMs = speedMs
        };
    }
}