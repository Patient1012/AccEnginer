using AccEngineer.Telemetry;

namespace AccEngineer.UI.Services.RaceEngineer;

public class TestScenarioRunner
{
    private readonly IRaceEngineerService _service;

    public TestScenarioRunner(IRaceEngineerService service)
    {
        _service = service;
    }

    public void RunFuelShortageScenario()
    {
        // Simulate fuel getting low
        var samples = new List<TelemetrySample>();
        // This would generate test samples
    }

    public void RunNormalSession()
    {
    }
}
