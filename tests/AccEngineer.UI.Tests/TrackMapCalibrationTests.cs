using AccEngineer.UI.Services;

namespace AccEngineer.UI.Tests;

public class TrackMapCalibrationTests
{
    private static readonly (double X, double Z)[] Square =
    {
        (0, 0), (100, 0), (100, 100), (0, 100)
    };

    [Fact]
    public void TryCreate_ReturnsNull_WhenNoPoints()
    {
        var result = TrackMapCalibration.TryCreate(Array.Empty<(double, double)>(), 200, 200);
        Assert.Null(result);
    }

    [Fact]
    public void TryCreate_ReturnsNull_ForDegenerateSize()
    {
        var result = TrackMapCalibration.TryCreate(Square, 1, 1);
        Assert.Null(result);
    }

    [Fact]
    public void Project_IsUniform_SoAspectRatioIsPreserved()
    {
        var calibration = TrackMapCalibration.TryCreate(Square, 400, 200, padding: 8)!.Value;
        var a = calibration.Project(0, 0);
        var b = calibration.Project(100, 0);
        var c = calibration.Project(0, 100);

        var spanX = b.X - a.X;
        var spanZ = c.Y - a.Y;
        Assert.Equal(spanX, spanZ, precision: 6);
    }

    [Fact]
    public void Project_FitsWithinBounds_WithPadding()
    {
        var width = 300.0;
        var height = 220.0;
        const double padding = 8.0;
        var calibration = TrackMapCalibration.TryCreate(Square, width, height, padding)!.Value;

        foreach (var (x, z) in Square)
        {
            var mapped = calibration.Project(x, z);
            Assert.InRange(mapped.X, padding - 0.001, width - padding + 0.001);
            Assert.InRange(mapped.Y, padding - 0.001, height - padding + 0.001);
        }
    }

    [Fact]
    public void Project_IsCentred_ForSymmetricInput()
    {
        var width = 400.0;
        var height = 200.0;
        var calibration = TrackMapCalibration.TryCreate(Square, width, height)!.Value;

        var centre = calibration.Project(50, 50);
        Assert.Equal(width / 2.0, centre.X, precision: 6);
        Assert.Equal(height / 2.0, centre.Y, precision: 6);
    }

    [Fact]
    public void TryCreate_SkipsNonFinitePoints()
    {
        var points = new (double, double)[]
        {
            (double.NaN, 0), (0, double.PositiveInfinity), (0, 0), (100, 100)
        };
        var calibration = TrackMapCalibration.TryCreate(points, 200, 200);
        Assert.NotNull(calibration);
    }

    [Fact]
    public void TryCreate_AppliesMinimumSpan_SoTinySetsDoNotExplode()
    {
        // A single-ish point would otherwise divide by a near-zero span.
        var calibration = TrackMapCalibration.TryCreate(new[] { (0.0, 0.0) }, 200, 200)!.Value;
        Assert.True(double.IsFinite(calibration.Scale));
        Assert.True(calibration.Scale is > 0 and < 10);
    }
}
