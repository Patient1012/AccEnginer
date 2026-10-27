using AccEngineer.Telemetry;

namespace AccEngineer.UI.Services.RaceEngineer;

public interface IRaceEngineerService : IDisposable
{
    bool IsEnabled { get; set; }
    bool IsMuted { get; set; }
    bool IsPushToTalkHeld { get; set; }
    void Start();
    void Stop();
    void OnTelemetry(TelemetrySample sample);
    event EventHandler<RaceEngineerAnnouncement>? Announcement;
}
