using AccEngineer.Analysis;
using AccEngineer.Analysis.Corners;
using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Tests;

public class AnalysisEngineTests
{
    private static TrackCornerDefinition Corners() => new()
    {
        Track = "Test",
        Corners = new[]
        {
            new Corner
            {
                Number = 1,
                StartDistanceMeters = 50,
                TurnInDistanceMeters = 50,
                ApexDistanceMeters = 100,
                ExitDistanceMeters = 150,
                EndDistanceMeters = 200
            }
        }
    };

    private static IReadOnlyList<TelemetrySample> Session()
    {
        TelemetrySample S(long id, int lap, double t, double p, float speed, float brake, float throttle, float steer, int gear, int? lastLapMs = null)
            => Synth.Sample(id, lap, p, t, speedKph: speed, brake: brake, throttle: throttle, steering: steer, gear: gear, lastLapTimeMs: lastLapMs);

        return new[]
        {
            // Lap 1 (reference): 8.0 s lap.
            S(1, 1, 0.0, 0, 200, 0, 1, 0, 5),
            S(2, 1, 2.0, 50, 150, 0.8f, 0, 0.3f, 4),
            S(3, 1, 4.0, 100, 120, 0.4f, 0.3f, 0.4f, 3),
            S(4, 1, 6.0, 150, 150, 0, 0.9f, 0.2f, 4),
            S(5, 1, 8.0, 200, 180, 0, 1, 0, 5),

            // Lap 2 (current): 9.0 s lap, slower through the corner.
            S(6, 2, 8.0, 0, 200, 0, 1, 0, 5, lastLapMs: 8000),
            S(7, 2, 10.0, 50, 150, 0.7f, 0, 0.3f, 4),
            S(8, 2, 12.5, 100, 115, 0.5f, 0.2f, 0.45f, 3),
            S(9, 2, 14.5, 150, 145, 0, 0.9f, 0.2f, 4),
            S(10, 2, 17.0, 200, 175, 0, 1, 0, 5),

            // Lap 3 (partial, marks lap 2 complete).
            S(11, 3, 17.0, 0, 180, 0, 1, 0, 5, lastLapMs: 9000),
            S(12, 3, 18.0, 50, 150, 0, 1, 0, 4)
        };
    }

    [Fact]
    public void Analyze_ProducesFullResult()
    {
        var result = new AnalysisEngine().Analyze(new SessionAnalysisRequest
        {
            Samples = Session(),
            Corners = Corners(),
            Track = "Test",
            Car = "GT3",
            Session = "Practice"
        });

        Assert.Equal(2, result.Laps.Count);
        Assert.NotNull(result.TimeLoss);
        Assert.NotEmpty(result.Findings);
        Assert.NotEmpty(result.Events);
        Assert.Equal("Test", result.AiSummary.Track);
        Assert.Equal("0:09.000", result.AiSummary.CurrentLap);
    }

    [Fact]
    public void Analyze_EstimatesTimeLossThroughCorner()
    {
        var result = new AnalysisEngine().Analyze(new SessionAnalysisRequest
        {
            Samples = Session(),
            Corners = Corners()
        });

        var corner = Assert.Single(result.TimeLoss!.Corners);
        Assert.Equal(1, corner.CornerNumber);
        Assert.Equal(1.0, corner.EstimatedTimeLossSeconds!.Value, 2);
    }

    [Fact]
    public void Analyze_MapsTopFindingToAiSummary()
    {
        var result = new AnalysisEngine().Analyze(new SessionAnalysisRequest
        {
            Samples = Session(),
            Corners = Corners()
        });

        var top = result.AiSummary.TopFindings[0];
        Assert.Equal(1, top.Rank);
        Assert.NotNull(top.Observation);
    }

    [Fact]
    public void Analyze_WithoutCorners_StillProducesResult()
    {
        var result = new AnalysisEngine().Analyze(new SessionAnalysisRequest { Samples = Session() });

        Assert.Equal(2, result.Laps.Count);
        Assert.Empty(result.TimeLoss!.Corners);
        Assert.NotNull(result.AiSummary);
    }

    [Fact]
    public void Analyze_NoSamples_ReportsWarning()
    {
        var result = new AnalysisEngine().Analyze(new SessionAnalysisRequest
        {
            Samples = Array.Empty<TelemetrySample>()
        });

        Assert.Empty(result.Laps);
        Assert.Contains(result.Warnings, w => w.Contains("Not enough valid laps", StringComparison.OrdinalIgnoreCase));
    }
}
