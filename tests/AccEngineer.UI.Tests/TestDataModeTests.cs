using System.Diagnostics;
using System.Text.Json;
using AccEngineer.Telemetry;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Tests;

/// <summary>
/// Integration checks that the application-wide test-data mode swaps the provider
/// feeding the shared live pipeline, that the active provider is reported honestly,
/// and that generated samples carry the TestData flag.
/// </summary>
public class TestDataModeTests
{
    [Fact]
    public void TogglingTestDataMode_UsesGeneratedProvider_AndFlagsSamples()
    {
        var settings = new AppSettings { PollIntervalMs = 5 };
        using var service = new LiveTelemetryService(new SessionEventLog(), settings);

        service.Start();
        Assert.False(service.IsTestData);

        service.SetTestDataMode(true);

        Assert.True(WaitUntil(
            () => service.IsTestData
                  && service.Connection == ConnectionState.TestData
                  && service.Latest?.Quality.HasFlag(DataQualityFlags.TestData) == true,
            TimeSpan.FromSeconds(5)));

        Assert.Equal(ConnectionState.TestData, service.Connection);
        Assert.True(service.Latest!.Quality.HasFlag(DataQualityFlags.TestData));

        service.SetTestDataMode(false);
        Assert.False(service.IsTestData);
    }

    [Fact]
    public void TestDataMode_RestoresFromSettings_AtStartup()
    {
        var settings = new AppSettings { PollIntervalMs = 5, TestDataMode = true };
        using var service = new LiveTelemetryService(new SessionEventLog(), settings);

        service.Start();

        Assert.True(WaitUntil(
            () => service.IsTestData
                  && service.Connection == ConnectionState.TestData
                  && service.Latest is not null,
            TimeSpan.FromSeconds(5)));
        Assert.Equal(ConnectionState.TestData, service.Connection);
    }

    [Fact]
    public void SetTestDataMode_WhenAlreadyOff_IsNoOp()
    {
        var settings = new AppSettings();
        using var service = new LiveTelemetryService(new SessionEventLog(), settings);

        service.SetTestDataMode(false);

        Assert.False(service.IsTestData);
    }

    [Fact]
    public void TestDataMode_SurvivesSettingsSerialization_RoundTrip()
    {
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var json = JsonSerializer.Serialize(new AppSettings { TestDataMode = true }, options);
        Assert.Contains("testDataMode", json, StringComparison.Ordinal);

        Assert.True(JsonSerializer.Deserialize<AppSettings>(json, options)!.TestDataMode);

        // A settings file written before this feature simply lacks the property and
        // must default to off (real ACC reader).
        Assert.False(JsonSerializer.Deserialize<AppSettings>("{}", options)!.TestDataMode);
    }

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < timeout)
        {
            if (condition()) return true;
            Thread.Sleep(20);
        }

        return condition();
    }
}
