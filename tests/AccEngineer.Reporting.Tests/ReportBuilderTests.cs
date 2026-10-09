using System.Text.Json;
using AccEngineer.Analysis;

namespace AccEngineer.Reporting.Tests;

public class ReportBuilderTests
{
    private static LapReport BuildSampleReport()
    {
        var reference = Synth.Lap(1, 20.0, new[] { (0.0, 0.0), (500.0, 10.0), (1000.0, 20.0) });
        var slower = Synth.Lap(2, 20.5, new[] { (0.0, 0.0), (500.0, 10.05), (1000.0, 20.5) });
        var invalid = Synth.Lap(3, 25.0, new[] { (0.0, 0.0), (1000.0, 25.0) }, valid: false);

        var analysis = Synth.Analysis(reference, slower, invalid);
        var chosen = ReferenceLapSelector.SelectFastestValid(analysis.Laps)!;
        var comparison = new LapComparer().Compare(chosen, analysis.Laps);

        return new ReportBuilder().Build(analysis, comparison, Synth.T0);
    }

    [Fact]
    public void Build_ProducesSchemaVersionAndSessionDetails()
    {
        var report = BuildSampleReport();

        Assert.Equal(ReportSchema.Version, report.SchemaVersion);
        Assert.Equal("monza", report.Session.Track);
        Assert.Equal("porsche_991_gt3_r", report.Session.Car);
        Assert.Equal(3, report.Laps.Count);
        Assert.Equal(Synth.T0, report.GeneratedAtUtc);
    }

    [Fact]
    public void Build_InvalidLap_DowngradesDataQualityAndWarns()
    {
        var report = BuildSampleReport();

        Assert.Equal("warnings", report.DataQuality.Status);
        Assert.Contains(report.DataQuality.Warnings, w => w.Contains("Invalid or incomplete laps", StringComparison.Ordinal));
        Assert.False(report.Laps.Single(l => l.LapNumber == 3).IsValid);
    }

    [Fact]
    public void Build_ComparisonExcludesReferenceAndKeepsRegions()
    {
        var report = BuildSampleReport();

        Assert.NotNull(report.Comparison);
        Assert.Equal(1, report.Comparison!.ReferenceLapNumber);
        Assert.Equal(2, report.Comparison.Laps.Count);
        Assert.DoesNotContain(report.Comparison.Laps, l => l.LapNumber == 1);

        var lap2 = report.Comparison.Laps.Single(l => l.LapNumber == 2);
        Assert.Equal(0.5, lap2.TotalDeltaSeconds!.Value, 6);
        Assert.NotEmpty(lap2.Regions);
        Assert.Equal("loss", lap2.Regions[0].Type);
    }

    [Fact]
    public void Json_RoundTripsAndContainsSchemaVersion()
    {
        var report = BuildSampleReport();
        var exporter = new JsonReportExporter();

        var json = exporter.ToJson(report);
        Assert.Contains("\"schemaVersion\"", json);
        Assert.Contains("\"referenceLapNumber\"", json);

        var roundTrip = JsonSerializer.Deserialize<LapReport>(json, JsonReportExporter.Options)!;
        Assert.Equal(ReportSchema.Version, roundTrip.SchemaVersion);
        Assert.Equal(3, roundTrip.Laps.Count);
        Assert.NotNull(roundTrip.Comparison);
        Assert.Equal(1, roundTrip.Comparison!.ReferenceLapNumber);
        Assert.Equal(2, roundTrip.Comparison.Laps.Count);

        var slower = roundTrip.Comparison.Laps.Single(l => l.LapNumber == 2);
        Assert.NotEmpty(slower.Regions);
        Assert.Equal("loss", slower.Regions[0].Type);
    }

    [Fact]
    public void Markdown_ContainsLapsComparisonAndWarnings()
    {
        var report = BuildSampleReport();

        var markdown = new MarkdownReportExporter().ToMarkdown(report);

        Assert.Contains("# Lap report — monza (Practice)", markdown);
        Assert.Contains("| Lap | Valid | Complete |", markdown);
        Assert.Contains("| 3 | no | yes |", markdown);
        Assert.Contains("Comparison vs lap 1", markdown);
        Assert.Contains("Loss", markdown);
        Assert.Contains("Schema: v1", markdown);
    }
}
