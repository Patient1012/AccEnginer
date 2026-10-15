using AccEngineer.Analysis.Findings;

namespace AccEngineer.Analysis.Tests;

public class ConfidenceCalculatorTests
{
    [Fact]
    public void Score_FullFactors_IsOne()
    {
        var calculator = new ConfidenceCalculator();

        var score = calculator.Score(ConfidenceFactors.Full);

        Assert.Equal(1.0, score, 3);
        Assert.Equal(EventConfidence.Verified, calculator.Classify(score));
    }

    [Fact]
    public void Score_NoFactors_IsZero()
    {
        var calculator = new ConfidenceCalculator();

        var score = calculator.Score(default);

        Assert.Equal(0.0, score, 3);
        Assert.Equal(EventConfidence.Uncertain, calculator.Classify(score));
    }

    [Fact]
    public void Classify_MidScore_IsProbable()
    {
        var calculator = new ConfidenceCalculator();

        Assert.Equal(EventConfidence.Probable, calculator.Classify(0.6));
    }
}
