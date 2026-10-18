using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Fuel;

/// <summary>Fuel consumed across one completed lap, measured from the live fuel channel.</summary>
public sealed record LapFuelObservation
{
    /// <summary>The completed lap number this observation describes.</summary>
    public int LapNumber { get; init; }

    /// <summary>Fuel consumed during the lap, in litres.</summary>
    public double LitresConsumed { get; init; }

    /// <summary>Lap time in milliseconds when ACC reported one.</summary>
    public int? LapTimeMs { get; init; }
}

/// <summary>Qualitative confidence in the tracked fuel figures, based on lap count and spread.</summary>
public enum FuelDataConfidence
{
    /// <summary>Not enough clean laps yet to report a dependable average.</summary>
    Insufficient,

    Low,
    Medium,
    High
}

/// <summary>Immutable view of the tracker state, safe to read from any thread.</summary>
public sealed record FuelConsumptionSnapshot
{
    /// <summary>Fuel consumed on the most recently completed, cleanly measured lap.</summary>
    public double? LastLapLitresPerLap { get; init; }

    /// <summary>Average fuel consumed across all cleanly measured laps.</summary>
    public double? AverageLitresPerLap { get; init; }

    /// <summary>Number of cleanly measured laps contributing to the average.</summary>
    public int ValidLapCount { get; init; }

    /// <summary>Average lap time in seconds across the measured laps, when available.</summary>
    public double? AverageLapSeconds { get; init; }

    public FuelDataConfidence Confidence { get; init; } = FuelDataConfidence.Insufficient;

    /// <summary>True once at least <see cref="FuelConsumptionTracker.MinimumLapsForAverage"/> laps are measured.</summary>
    public bool HasSufficientData { get; init; }

    /// <summary>The measured lap observations, oldest first.</summary>
    public IReadOnlyList<LapFuelObservation> Observations { get; init; } = Array.Empty<LapFuelObservation>();
}

/// <summary>
/// Measures real per-lap fuel consumption from the live fuel channel by comparing the
/// fuel at the start of each lap with the fuel at the start of the next. It deliberately
/// ignores anything that would make a lap's figure meaningless: the first (possibly
/// partial) lap after the tracker starts, laps ACC flags invalid, laps that span a
/// refuel, laps that jump backwards or skip a lap number, and samples flagged stale or
/// out of order. Cleanly measured laps feed the last/average figures and the confidence
/// estimate. Thread-safe: <see cref="Observe"/> runs on the reader thread while
/// <see cref="Snapshot"/> is read from the UI.
/// </summary>
public sealed class FuelConsumptionTracker
{
    /// <summary>Laps required before an average is reported instead of "collecting data".</summary>
    public const int MinimumLapsForAverage = 2;

    /// <summary>Fuel above this per lap is treated as a measurement error or refuel rather than a real lap.</summary>
    public const double MaxPlausibleLitresPerLap = 30.0;

    /// <summary>A rise in fuel above this (litres) during a lap is treated as a refuel.</summary>
    private const double RefuelEpsilon = 0.01;

    private const int MaxObservations = 200;

    private readonly object _gate = new();
    private readonly List<LapFuelObservation> _observations = new();
    private string? _sessionId;
    private int _lapStartLap = -1;
    private double? _lapStartFuel;
    private bool _lapRecordable;
    private int _lastRecordedLap = int.MinValue;

