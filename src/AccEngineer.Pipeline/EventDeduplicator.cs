using AccEngineer.Analysis;

namespace AccEngineer.Pipeline;

/// <summary>
/// Coalesces repeated events from the same corner or condition so a recurring
/// pattern does not flood the queue, the model, or the reports (roadmap §9).
/// A key is category + lap + progress bucket; repeats inside the cooldown window
/// are folded into the existing item and counted.
/// </summary>
public sealed class EventDeduplicator
{
    private readonly double _bucketMeters;
    private readonly TimeSpan _cooldown;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public EventDeduplicator(double bucketMeters = 100, TimeSpan? cooldown = null)
    {
        _bucketMeters = Math.Max(1, bucketMeters);
        _cooldown = cooldown ?? TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// Returns true when the event should be queued. When false,
    /// <paramref name="coalescedCount"/> is the running count for that key.
    /// </summary>
    public bool ShouldAccept(TelemetryEvent finding, DateTimeOffset now, out int coalescedCount)
    {
        ArgumentNullException.ThrowIfNull(finding);

        var key = KeyFor(finding);
        if (_entries.TryGetValue(key, out var entry) && now - entry.LastSeenUtc < _cooldown)
        {
            entry.Count++;
            entry.LastSeenUtc = now;
            coalescedCount = entry.Count;
            return false;
        }

        _entries[key] = new Entry { Count = 1, LastSeenUtc = now };
        coalescedCount = 1;
        return true;
    }

    public void Reset() => _entries.Clear();

    /// <summary>Stable key for an event: category + lap + progress bucket.</summary>
    public string KeyFor(TelemetryEvent finding)
    {
        var bucket = finding.ProgressMeters is { } progress
            ? ((long)Math.Floor(progress / _bucketMeters)).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "?";
        var lap = finding.LapNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?";
        return $"{finding.Category}|{lap}|{bucket}";
    }

    private sealed class Entry
    {
        public int Count { get; set; }

        public DateTimeOffset LastSeenUtc { get; set; }
    }
}
