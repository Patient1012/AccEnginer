namespace AccEngineer.UI.Services.RaceEngineer;

public class RaceEngineerOptions
{
    public bool Enabled { get; set; } = true;
    public bool Muted { get; set; }
    public bool EmergencyOverrideMute { get; set; } = true;
    public double SpeechVolume { get; set; } = 1.0;
    public string PushToTalkKey { get; set; } = "LeftAlt";
    public bool EnableFuel { get; set; } = true;
    public bool EnablePace { get; set; } = true;
    public bool EnableTyres { get; set; } = true;
    public bool EnableSession { get; set; } = true;
    public bool EnableSetup { get; set; } = true;
}
