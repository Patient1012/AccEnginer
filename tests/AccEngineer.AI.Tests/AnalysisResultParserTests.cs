using System.Net;
using System.Text;
using AccEngineer.AI;

namespace AccEngineer.AI.Tests;

public class AnalysisResultParserTests
{
    [Fact]
    public void Parse_Empty_ReturnsInvalid()
    {
        var result = AnalysisResultParser.Parse("");
        Assert.Equal(AnalysisOutcome.InvalidResponse, result.Outcome);
    }

    [Fact]
    public void Parse_MissingObservation_ReturnsInvalid()
    {
        var result = AnalysisResultParser.Parse("{\"findingType\":\"braking\"}");
        Assert.Equal(AnalysisOutcome.InvalidResponse, result.Outcome);
    }

    [Fact]
    public void Parse_ValidJson_ReturnsSuccess()
    {
        var json = "{\"findingType\":\"braking\",\"observation\":\"Later threshold\",\"evidence\":[\"brake>0.2 over 40m\"],\"interpretation\":\"Trail braking\",\"confidence\":0.8,\"suggestedTest\":\"Repeat with less brake\",\"limitations\":[\"no steering trace\"]}";
        var result = AnalysisResultParser.Parse(json, "test");
        Assert.True(result.IsSuccess);
        Assert.Equal(0.8, result.Confidence);
        Assert.Equal("braking", result.FindingType);
        Assert.Contains("trail braking", result.Interpretation?.ToLowerInvariant() ?? string.Empty);
    }

    [Fact]
    public void Parse_CodeFenceStripsFences()
    {
        var fenced = "```json\n{\"observation\":\"RPM spike\",\"confidence\":0.6}\n```";
        var result = AnalysisResultParser.Parse(fenced);
        Assert.True(result.IsSuccess);
        Assert.Equal("RPM spike", result.Observation);
    }

    [Fact]
    public void Parse_ConfidenceClamped()
    {
        var result = AnalysisResultParser.Parse("{\"observation\":\"O\",\"confidence\":2.0}");
        Assert.True(result.IsSuccess);
        Assert.Equal(1.0, result.Confidence);
    }
}
