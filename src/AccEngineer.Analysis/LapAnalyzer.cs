using AccEngineer.Telemetry;

namespace AccEngineer.Analysis;

/// <summary>
/// Reconstructs laps from an ordered telemetry stream. Deterministic and free of
/// ACC or I/O dependencies so it can run on recorded or synthetic data.
///
/// Lap boundaries are detected by a change in (session id, lap number). Within a
/// session the final run is marked incomplete because no following lap was
/// observed, so a recording that stops mid-lap never reports a partial lap as a
/// valid lap time. Validity also requires every sample to report a valid lap and
/// to carry no invalid-lap quality flag.
/// </summary>
public sealed class LapAnalyzer
{
    private readonly LapAnalysisOptions _options;

    public LapAnalyzer(LapAnalysisOptions? options = null)
    {
        _options = options ?? new LapAnalysisOptions();
    }

    public SessionLapAnalysis Analyze(IReadOnlyList<TelemetrySample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0)
        {
            return new SessionLapAnalysis
            {
                SessionId = null,
                Laps = Array.Empty<LapTimeline>(),
                Warnings = new[] { "No samples to analyse." }
            };
        }

        var warnings = new List<string>();
        if (HasOutOfOrderSamples(samples))
        {
            warnings.Add("Samples are not strictly ordered by sample id; lap reconstruction assumes recorded order.");
        }

        var runs = BuildRuns(samples);
        for (var i = 0; i < runs.Count; i++)
        {
            runs[i].IsComplete = i + 1 < runs.Count && runs[i + 1].SessionId == runs[i].SessionId;
        }

        var laps = new List<LapTimeline>(runs.Count);
        var skipped = 0;

        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (run.Samples.Count < _options.MinimumSamplesPerLap)
            {
                skipped++;
                continue;
            }

