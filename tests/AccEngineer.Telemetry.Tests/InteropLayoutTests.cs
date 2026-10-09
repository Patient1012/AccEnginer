using System.Runtime.InteropServices;

namespace AccEngineer.Telemetry.Tests;

/// <summary>
/// Layout regression tests for the ACC interop structs. Offsets were
/// cross-verified against three independent community implementations; these
/// tests fail if a struct edit silently shifts the layout.
/// </summary>
public class InteropLayoutTests
{
    [Theory]
    [InlineData(nameof(AccPhysicsPage.PacketId), 0)]
    [InlineData(nameof(AccPhysicsPage.Gas), 4)]
    [InlineData(nameof(AccPhysicsPage.Gear), 16)]
    [InlineData(nameof(AccPhysicsPage.SteerAngle), 24)]
    [InlineData(nameof(AccPhysicsPage.SpeedKmh), 28)]
    public void PhysicsPage_KnownOffsets(string field, int expected) =>
        Assert.Equal(expected, (int)Marshal.OffsetOf<AccPhysicsPage>(field));

    [Theory]
    [InlineData(nameof(AccGraphicsPage.PacketId), 0)]
    [InlineData(nameof(AccGraphicsPage.Status), 4)]
    [InlineData(nameof(AccGraphicsPage.Session), 8)]
    [InlineData(nameof(AccGraphicsPage.CompletedLaps), 132)]
    [InlineData(nameof(AccGraphicsPage.DistanceTraveled), 156)]
    [InlineData(nameof(AccGraphicsPage.SessionIndex), 1320)]
    public void GraphicsPage_KnownOffsets(string field, int expected) =>
        Assert.Equal(expected, (int)Marshal.OffsetOf<AccGraphicsPage>(field));

    [Theory]
    [InlineData(nameof(AccStaticPage.SmVersion), 0)]
    [InlineData(nameof(AccStaticPage.NumberOfSessions), 60)]
    [InlineData(nameof(AccStaticPage.CarModel), 68)]
    [InlineData(nameof(AccStaticPage.Track), 134)]
    public void StaticPage_KnownOffsets(string field, int expected) =>
        Assert.Equal(expected, (int)Marshal.OffsetOf<AccStaticPage>(field));

    [Fact]
    public void PhysicsPage_StructSizeCoversAllDeclaredFields()
    {
        var size = Marshal.SizeOf<AccPhysicsPage>();
        var lastFieldOffset = (int)Marshal.OffsetOf<AccPhysicsPage>(nameof(AccPhysicsPage.AbsVibration));
        Assert.True(size >= lastFieldOffset + sizeof(float), $"size {size} too small for last field at {lastFieldOffset}");
    }

    [Fact]
    public void GraphicsPage_StructSizeCoversAllDeclaredFields()
    {
        var size = Marshal.SizeOf<AccGraphicsPage>();
        var lastFieldOffset = (int)Marshal.OffsetOf<AccGraphicsPage>(nameof(AccGraphicsPage.MissingMandatoryPits));
        Assert.True(size >= lastFieldOffset + sizeof(int), $"size {size} too small for last field at {lastFieldOffset}");
    }
}
