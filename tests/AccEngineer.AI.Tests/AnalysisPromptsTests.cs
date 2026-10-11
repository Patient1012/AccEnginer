using AccEngineer.AI;

namespace AccEngineer.AI.Tests;

public class AnalysisPromptsTests
{
    [Fact]
    public void BuildUserPrompt_IncludesFactsAndWarnings()
    {
        var request = new AnalysisRequest
        {
            Context = new AnalysisContext
            {
                Track = "Spa",
                Car = "Ferrari 488 GT3 Evo",
                SessionType = "Practice",
                LapNumber = 3,
                ReferenceLapNumber = 5,
                LapTimeSeconds = 131.42,
                ReferenceLapTimeSeconds = 130.98
            },
            EventCategory = "MidCorner",
            EventDescription = "Minimum speed lower by ~1.6 kph relative to reference at similar progress.",
            Metrics = new[]
            {
                new AnalysisMetric("Min speed delta", "-1.6") { Unit = "kph" },
                new AnalysisMetric("Steering peak", "0.48") { Unit = "norm" }
            },
            Samples = new[]
            {
                new SampleExcerpt { SampleId = 100, ProgressMeters = 1200, SpeedKph = 145.2, Throttle = 0.0, Brake = 0.0, Steering = 0.42 },
                new SampleExcerpt { SampleId = 101, ProgressMeters = 1210, SpeedKph = 143.8, Throttle = 0.0, Brake = 0.0, Steering = 0.46 },
                new SampleExcerpt { SampleId = 102, ProgressMeters = 1220, SpeedKph = 142.4, Throttle = 0.0, Brake = 0.0, Steering = 0.48 }
            },
            DataQualityWarnings = new[] { "Some samples missing steering input" },
            Question = "Explain with separation of observation and interpretation."
        };

        var prompt = AnalysisPrompts.BuildUserPrompt(request);
        Assert.Contains("METADATA", prompt);
        Assert.Contains("Spa", prompt);
        Assert.Contains("MidCorner", prompt);
        Assert.Contains("100, 1200, 145.2", prompt);
        Assert.Contains("DATA QUALITY WARNINGS", prompt);
        Assert.Contains("Some samples missing steering input", prompt);
    }
}
