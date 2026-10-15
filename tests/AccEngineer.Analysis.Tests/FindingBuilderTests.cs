using AccEngineer.Analysis.Comparison;
using AccEngineer.Analysis.Findings;
using AccEngineer.Analysis.Indicators;

namespace AccEngineer.Analysis.Tests;

public class FindingBuilderTests
{
    private static CornerTimeLoss Corner(bool sufficient = true) => new()
    {
        CornerNumber = 4,
        WindowStartMeters = 0,
        WindowEndMeters = 80,
        ReferenceBrakePointMeters = 120,
        CurrentBrakePointMeters = 145,
        BrakePointDeltaMeters = 25,
        ReferenceMinimumSpeedKph = 142,
        CurrentMinimumSpeedKph = 136,
        MinimumSpeedDeltaKph = -6,
        ReferenceThrottlePickupMeters = 84,
        CurrentThrottlePickupMeters = 114,
        ThrottlePickupDeltaMeters = 30,
        EstimatedTimeLossSeconds = 0.18,
        HasSufficientEvidence = sufficient
    };

    [Fact]
    public void FromTimeLoss_CreatesFindingAboveThreshold()
    {
        var analysis = new TimeLossAnalysis
        {
            ReferenceLapNumber = 1,
            CurrentLapNumber = 2,
            TotalDeltaSeconds = 0.18,
            Corners = new[] { Corner() }
        };

        var findings = new FindingBuilder().FromTimeLoss(analysis);

        var finding = Assert.Single(findings);
        Assert.Equal(4, finding.Location!.CornerNumber);
        Assert.Equal(0.18, finding.EstimatedTimeLossSeconds!.Value, 3);
        Assert.Equal(3, finding.Evidence.Count);
        Assert.NotEmpty(finding.PossibleCauses);
        Assert.NotNull(finding.ValidationMethod);
    }

    [Fact]
    public void FromTimeLoss_SkipsBelowThreshold()
    {
        var analysis = new TimeLossAnalysis
        {
            ReferenceLapNumber = 1,
            CurrentLapNumber = 2,
            Corners = new[]
            {
                new CornerTimeLoss { CornerNumber = 1, EstimatedTimeLossSeconds = 0.01 }
            }
        };

        var findings = new FindingBuilder().FromTimeLoss(analysis);

        Assert.Empty(findings);
    }

    [Fact]
    public void FromTimeLoss_SelectsDominantCategory()
    {
        var analysis = new TimeLossAnalysis
        {
            ReferenceLapNumber = 1,
            CurrentLapNumber = 2,
            Corners = new[] { Corner() }
        };

        var finding = Assert.Single(new FindingBuilder().FromTimeLoss(analysis));

        // Throttle pickup difference (30 m) is the largest standardised difference.
        Assert.Equal(EventCategory.CornerExit, finding.Category);
    }

    [Fact]
    public void FromIndicators_MapsCategoryAndEvidence()
    {
        var indicators = new[]
        {
            new BehaviourIndicator
            {
                Kind = BehaviourIndicatorKind.PossibleMidCornerUndersteer,
                CornerNumber = 4,
                Phase = CornerPhase.MidCorner,
                Observation = "Steering stays high.",
                Evidence = new[] { "Peak steering 0.45." },
                Confidence = 0.55
            }
        };

        var findings = new FindingBuilder().FromIndicators(indicators, lapNumber: 2);

        var finding = Assert.Single(findings);
        Assert.Equal(EventCategory.MidCorner, finding.Category);
        Assert.Single(finding.Evidence);
        Assert.Equal(EventConfidence.Probable, finding.Confidence);
    }
}
