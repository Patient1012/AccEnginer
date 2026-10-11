namespace AccEngineer.Pipeline.Tests;

public class RollingEvidenceBufferTests
{
    [Fact]
    public void TrimsOldestSamplesWhenOverCapacity()
    {
        var buffer = new RollingEvidenceBuffer(capacity: 5);
        PipelineTestData.Prefill(buffer, 1, 10);

        Assert.Equal(5, buffer.Count);
        var snapshot = buffer.Snapshot();
        Assert.Equal(6, snapshot[0].SampleId);
        Assert.Equal(10, snapshot[^1].SampleId);
    }

    [Fact]
    public void RangeReturnsInclusiveSamples()
    {
        var buffer = new RollingEvidenceBuffer(capacity: 50);
        PipelineTestData.Prefill(buffer, 1, 30);

        var range = buffer.Range(10, 20);

        Assert.Equal(11, range.Count);
        Assert.Equal(10, range[0].SampleId);
        Assert.Equal(20, range[^1].SampleId);
    }

    [Fact]
    public void RangeNormalizesSwappedBounds()
    {
        var buffer = new RollingEvidenceBuffer(capacity: 50);
        PipelineTestData.Prefill(buffer, 1, 30);

        var range = buffer.Range(20, 10);

        Assert.Equal(11, range.Count);
    }

    [Fact]
    public void AroundProgressFiltersByTolerance()
    {
        var buffer = new RollingEvidenceBuffer(capacity: 50);
        buffer.Add(PipelineTestData.Sample(1, progress: 100));
        buffer.Add(PipelineTestData.Sample(2, progress: 110));
        buffer.Add(PipelineTestData.Sample(3, progress: 300));

        var near = buffer.AroundProgress(105, toleranceMeters: 10);

        Assert.Equal(2, near.Count);
    }

    [Fact]
    public void ClearEmptiesBuffer()
    {
        var buffer = new RollingEvidenceBuffer(capacity: 10);
        buffer.Add(PipelineTestData.Sample(1, 10));
        buffer.Clear();

        Assert.Equal(0, buffer.Count);
    }
}
