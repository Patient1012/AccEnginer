using System.Globalization;
using System.IO;
using System.Text.Json;
using AccEngineer.AI;
using AccEngineer.UI.Themes;

namespace AccEngineer.UI.Services;

/// <summary>
/// User-configurable settings. Persisted as JSON under
/// <c>%AppData%\AccEngineer\settings.json</c> so operator values stay out of the
/// source tree (roadmap §14). AI configuration is stored for convenience;
/// provider keys are never persisted and are read from environment variables.
/// </summary>
public sealed class AppSettings
{
    public int PollIntervalMs { get; set; } = 50;

    public string RecordingsDirectory { get; set; } =
        Path.Combine(Environment.CurrentDirectory, "recordings");

    public string ReportsDirectory { get; set; } =
        Path.Combine(Environment.CurrentDirectory, "reports");

    /// <summary>
    /// Id of the selected UI theme (the <see cref="AppTheme"/> name). Unknown or missing
    /// values resolve to the default theme, so an old or hand-edited settings file can
    /// never leave the application unstyled.
    /// </summary>
    public string Theme { get; set; } = nameof(AppTheme.Default);

    public string? AiProvider { get; set; }

    public string? AiModel { get; set; }

    public string? AiEndpoint { get; set; }

    /// <summary>
    /// When true the app runs on built-in generated telemetry instead of ACC shared
    /// memory, so the whole pipeline can be exercised without a simulator. Restored
    /// at startup and applied immediately when changed in Settings.
    /// </summary>
    public bool TestDataMode { get; set; }

    public double? FuelCalculatorLastReserveLitres { get; set; }
    public double? FuelCalculatorLastSafetyMarginLitres { get; set; }
    public double? FuelCalculatorLastSafetyMarginPercent { get; set; }
    public string? FuelCalculatorSafetyMarginMode { get; set; } = "Litres";
    public double? FuelCalculatorManualConsumptionPerLap { get; set; }
    public double? FuelCalculatorManualRemainingLaps { get; set; }
    public int? FuelCalculatorStintLaps { get; set; }
    public bool FuelCalculatorPreferTimeLimitedEstimate { get; set; }

    /// <summary>
    /// Whether to connect to the ACC UDP Broadcasting API. Competitor names, race
    /// numbers, teams and official gaps exist only there (shared memory has none),
    /// so the standings/relative panels stay unavailable until this is enabled and
    /// ACC's <c>broadcasting.json</c> is configured to match the port/password below.
    /// </summary>
    public bool BroadcastingEnabled { get; set; }

    /// <summary>Must match <c>udpListenerPort</c> in ACC's <c>broadcasting.json</c>.</summary>
    public int BroadcastingPort { get; set; } = 9000;

    /// <summary>Must match <c>connectionPassword</c> in ACC's <c>broadcasting.json</c>.</summary>
    public string BroadcastingPassword { get; set; } = "";

    public bool IsAiConfigured =>
        !string.IsNullOrWhiteSpace(AiModel) &&
        AnalysisProviderFactory.ResolveEndpoint(new AiProviderOptions
        {
            Provider = AiProvider,
            Endpoint = AiEndpoint
        }) is not null;

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
                    loaded.BroadcastingPort = Math.Clamp(loaded.BroadcastingPort, 1, 65535);
                    // A missing, renamed or hand-edited theme id falls back to the default
                    // rather than failing to load or leaving the UI unstyled.
                    if (!ThemeCatalog.TryResolve(loaded.Theme, out var theme))
                    {
                        loaded.Theme = theme.Id.ToString();
                    }

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
