using System.IO;
using System.Text.Json;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Overlay;

/// <summary>
/// Loads and saves the overlay document as versioned JSON next to the app settings
/// (<c>%AppData%\AccEngineer\overlay.json</c>). Older single-layout files (v1) are
/// migrated into the default profile/layout. A missing or corrupt file falls back to
/// defaults so the overlay can always start.
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

    public OverlayLayoutDocument Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var document = Deserialize(json);
                if (document is not null) return Normalize(document);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Fall back to defaults on a corrupt or unreadable layout file.
        }

        return OverlayLayoutDocument.CreateDefault();
    }

    public void Save(OverlayLayoutDocument document)
    {
        // Never let a non-finite value (e.g. a window's NaN Left before first layout)
        // reach the serializer: System.Text.Json refuses to write NaN/Infinity.
        Sanitize(document);

        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(FilePath, JsonSerializer.Serialize(document, JsonOptions));
    }

    /// <summary>Clamps every panel and appearance in the document to serializable ranges.</summary>
    private static void Sanitize(OverlayLayoutDocument document)
    {
        foreach (var profile in document.Profiles.Values)
        {
            foreach (var layout in profile.Layouts.Values)
            {
                layout.Appearance.Sanitize();
                foreach (var panel in layout.Panels) panel.Sanitize();
            }
        }
    }

    /// <summary>Reads either a v2 document or a legacy v1 single layout.</summary>
    private static OverlayLayoutDocument? Deserialize(string json)
    {
        using var probe = JsonDocument.Parse(json);
        if (probe.RootElement.ValueKind != JsonValueKind.Object) return null;

        // v2 documents have a "profiles" object; v1 files were a bare layout.
        if (probe.RootElement.TryGetProperty("profiles", out _))
        {
            return JsonSerializer.Deserialize<OverlayLayoutDocument>(json, JsonOptions);
        }

        var legacy = JsonSerializer.Deserialize<OverlayLayout>(json, JsonOptions);
        var document = OverlayLayoutDocument.CreateDefault();
        document.Profiles[OverlayLayoutDocument.DefaultProfileName].Layouts[OverlayLayoutDocument.DefaultLayoutName] =
            legacy ?? new OverlayLayout();
        return document;
    }

    /// <summary>Guarantees the document always has a valid active profile and layout.</summary>
    private static OverlayLayoutDocument Normalize(OverlayLayoutDocument document)
    {
        var originalVersion = document.SchemaVersion;

        if (document.Profiles.Count == 0)
        {
            document.Profiles[OverlayLayoutDocument.DefaultProfileName] = new OverlayLayoutProfile
            {
                Simulator = OverlayLayoutDocument.DefaultProfileName
            };
        }

        if (!document.Profiles.TryGetValue(document.ActiveProfile, out var profile))
        {
            document.ActiveProfile = document.Profiles.Keys.First();
            profile = document.Profiles[document.ActiveProfile];
        }

        if (profile.Layouts.Count == 0)
        {
            profile.Layouts[OverlayLayoutDocument.DefaultLayoutName] = new OverlayLayout();
        }

        if (string.IsNullOrEmpty(profile.ActiveLayout) || !profile.Layouts.ContainsKey(profile.ActiveLayout))
        {
            profile.ActiveLayout = profile.Layouts.Keys.First();
        }

        MigrateToV3(document, originalVersion);

        return document;
    }

    /// <summary>
    /// v3 reshaped the inputs panel from a tall portrait stack to a landscape strip, so
    /// any previously saved inputs geometry is re-based to the new default size. Other
    /// panels and the user's placement of them are left untouched.
    /// </summary>
    private static void MigrateToV3(OverlayLayoutDocument document, int originalVersion)
    {
        if (originalVersion >= OverlayLayoutSchema.Version) return;

        foreach (var profile in document.Profiles.Values)
        {
            foreach (var layout in profile.Layouts.Values)
            {
                var inputs = layout.Panels.FirstOrDefault(
                    p => string.Equals(p.Id, "inputs", StringComparison.Ordinal));
                if (inputs is not null)
                {
                    inputs.Width = OverlayLayoutSchema.InputsDefaultWidth;
                    inputs.Height = OverlayLayoutSchema.InputsDefaultHeight;
                }

                layout.SchemaVersion = OverlayLayoutSchema.Version;
            }
        }

        document.SchemaVersion = OverlayLayoutSchema.Version;
    }
}
