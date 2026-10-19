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

    [Theory]
    [InlineData(0, AccCarBrand.Porsche)]        // Porsche 991 GT3 R
    [InlineData(2, AccCarBrand.Ferrari)]        // Ferrari 488 GT3
    [InlineData(6, AccCarBrand.Nissan)]         // Nissan GT-R Nismo 2018
    [InlineData(8, AccCarBrand.Bentley)]        // Bentley Continental GT3 2018
    [InlineData(12, AccCarBrand.AstonMartin)]   // Aston Martin V12 Vantage
    [InlineData(26, AccCarBrand.Ferrari)]       // Ferrari 488 Challenge Evo
    [InlineData(27, AccCarBrand.BMW)]           // BMW M2 CS Racing
    [InlineData(30, AccCarBrand.BMW)]           // BMW M4 GT3
    [InlineData(36, AccCarBrand.Ford)]          // Ford Mustang GT3
    [InlineData(50, AccCarBrand.Alpine)]        // Alpine A110 GT4
    [InlineData(55, AccCarBrand.Chevrolet)]     // Chevrolet Camaro GT4
    [InlineData(58, AccCarBrand.Maserati)]      // Maserati MC GT4
    [InlineData(61, AccCarBrand.Porsche)]       // Porsche 718 Cayman GT4
    [InlineData(80, AccCarBrand.Audi)]          // Audi R8 LMS GT2
    [InlineData(83, AccCarBrand.Maserati)]      // Maserati MC20 GT2
    [InlineData(84, AccCarBrand.Mercedes)]      // Mercedes AMG GT2
    [InlineData(86, AccCarBrand.Porsche)]       // Porsche 935
    public void BrandOf_MapsKnownModelIds(int modelId, AccCarBrand expected) =>
        Assert.Equal(expected, AccCarBrandCatalog.BrandOf(modelId));

    [Theory]
    [InlineData(54)]
    [InlineData(87)]
    [InlineData(255)]
    public void BrandOf_LeavesUnlistedIdsUnknown(int modelId) =>
        Assert.Equal(AccCarBrand.Unknown, AccCarBrandCatalog.BrandOf(modelId));

    [Fact]
    public void BrandOf_AgreesWithClassCatalog_AcrossDocumentedRanges()
    {
        // Every id the class catalog classifies must also resolve to a real brand,
        // so the two tables can never drift apart on the documented id space.
        for (var id = 0; id <= 86; id++)
        {
            var carClass = AccCarModelCatalog.ClassOf(id);
            if (carClass == AccCarClass.Unknown) continue;

            Assert.NotEqual(AccCarBrand.Unknown, AccCarBrandCatalog.BrandOf(id));
        }
    }

    [Fact]
    public void Code_ReturnsShortUppercaseMonograms()
    {
        Assert.Equal("POR", AccCarBrandCatalog.Code(AccCarBrand.Porsche));
        Assert.Equal("FER", AccCarBrandCatalog.Code(AccCarBrand.Ferrari));
        Assert.Equal("AMR", AccCarBrandCatalog.Code(AccCarBrand.AstonMartin));
        Assert.Equal("", AccCarBrandCatalog.Code(AccCarBrand.Unknown));
    }
}
