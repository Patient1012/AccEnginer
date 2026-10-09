using System.Text.Json;
using System.Text.Json.Serialization;

namespace AccEngineer.Storage;

/// <summary>
/// Shared JSON settings for session recordings and, later, exported reports.
/// Nullable fields are omitted entirely (missing means unknown — never written
/// as zero), enums and data-quality flags are written as names for readability.
/// </summary>
internal static class TelemetryJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