    /// <summary>Clears all measurements, for example when starting a new session.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            ResetInternal();
        }
    }

    /// <summary>Feeds one telemetry sample into the tracker.</summary>
    public void Observe(TelemetrySample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        lock (_gate)
        {
            if (sample.Quality.HasFlag(DataQualityFlags.SessionChanged) || _sessionId != sample.SessionId)
            {
                ResetInternal();
                _sessionId = sample.SessionId;
            }

            if (sample.Quality.HasFlag(DataQualityFlags.Stale) ||
                sample.Quality.HasFlag(DataQualityFlags.OutOfOrder))
            {
                return;
            }

            if (_lapStartLap < 0)
            {
                // First sample of the session: it may be mid-lap, so this lap is not recordable.
                Baseline(sample, recordable: false);
                return;
            }

            if (sample.LapNumber == _lapStartLap)
            {
                MarkCurrentLap(sample);
                return;
            }

            if (sample.LapNumber > _lapStartLap)
            {
                var completedLap = _lapStartLap;
                var lapDelta = sample.LapNumber - _lapStartLap;

                if (lapDelta == 1 && _lapRecordable && completedLap != _lastRecordedLap &&
                    _lapStartFuel is { } startFuel && sample.FuelLevel is { } endFuel)
                {
                    var used = startFuel - endFuel;
                    if (double.IsFinite(used) && used > 0 && used <= MaxPlausibleLitresPerLap)
                    {
                        _observations.Add(new LapFuelObservation
                        {
                            LapNumber = completedLap,
                            LitresConsumed = used,
                            LapTimeMs = sample.LastLapTimeMs
                        });

                        if (_observations.Count > MaxObservations)
                        {
                            _observations.RemoveAt(0);
                        }

                        _lastRecordedLap = completedLap;
                    }
                }

                // The sample at the boundary is the start of the new lap, so it is recordable.
                Baseline(sample, recordable: true);
                return;
            }

            // Lap number went backwards: rebaseline without trusting the previous lap.
            Baseline(sample, recordable: false);
        }
    }

    /// <summary>Returns an immutable snapshot of the current measurements.</summary>
    public FuelConsumptionSnapshot Snapshot()
    {
        lock (_gate)
        {
            var observations = _observations.ToArray();

            double? last = observations.Length > 0 ? observations[^1].LitresConsumed : null;
            double? average = observations.Length > 0 ? observations.Average(o => o.LitresConsumed) : null;

            var lapTimes = new List<int>();
            foreach (var observation in observations)
            {
                if (observation.LapTimeMs is > 0)
                {
                    lapTimes.Add(observation.LapTimeMs.Value);
                }
            }

            return new FuelConsumptionSnapshot
            {
                LastLapLitresPerLap = last,
                AverageLitresPerLap = average,
                ValidLapCount = observations.Length,
                AverageLapSeconds = FuelLapEstimator.AverageLapSeconds(lapTimes),
                Confidence = Classify(observations),
                HasSufficientData = observations.Length >= MinimumLapsForAverage,
                Observations = observations
            };
        }
    }

    private void ResetInternal()
    {
        _observations.Clear();
        _sessionId = null;
        _lapStartLap = -1;
        _lapStartFuel = null;
        _lapRecordable = false;
        _lastRecordedLap = int.MinValue;
    }

    private void Baseline(TelemetrySample sample, bool recordable)
    {
        _lapStartLap = sample.LapNumber;
        _lapStartFuel = sample.FuelLevel;
        _lapRecordable = recordable;
        if (recordable)
        {
            MarkCurrentLap(sample);
        }
    }

    private void MarkCurrentLap(TelemetrySample sample)
    {
        if (!sample.IsLapValid || sample.Quality.HasFlag(DataQualityFlags.InvalidLap))
        {
            _lapRecordable = false;
        }

        if (_lapStartFuel is { } startFuel && sample.FuelLevel is { } fuel && fuel - startFuel > RefuelEpsilon)
        {
            _lapRecordable = false;
        }
    }

    private static FuelDataConfidence Classify(IReadOnlyList<LapFuelObservation> observations)
    {
        if (observations.Count < MinimumLapsForAverage)
        {
            return FuelDataConfidence.Insufficient;
        }

        var mean = 0.0;
        foreach (var observation in observations)
        {
            mean += observation.LitresConsumed;
        }

        mean /= observations.Count;
        if (mean <= 0)
        {
            return FuelDataConfidence.Insufficient;
        }

        var variance = 0.0;
        foreach (var observation in observations)
        {
            var delta = observation.LitresConsumed - mean;
            variance += delta * delta;
        }

        variance /= observations.Count;
        var coefficientOfVariation = Math.Sqrt(variance) / mean;

        if (observations.Count >= 10 && coefficientOfVariation <= 0.10)
        {
            return FuelDataConfidence.High;
        }

        if (observations.Count >= 5 && coefficientOfVariation <= 0.20)
        {
            return FuelDataConfidence.Medium;
        }

        return FuelDataConfidence.Low;
    }
}
