using AccEngineer.Analysis;

namespace AccEngineer.Pipeline.Tests;

public class EventDeduplicatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstOccurrenceIsAccepted()
    {
        var dedup = new EventDeduplicator();

        Assert.True(dedup.ShouldAccept(PipelineTestData.Event("a"), Now, out var count));
        Assert.Equal(1, count);
    }

    [Fact]
    public void ImmediateRepeatIsCoalesced()
    {
        var dedup = new EventDeduplicator();

        dedup.ShouldAccept(PipelineTestData.Event("a"), Now, out _);
        var accepted = dedup.ShouldAccept(PipelineTestData.Event("b"), Now.AddSeconds(1), out var count);

        Assert.False(accepted);
        Assert.Equal(2, count);
    }

    [Fact]
    public void DifferentProgressBucketIsAccepted()
    {
        var dedup = new EventDeduplicator(bucketMeters: 100);

        dedup.ShouldAccept(PipelineTestData.Event("a", progress: 100), Now, out _);
        Assert.True(dedup.ShouldAccept(PipelineTestData.Event("b", progress: 300), Now.AddSeconds(1), out _));
    }

    [Fact]
    public void DifferentCategoryIsAccepted()
    {
        var dedup = new EventDeduplicator();

        dedup.ShouldAccept(PipelineTestData.Event("a", category: EventCategory.Braking), Now, out _);
        Assert.True(dedup.ShouldAccept(
            PipelineTestData.Event("b", category: EventCategory.CornerExit),
            Now.AddSeconds(1),
            out _));
    }

    [Fact]
    public void RepeatAfterCooldownIsAcceptedAgain()
    {
        var dedup = new EventDeduplicator(cooldown: TimeSpan.FromSeconds(15));

        dedup.ShouldAccept(PipelineTestData.Event("a"), Now, out _);
        Assert.True(dedup.ShouldAccept(PipelineTestData.Event("b"), Now.AddSeconds(20), out _));
    }

    [Fact]
    public void ResetClearsState()
    {
        var dedup = new EventDeduplicator();

        dedup.ShouldAccept(PipelineTestData.Event("a"), Now, out _);
        dedup.Reset();

        Assert.True(dedup.ShouldAccept(PipelineTestData.Event("b"), Now.AddSeconds(1), out var count));
        Assert.Equal(1, count);
    }
}