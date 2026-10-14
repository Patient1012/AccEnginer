using System.IO;
using System.Text.Json;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Overlay;

/// <summary>
/// Loads and saves the overlay layout as versioned JSON next to the app settings
/// (<c>%AppData%\AccEngineer\overlay.json</c>). A missing or corrupt file falls back
/// to defaults so the overlay can always start.
/// </summary>
public sealed class OverlayLayoutStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OverlayLayoutStore(string? path = null)
    {
        FilePath = path ?? Path.Combine(AppSettings.SettingsDirectory, "overlay.json");
    }

    public string FilePath { get; }

    public OverlayLayout Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var layout = JsonSerializer.Deserialize<OverlayLayout>(json, JsonOptions);
                if (layout is not null)
                {
                    return layout;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Fall back to defaults on a corrupt or unreadable layout file.
        }

        return new OverlayLayout();
    }

    public void Save(OverlayLayout layout)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(FilePath, JsonSerializer.Serialize(layout, JsonOptions));
    }
}
