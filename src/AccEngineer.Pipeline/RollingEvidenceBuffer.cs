using AccEngineer.Telemetry;

namespace AccEngineer.Pipeline;

/// <summary>
/// Bounded, thread-safe rolling window of the most recent telemetry samples.
/// Used to attach a small evidence window to a live event without keeping the
/// whole session in memory. Samples are assumed to arrive with non-decreasing
/// <see cref="TelemetrySample.SampleId"/>.
/// </summary>
public sealed class RollingEvidenceBuffer
{
    private readonly object _sync = new();
    private readonly List<TelemetrySample> _samples;
    private readonly int _capacity;

    public RollingEvidenceBuffer(int capacity = 4000)
    {
        _capacity = Math.Max(1, capacity);
        _samples = new List<TelemetrySample>(Math.Min(_capacity, 1024));
    }

    public int Capacity => _capacity;

    public int Count
    {
        get { lock (_sync) { return _samples.Count; } }
    }

    public void Add(TelemetrySample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        lock (_sync)
        {
            _samples.Add(sample);
            var overflow = _samples.Count - _capacity;
            if (overflow > 0)
            {
                _samples.RemoveRange(0, overflow);
            }
        }
    }

    public void AddRange(IEnumerable<TelemetrySample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        foreach (var sample in samples)
        {
            Add(sample);
        }
    }

    public void Clear()
    {
        lock (_sync) { _samples.Clear(); }
    }

    /// <summary>Samples whose id is within <paramref name="firstId"/>..<paramref name="lastId"/> (inclusive).</summary>
    public IReadOnlyList<TelemetrySample> Range(long firstId, long lastId)
    {
        if (lastId < firstId) (firstId, lastId) = (lastId, firstId);

        lock (_sync)
        {
            var result = new List<TelemetrySample>();
            foreach (var sample in _samples)
            {
                if (sample.SampleId < firstId) continue;
                if (sample.SampleId > lastId) break;
                result.Add(sample);
            }

            return result;
        }
    }

    /// <summary>Samples near a lap-distance location, when no sample ids are available.</summary>
    public IReadOnlyList<TelemetrySample> AroundProgress(double progressMeters, double toleranceMeters)
    {
        var tolerance = Math.Abs(toleranceMeters);
        lock (_sync)
        {
            return _samples
                .Where(s => s.TrackProgressMeters is { } p && Math.Abs(p - progressMeters) <= tolerance)
                .ToList();
        }
    }

    /// <summary>All retained samples snapshot (oldest first).</summary>
    public IReadOnlyList<TelemetrySample> Snapshot()
    {
        lock (_sync) { return _samples.ToArray(); }
    }
}