            laps.Add(BuildLap(run, runs, i, warnings));
        }

        if (skipped > 0)
        {
            warnings.Add($"Skipped {skipped} sample run(s) shorter than {_options.MinimumSamplesPerLap} samples.");
        }

        if (laps.Any(l => l.Samples.All(s => s.ProgressMeters is null)))
        {
            warnings.Add("At least one lap has no track progress data and cannot be distance-aligned.");
        }

        var first = samples[0];
        return new SessionLapAnalysis
        {
            SessionId = first.SessionId,
            TrackName = first.TrackName,
            CarName = first.CarName,
            SessionType = first.SessionType,
            Laps = laps,
            SkippedLapCount = skipped,
            Warnings = warnings
        };
    }

    private static bool HasOutOfOrderSamples(IReadOnlyList<TelemetrySample> samples)
    {
        for (var i = 1; i < samples.Count; i++)
        {
            if (samples[i].SampleId <= samples[i - 1].SampleId) return true;
        }
        return false;
    }

    private static List<Run> BuildRuns(IReadOnlyList<TelemetrySample> samples)
    {
        var runs = new List<Run>();
        Run? current = null;

        foreach (var sample in samples)
        {
            if (current is null || sample.SessionId != current.SessionId || sample.LapNumber != current.LapNumber)
            {
                current = new Run(sample.SessionId, sample.LapNumber);
                runs.Add(current);
            }
            current.Samples.Add(sample);
        }

        return runs;
    }

    private static LapTimeline BuildLap(Run run, List<Run> runs, int index, List<string> warnings)
    {
        var firstSample = run.Samples[0];
        var lastSample = run.Samples[^1];
        var startedAt = firstSample.ReceivedAtUtc;

        var (lapTimeSeconds, source) = ResolveLapTime(run, runs, index);

        var lapSamples = run.Samples
            .Select(sample => new LapSample
            {
                Source = sample,
                LapTimeSeconds = (sample.ReceivedAtUtc - startedAt).TotalSeconds,
                ProgressMeters = sample.TrackProgressMeters
            })
            .ToArray();

        var sectors = BuildSectors(run, lapTimeSeconds);

        var allValid = run.Samples.All(s => s.IsLapValid);
        var anyInvalidFlag = run.Samples.Any(s => s.Quality.HasFlag(DataQualityFlags.InvalidLap));
        var isValid = run.IsComplete && allValid && !anyInvalidFlag;

        if (run.IsComplete && !isValid)
        {
            warnings.Add($"Lap {run.LapNumber} is invalid according to the source.");
        }

        return new LapTimeline
        {
            SessionId = run.SessionId,
            LapNumber = run.LapNumber,
            IsComplete = run.IsComplete,
            IsValid = isValid,
            StartedAtUtc = startedAt,
            EndedAtUtc = lastSample.ReceivedAtUtc,
            LapTimeSeconds = lapTimeSeconds,
            LapTimeSource = source,
            SectorCount = firstSample.SectorCount,
            Samples = lapSamples,
            Sectors = sectors
        };
    }

    /// <summary>
    /// Determines the lap time. ACC's reported last-lap time on the first sample of
    /// the following lap is authoritative when present; otherwise the boundary time
    /// between the two laps is used. Incomplete laps only report their partial span.
    /// </summary>
    private static (double? Seconds, LapTimeSource Source) ResolveLapTime(Run run, List<Run> runs, int index)
    {
        if (!run.IsComplete)
        {
            var partial = (run.Samples[^1].ReceivedAtUtc - run.Samples[0].ReceivedAtUtc).TotalSeconds;
            return (partial, LapTimeSource.PartialSpan);
        }

        var nextFirst = runs[index + 1].Samples[0];
        var accMs = nextFirst.LastLapTimeMs;
        if (accMs is > 0)
        {
            return (accMs.Value / 1000.0, LapTimeSource.AccReported);
        }

        var boundary = (nextFirst.ReceivedAtUtc - run.Samples[0].ReceivedAtUtc).TotalSeconds;
        return (boundary, LapTimeSource.ComputedBoundary);
    }

    private static IReadOnlyList<SectorTimeline> BuildSectors(Run run, double? lapTimeSeconds)
    {
        int? currentSector = null;
        double? sectorStartLapTime = null;
        double? sectorStartProgress = null;
        double lastKnownLapTime = 0;
        var sectors = new List<SectorTimeline>();

        foreach (var sample in run.Samples)
        {
            var lapTime = (sample.ReceivedAtUtc - run.Samples[0].ReceivedAtUtc).TotalSeconds;
            lastKnownLapTime = lapTime;

            var sectorIndex = sample.CurrentSectorIndex;
            if (sectorIndex is null) continue;

            if (currentSector is null)
            {
                currentSector = sectorIndex;
                sectorStartLapTime = lapTime;
                sectorStartProgress = sample.TrackProgressMeters;
                continue;
            }

            if (sectorIndex != currentSector)
            {
                sectors.Add(new SectorTimeline
                {
                    SectorIndex = currentSector.Value,
                    StartProgressMeters = sectorStartProgress,
                    StartLapTimeSeconds = sectorStartLapTime,
                    TimeSeconds = lapTime - (sectorStartLapTime ?? lapTime)
                });

                currentSector = sectorIndex;
                sectorStartLapTime = lapTime;
                sectorStartProgress = sample.TrackProgressMeters;
            }
        }

        if (currentSector is not null)
        {
            // Final sector runs to the end of the lap when the lap time is known.
            var end = lapTimeSeconds ?? lastKnownLapTime;
            sectors.Add(new SectorTimeline
            {
                SectorIndex = currentSector.Value,
                StartProgressMeters = sectorStartProgress,
                StartLapTimeSeconds = sectorStartLapTime,
                TimeSeconds = end - (sectorStartLapTime ?? end)
            });
        }

        return sectors;
    }

    private sealed class Run
    {
        public Run(string sessionId, int lapNumber)
        {
            SessionId = sessionId;
            LapNumber = lapNumber;
        }

        public string SessionId { get; }

        public int LapNumber { get; }

        public List<TelemetrySample> Samples { get; } = new();

        public bool IsComplete { get; set; }
    }
}
