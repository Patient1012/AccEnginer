using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Tests;

public class LapAnalyzerTests
{
    private static List<TelemetrySample> TwoSimpleLaps(int samplesPerLap = 5)
    {
        var samples = new List<TelemetrySample>();
        long id = 0;
        double time = 0;

        void AddLap(int lap)
        {
            for (var i = 0; i < samplesPerLap; i++)
            {
                time += 0.05;
                samples.Add(Synth.Sample(++id, lap, i * 100.0, time));
            }
        }

        AddLap(1);
        AddLap(2);
        return samples;
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsNoLapsAndWarning()
    {
        var analysis = new LapAnalyzer().Analyze(Array.Empty<TelemetrySample>());

        Assert.Empty(analysis.Laps);
        Assert.Contains(analysis.Warnings, w => w.Contains("No samples", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_LapNumberChange_DetectsBoundary()
    {
        var analysis = new LapAnalyzer().Analyze(TwoSimpleLaps());

        Assert.Equal(2, analysis.Laps.Count);

        var first = analysis.Laps[0];
        Assert.True(first.IsComplete);
        Assert.True(first.IsValid);
        Assert.Equal(LapTimeSource.ComputedBoundary, first.LapTimeSource);
        Assert.Equal(0.25, first.LapTimeSeconds!.Value, 6);

        var second = analysis.Laps[1];
        Assert.False(second.IsComplete);
        Assert.False(second.IsValid);
        Assert.Equal(LapTimeSource.PartialSpan, second.LapTimeSource);
    }

    [Fact]
    public void Analyze_AccReportedLapTime_PreferredOverComputed()
    {
        var samples = new List<TelemetrySample>();
        long id = 0;
        double time = 0;
        for (var i = 0; i < 5; i++) samples.Add(Synth.Sample(++id, 1, i * 100, time += 0.05));
        for (var i = 0; i < 5; i++)
        {
            samples.Add(Synth.Sample(++id, 2, i * 100, time += 0.05, lastLapTimeMs: i == 0 ? 92_123 : null));
        }

        var lap1 = new LapAnalyzer().Analyze(samples).Laps[0];

        Assert.Equal(LapTimeSource.AccReported, lap1.LapTimeSource);
        Assert.Equal(92.123, lap1.LapTimeSeconds!.Value, 6);
    }

    [Fact]
    public void Analyze_InvalidLapFlag_KeepsLapButMarksInvalid()
    {
        var samples = new List<TelemetrySample>();
        long id = 0;
        double time = 0;
        samples.Add(Synth.Sample(++id, 1, 0, time += 0.05, quality: DataQualityFlags.InvalidLap));
        samples.Add(Synth.Sample(++id, 1, 100, time += 0.05));
        samples.Add(Synth.Sample(++id, 1, 200, time += 0.05));
        for (var i = 0; i < 5; i++) samples.Add(Synth.Sample(++id, 2, i * 100, time += 0.05));

        var analysis = new LapAnalyzer().Analyze(samples);
        var lap1 = analysis.Laps[0];

        Assert.True(lap1.IsComplete);
        Assert.False(lap1.IsValid);
        Assert.Contains(analysis.Warnings, w => w.Contains("Lap 1 is invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_SessionChange_SplitsLapsAndLeavesEachSessionsLastIncomplete()
    {
        var samples = new List<TelemetrySample>();
        long id = 0;
        double time = 0;
        for (var i = 0; i < 5; i++) samples.Add(Synth.Sample(++id, 1, i * 100, time += 0.05, sessionId: "A"));
        for (var i = 0; i < 5; i++) samples.Add(Synth.Sample(++id, 1, i * 100, time += 0.05, sessionId: "B"));

        var analysis = new LapAnalyzer().Analyze(samples);

        Assert.Equal(2, analysis.Laps.Count);
        Assert.All(analysis.Laps, lap => Assert.False(lap.IsComplete));
        Assert.Equal("A", analysis.Laps[0].SessionId);
        Assert.Equal("B", analysis.Laps[1].SessionId);
    }

    [Fact]
    public void Analyze_SectorIndexChanges_ProduceSectorTimelines()
    {
        var samples = new List<TelemetrySample>();
        long id = 0;
        double time = 0;
        int[] sectors = { 0, 0, 1, 1, 2, 2 };
        for (var i = 0; i < sectors.Length; i++)
        {
            samples.Add(Synth.Sample(++id, 1, i * 100, time += 0.05, sectorIndex: sectors[i], sectorCount: 3));
        }
        for (var i = 0; i < 5; i++) samples.Add(Synth.Sample(++id, 2, i * 100, time += 0.05));

        var lap1 = new LapAnalyzer().Analyze(samples).Laps[0];

        Assert.Equal(3, lap1.Sectors.Count);
        Assert.Equal(3, lap1.SectorCount);
        Assert.Equal(0, lap1.Sectors[0].SectorIndex);
        Assert.Equal(0.10, lap1.Sectors[0].TimeSeconds!.Value, 6);
        Assert.Equal(1, lap1.Sectors[1].SectorIndex);
        Assert.Equal(0.10, lap1.Sectors[1].TimeSeconds!.Value, 6);
        Assert.Equal(2, lap1.Sectors[2].SectorIndex);
        // Sector 2 starts at lap-relative 0.20 s and runs to the 0.30 s lap boundary.
        Assert.Equal(0.10, lap1.Sectors[2].TimeSeconds!.Value, 6);
    }

    [Fact]
    public void Analyze_RunShorterThanMinimum_IsSkippedAndWarned()
    {
        var samples = new List<TelemetrySample>();
        long id = 0;
        double time = 0;
        samples.Add(Synth.Sample(++id, 1, 0, time += 0.05)); // single-sample run
        for (var i = 0; i < 5; i++) samples.Add(Synth.Sample(++id, 2, i * 100, time += 0.05));

        var analysis = new LapAnalyzer().Analyze(samples);

        Assert.Equal(1, analysis.SkippedLapCount);
        Assert.Single(analysis.Laps);
        Assert.Contains(analysis.Warnings, w => w.Contains("Skipped", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsWarning()
    {
        var analysis = new LapAnalyzer().Analyze(Array.Empty<TelemetrySample>());

        Assert.Empty(analysis.Laps);
        Assert.NotEmpty(analysis.Warnings);
    }
}
