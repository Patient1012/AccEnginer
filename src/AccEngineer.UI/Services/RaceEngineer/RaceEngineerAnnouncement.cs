namespace AccEngineer.UI.Services.RaceEngineer;

public enum AnnouncementPriority
{
    Routine = 0,
    Important = 1,
    Critical = 2
}

public class RaceEngineerAnnouncement
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Message { get; set; } = string.Empty;
    public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Routine;
    public string Category { get; set; } = "General";
    public bool IsSpoken { get; set; } = true;
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}
