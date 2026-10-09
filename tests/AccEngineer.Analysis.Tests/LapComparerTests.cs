namespace AccEngineer.Analysis.Tests;

public class LapComparerTests
{
    [Fact]
    public void Compare_IdenticalProfile_DifferentSampleTiming_YieldsZeroDelta()
    {
        var reference = Synth.Timeline(1, (0, 0), (100, 2), (200, 4), (300, 6), (400, 8), (500, 10));
        var other = Synth.Timeline(2, (0, 0), (150, 3), (300, 6), (450, 9), (500, 10));

        var comparison = new LapComparer().Compare(reference, new[] { reference, other });
        var delta = comparison.Laps.Single(l => l.LapNumber == 2);

        Assert.NotNull(delta.TotalDeltaSeconds);
        Assert.Equal(0.0, delta.TotalDeltaSeconds!.Value, 6);
        Assert.All(
            delta.Points.Where(p => p.DeltaSeconds is not null),
            p => Assert.True(Math.Abs(p.DeltaSeconds!.Value) < 1e-6, $"delta at {p.ProgressMeters} m was {p.DeltaSeconds}"));
    }

    [Fact]
    public void Compare_SlowerSecondHalf_ReportsLossRegion()
    {
        var reference = Synth.Timeline(1, (0, 0), (250, 5), (500, 10), (750, 15), (1000, 20));
        var slower = Synth.Timeline(2, (0, 0), (250, 5), (500, 10.5), (750, 16), (1000, 21.5));

        var delta = new LapComparer().Compare(reference, new[] { slower }).Laps[0];

        Assert.Equal(1.5, delta.TotalDeltaSeconds!.Value, 6);
        Assert.True(delta.DeltaAtEndSeconds!.Value > 1.4);

        Assert.Contains(
            delta.Regions,
            r => !r.IsGain && r.StartProgressMeters >= 249 && r.EndProgressMeters >= 999);

        Assert.All(
            delta.Points.Where(p => p.ProgressMeters < 250 && p.DeltaSeconds is not null),
            p => Assert.True(Math.Abs(p.DeltaSeconds!.Value) < 1e-6));
    }

    [Fact]
    public void Compare_ReferenceWithoutProgress_Throws()
    {
        var thin = Synth.Timeline(1, (0, 0));

        Assert.Throws<InvalidOperationException>(() => new LapComparer().Compare(thin, new[] { thin }));
    }

    [Fact]
    public void SelectFastestValid_IgnoresInvalidAndTimelessLaps()
    {
        var fastestButInvalid = Synth.Timeline(3, (0, 0), (1000, 90)) with { IsValid = false };
        var noTime = Synth.Timeline(4, (0, 0), (1000, 80)) with { LapTimeSeconds = null };
        var good1 = Synth.Timeline(1, (0, 0), (1000, 92));
        var good2 = Synth.Timeline(2, (0, 0), (1000, 91));

        var selected = ReferenceLapSelector.SelectFastestValid(new[] { fastestButInvalid, noTime, good1, good2 });

        Assert.NotNull(selected);
        Assert.Equal(2, selected!.LapNumber);
        Assert.Null(ReferenceLapSelector.SelectFastestValid(new[] { fastestButInvalid, noTime }));
    }

    [Fact]
    public void SelectFastestValid_TieBreaks_ByLowestLapNumber()
    {
        var lap5 = Synth.Timeline(5, (0, 0), (1000, 90));
        var lap2 = Synth.Timeline(2, (0, 0), (1000, 90));

        var selected = ReferenceLapSelector.SelectFastestValid(new[] { lap5, lap2 });

        Assert.NotNull(selected);
        Assert.Equal(2, selected!.LapNumber);
    }
}
