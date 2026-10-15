using System.Text.Json;

namespace AccEngineer.Analysis.Corners;

/// <summary>Corners for one track, loaded from a manual configuration.</summary>
public sealed record TrackCornerDefinition
{
    public required string Track { get; init; }

    /// <summary>Corners ordered by number.</summary>
    public required IReadOnlyList<Corner> Corners { get; init; }
}

/// <summary>
/// Loads manually-defined corner configurations from JSON so the engine has reliable
/// boundaries before automatic detection exists. No file I/O happens unless
/// <see cref="LoadFromFile"/> is called.
/// </summary>
public sealed class CornerDefinitionLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public TrackCornerDefinition LoadFromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var dto = JsonSerializer.Deserialize<CornerDefinitionDto>(json, Options)
                  ?? throw new InvalidOperationException("Corner definition JSON deserialised to null.");

        if (string.IsNullOrWhiteSpace(dto.Track))
        {
            throw new InvalidOperationException("Corner definition must declare a track name.");
        }

        var corners = dto.Corners
            .Select(MapCorner)
            .OrderBy(c => c.Number)
            .ToArray();

        return new TrackCornerDefinition
        {
            Track = dto.Track,
            Corners = corners
        };
    }

    public TrackCornerDefinition LoadFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return LoadFromJson(File.ReadAllText(path));
    }

    private static Corner MapCorner(CornerDto dto)
    {
        if (dto.Apex is null)
        {
            throw new InvalidOperationException($"Corner {dto.Number} is missing an apex distance.");
        }

        return new Corner
        {
            Number = dto.Number,
            Name = dto.Name,
            StartDistanceMeters = dto.Start,
            EntryDistanceMeters = dto.Entry,
            TurnInDistanceMeters = dto.TurnIn,
            ApexDistanceMeters = dto.Apex.Value,
            ExitDistanceMeters = dto.Exit,
            EndDistanceMeters = dto.End
        };
    }

    private sealed class CornerDefinitionDto
    {
        public string Track { get; set; } = string.Empty;
        public List<CornerDto> Corners { get; set; } = new();
    }

    private sealed class CornerDto
    {
        public int Number { get; set; }
        public string? Name { get; set; }
        public double? Start { get; set; }
        public double? Entry { get; set; }
        public double? TurnIn { get; set; }
        public double? Apex { get; set; }
        public double? Exit { get; set; }
        public double? End { get; set; }
    }
}
