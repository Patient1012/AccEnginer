using AccEngineer.Analysis.Fuel;

namespace AccEngineer.Analysis.Tests;

public class FuelLapEstimatorTests
{
    [Theory]
    [InlineData(20, 1, 20)]
    [InlineData(20, 5, 16)]
    [InlineData(20, 20, 1)]
    [InlineData(20, 21, 0)]
    [InlineData(0, 5, 0)]
    public void RemainingRaceLaps_CountsInProgressLap(int totalLaps, int currentLap, int expected)
    {
        Assert.Equal(expected, FuelLapEstimator.RemainingRaceLaps(totalLaps, currentLap));
    }

    [Fact]
    public void EstimateRemainingLapsFromTime_ReturnsFractionOfLaps()
    {
        var laps = FuelLapEstimator.EstimateRemainingLapsFromTime(380, 95);

        Assert.Equal(4.0, laps!.Value, 6);
    }

    [Fact]
    public void EstimateRemainingLapsFromTime_DoesNotRoundPartialLapUp()
    {
        var laps = FuelLapEstimator.EstimateRemainingLapsFromTime(100, 95);

        Assert.Equal(1.0526, laps!.Value, 3);
    }

    [Fact]
    public void EstimateRemainingLapsFromTime_ZeroTimeLeft_ReturnsZero()
    {
        Assert.Equal(0.0, FuelLapEstimator.EstimateRemainingLapsFromTime(0, 95)!.Value, 6);
    }

    [Fact]
    public void EstimateRemainingLapsFromTime_MissingLapTime_ReturnsNull()
    {
        Assert.Null(FuelLapEstimator.EstimateRemainingLapsFromTime(380, null));
        Assert.Null(FuelLapEstimator.EstimateRemainingLapsFromTime(380, 0));
    }

    [Fact]
    public void EstimateRemainingLapsFromTime_MissingTimeLeft_ReturnsNull()
    {
        Assert.Null(FuelLapEstimator.EstimateRemainingLapsFromTime(null, 95));
    }

    [Fact]
    public void AverageLapSeconds_IgnoresNonPositiveAndAveragesRest()
    {
        var average = FuelLapEstimator.AverageLapSeconds(new[] { 95000, 0, -5, 97000 });

        Assert.Equal(96.0, average!.Value, 6);
    }

    [Fact]
    public void AverageLapSeconds_NoUsableTimes_ReturnsNull()
    {
        Assert.Null(FuelLapEstimator.AverageLapSeconds(Array.Empty<int>()));
        Assert.Null(FuelLapEstimator.AverageLapSeconds(null));
    }
}
