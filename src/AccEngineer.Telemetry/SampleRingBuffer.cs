namespace AccEngineer.Telemetry;

/// <summary>
/// Bounded ring buffer of recent telemetry samples (oldest to newest).
/// Overwriting the oldest entry when full. Single-threaded use.
/// Intended for rolling evidence windows around detected events.
/// </summary>
public sealed class SampleRingBuffer
{
    private readonly TelemetrySample?[] _items;
    private int _nextIndex;
    private int _count;

    public SampleRingBuffer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        _items = new TelemetrySample[capacity];
    }

    public int Capacity => _items.Length;

    public int Count => _count;

    public void Push(TelemetrySample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        _items[_nextIndex] = sample;
        _nextIndex = (_nextIndex + 1) % _items.Length;
        if (_count < _items.Length) _count++;
    }

    /// <summary>Item by recency rank: 0 = oldest retained, Count-1 = newest.</summary>
    public TelemetrySample this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));
            var start = (_nextIndex - _count + _items.Length) % _items.Length;
            return _items[(start + index) % _items.Length]!;
        }
    }

    /// <summary>Snapshot of retained samples ordered oldest to newest.</summary>
    public IReadOnlyList<TelemetrySample> Snapshot()
    {
        var result = new TelemetrySample[_count];
        for (var i = 0; i < _count; i++) result[i] = this[i];
        return result;
    }
}
