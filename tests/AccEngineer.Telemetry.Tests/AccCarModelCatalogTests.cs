using AccEngineer.Telemetry.Broadcasting;

namespace AccEngineer.Telemetry.Tests;

/// <summary>Vehicle-class mapping from ACC car model ids (Kunos Server Admin Handbook table).</summary>
public sealed class AccCarModelCatalogTests
{
    [Theory]
    [InlineData(0, AccCarClass.Gt3)]    // Porsche 991 GT3 R
    [InlineData(8, AccCarClass.Gt3)]    // Aston V8 Vantage GT3
    [InlineData(9, AccCarClass.Gtc)]    // Porsche 911 GT3 Cup
    [InlineData(10, AccCarClass.Gt3)]   // Ferrari 488 GT3
    [InlineData(26, AccCarClass.Tcx)]   // Abarth 500
    [InlineData(27, AccCarClass.Tcx)]   // Alpine A110
    [InlineData(50, AccCarClass.Gt4)]   // Aston Martin V8 Vantage GT4
    [InlineData(53, AccCarClass.Gt4)]   // Maserati MC4 GT4
    [InlineData(55, AccCarClass.Gt4)]   // BMW M4 GT4
    [InlineData(61, AccCarClass.Gt4)]   // Toyota GR Supra GT4
    [InlineData(80, AccCarClass.Gt2)]   // Porsche 911 GT2 RS CS Evo
    [InlineData(86, AccCarClass.Gt2)]   // Maserati MC20 GT2
    public void ClassOf_MapsKnownModelIds(int modelId, AccCarClass expected) =>
        Assert.Equal(expected, AccCarModelCatalog.ClassOf(modelId));

    [Theory]
    [InlineData(54)]  // reserved gap in the GT4 range
    [InlineData(62)]  // above the last GT4 id
    [InlineData(79)]  // just below the GT2 block
    [InlineData(81)]
    [InlineData(87)]
    [InlineData(255)]
    public void ClassOf_LeavesUnlistedIdsUnknown(int modelId) =>
        Assert.Equal(AccCarClass.Unknown, AccCarModelCatalog.ClassOf(modelId));
}
