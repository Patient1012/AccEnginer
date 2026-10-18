using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Telemetry;

/// <summary>
/// Speed profile along the lap, built from the player's own recent samples, used to
/// turn a distance gap into a time gap.
///
/// ACC exposes no opponent time gaps: <c>DeltaMs</c> is a delta to the reference lap
/// (the same semantics for every car), so it cannot be differenced into an interval,
/// and the shared-memory opponent list has no timing at all. The defensible
/// alternative is therefore to integrate the player's recent pace over the distance:
/// <c>time = integral of ds / v(s)</c>, where <c>v(s)</c> is sampled from the player's
/// own driven laps rather than assumed constant (a fixed conversion such as
/// metres/50 would ignore corners, braking and traffic).
///
/// The result is an estimate, not official timing: it is accurate to roughly
/// ±0.1–0.3 s while the track state resembles the sampled laps, and degrades in
/// traffic, under safety cars or after a setup change. Arc the profile never sampled
/// is filled with the player's mean recent pace, never with a hard-coded speed.
/// </summary>
public sealed class TrackPaceProfile
{
    /// <summary>Bin size along the lap, in metres. Small enough to resolve corners,
    /// large enough that a ~90-second window yields a few hundred bins.</summary>
    internal const int BinMeters = 10;

    /// <summary>Samples slower than this are pits/standing cars and are skipped so a
    /// crawl never dominates the profile.</summary>
    internal const double MinSpeedMps = 0.5;

    /// <summary>Minimum usable samples before any estimate is offered.</summary>
    internal const int MinSamples = 25;

    /// <summary>Minimum covered arc in metres. A session that has barely started has
    /// no representative profile, so gaps read "--" instead of a guess.</summary>
    internal const double MinCoveredMeters = 500;

    /// <summary>Backstop for inputs when no track length is available: larger gaps
    /// are treated as unavailable rather than integrated (mirrors the overlay's
    /// plausible-track-length bound).</summary>
    internal const double MaxPlausibleGapMeters = 25_000;

    private readonly Dictionary<int, double> _binSpeedMps;

    private TrackPaceProfile(Dictionary<int, double> binSpeedMps)
    {
        _binSpeedMps = binSpeedMps;
        MeanSpeedMps = binSpeedMps.Values.Average();
    }

    /// <summary>Mean speed over the covered part of the lap (metres per second).
    /// Only ever used to fill arc the profile never sampled.</summary>
    public double MeanSpeedMps { get; }

    /// <summary>Approximate arc of the lap the profile has actually sampled, in metres.</summary>
    public int CoveredMeters => _binSpeedMps.Count * BinMeters;

    /// <summary>
    /// Builds a profile from recent samples, or null when there is not enough usable
    /// data (empty history, too few samples, or too little of the lap covered).
    /// Samples need <see cref="TelemetrySample.TrackProgressMeters"/> and a usable
    /// speed; duplicates across laps collapse into the same bins on purpose.
    /// </summary>
    public static TrackPaceProfile? Build(IReadOnlyList<TelemetrySample>? recent)
    {
        if (recent is null || recent.Count < MinSamples) return null;

        var sums = new Dictionary<int, double>();
        var counts = new Dictionary<int, int>();
        var usable = 0;

        for (var i = 0; i < recent.Count; i++)
        {
            var sample = recent[i];
            if (sample.TrackProgressMeters is not { } progress) continue;
            if (double.IsNaN(progress) || double.IsInfinity(progress) || progress < 0) continue;

            var speed = sample.SpeedMetersPerSecond is { } metersPerSecond
                ? metersPerSecond
                : sample.SpeedKph is { } kilometersPerHour ? kilometersPerHour / 3.6f : 0f;
            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= MinSpeedMps) continue;

            var bin = (int)(progress / BinMeters);
            sums[bin] = sums.TryGetValue(bin, out var sum) ? sum + speed : speed;
            counts[bin] = counts.TryGetValue(bin, out var count) ? count + 1 : 1;
            usable++;
        }

        if (usable < MinSamples) return null;

        var bins = new Dictionary<int, double>(sums.Count);
        foreach (var (bin, sum) in sums)
        {
            bins[bin] = sum / counts[bin];
        }

        if (bins.Count * (double)BinMeters < MinCoveredMeters) return null;

        return new TrackPaceProfile(bins);
    }

    /// <summary>
    /// Seconds needed to cover <paramref name="deltaMeters"/> (signed as a distance
    /// along the lap, from <paramref name="fromProgressMeters"/>) at the profiled
    /// pace. Pass <paramref name="lapMeters"/> when the track length is known so the
    /// walk wraps across the start/finish line; without it the walk stays in raw
    /// progress coordinates and uncovered arc falls back to <see cref="MeanSpeedMps"/>.
    /// Returns null for non-finite input or gaps that could not be real.
    /// </summary>
    public double? TimeDeltaSeconds(double fromProgressMeters, double deltaMeters, double? lapMeters)
    {
        if (double.IsNaN(fromProgressMeters) || double.IsInfinity(fromProgressMeters)) return null;
        if (double.IsNaN(deltaMeters) || double.IsInfinity(deltaMeters)) return null;
        if (Math.Abs(deltaMeters) > MaxPlausibleGapMeters) return null;

        if (lapMeters is { } lap && (double.IsNaN(lap) || double.IsInfinity(lap) || lap <= 0)) return null;
        if (lapMeters is { } boundedLap && Math.Abs(deltaMeters) > boundedLap / 2) return null;

        var remaining = Math.Abs(deltaMeters);
        if (remaining <= 1e-6) return 0;

        // The integral depends only on which positions are covered, never on the
        // direction of travel, so the walk always runs forward from the lower end;
        // that keeps the step arithmetic free of boundary corner cases.
        var position = deltaMeters >= 0 ? fromProgressMeters : fromProgressMeters + deltaMeters;
        if (lapMeters is { } anchorLap) position = Mod(position, anchorLap);

        var total = 0d;
        var guard = 0;

        while (remaining > 1e-6)
        {
            if (++guard > 10_000) return null;

            var lookup = lapMeters is { } lapLength ? Mod(position, lapLength) : position;
            var bin = (int)Math.Floor(lookup / BinMeters);
            var toBoundary = ((bin + 1) * (double)BinMeters) - lookup;

            // With a known lap length the walk must also stop at start/finish: the arc
            // from there on belongs to the bins at the beginning of the lap.
            if (lapMeters is { } lapEnd) toBoundary = Math.Min(toBoundary, lapEnd - lookup);

            var segment = Math.Min(remaining, toBoundary);
            if (segment <= 0) return null;

            total += segment / SpeedAtBin(bin);
            position += segment;
            remaining -= segment;
        }

        return double.IsFinite(total) ? total : null;
    }

    /// <summary>Profiled speed for a bin; arc the player never drove uses the mean
    /// recent pace rather than a fabricated value.</summary>
    private double SpeedAtBin(int bin) =>
        _binSpeedMps.TryGetValue(bin, out var speed) ? speed : MeanSpeedMps;

    private static double Mod(double value, double modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
