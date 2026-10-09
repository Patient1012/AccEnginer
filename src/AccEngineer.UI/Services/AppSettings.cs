using System.Globalization;
using System.IO;
using System.Text.Json;

namespace AccEngineer.UI.Services;

/// <summary>
/// User-configurable settings. Persisted as JSON under
/// <c>%AppData%\AccEngineer\settings.json</c> so operator values stay out of the
/// source tree (roadmap §14). No secrets are stored here; AI provider keys are
/// not part of this milestone.
/// </summary>
public sealed class AppSettings
{
    public int PollIntervalMs { get; set; } = 50;

    public string RecordingsDirectory { get; set; } =
        Path.Combine(Environment.CurrentDirectory, "recordings");

    public string ReportsDirectory { get; set; } =
        Path.Combine(Environment.CurrentDirectory, "reports");

    public string AccentColor { get; set; } = "#B7F34A";

    public string? AiProvider { get; set; }

    public string? AiModel { get; set; }

    public string? AiEndpoint { get; set; }

    public bool IsAiConfigured =>
        !string.IsNullOrWhiteSpace(AiProvider) && !string.IsNullOrWhiteSpace(AiEndpoint);

    public static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AccEngineer");

    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    loaded.PollIntervalMs = Math.Clamp(loaded.PollIntervalMs, 10, 2000);
                    return loaded;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Fall back to defaults on a corrupt or unreadable settings file.
        }

        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
