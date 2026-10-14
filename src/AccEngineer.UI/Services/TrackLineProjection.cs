using AccEngineer.Telemetry;

namespace AccEngineer.UI.Services;

/// <summary>A point on the player's recent driven line, with its known track progress.</summary>
public readonly record struct TrackLineVertex(double X, double Z, double? Progress);

/// <summary>
/// Builds a polyline from a window of recent samples and projects arbitrary world
/// positions onto it. ACC exposes no opponent timing, so projecting other cars onto
/// the player's line is how the app estimates running order and gaps. Shared by the
/// pit-wall and the overlay so both agree on the same estimate.
/// </summary>
public static class TrackLineProjection
{
    /// <summary>
    /// Builds a line from recent samples. Missing ACC progress for a vertex falls back
    /// to cumulative path length so the ordering stays meaningful and in real metres.
    /// </summary>
    public static List<TrackLineVertex> BuildLine(IReadOnlyList<TelemetrySample> recent)
    {
        var line = new List<TrackLineVertex>(recent.Count);
        var pathMeters = 0.0;
        for (var i = 0; i < recent.Count; i++)
        {
            var sample = recent[i];
            if (sample.PositionXMeters is not { } x || sample.PositionZMeters is not { } z) continue;
            if (line.Count > 0)
            {
                var previous = line[^1];
                pathMeters += Math.Sqrt(((x - previous.X) * (x - previous.X)) + ((z - previous.Z) * (z - previous.Z)));
            }

            line.Add(new TrackLineVertex(x, z, sample.TrackProgressMeters is { } progress
                ? progress
                : line.Count > 0 ? pathMeters : 0));
        }

        return line;
    }

    /// <summary>Progress of the nearest point on the line, or null for an empty line.</summary>
    public static double? ProjectToLine(float x, float z, List<TrackLineVertex> line)
    {
        double? best = null;
        var bestSquared = double.MaxValue;
        for (var i = 0; i < line.Count; i++)
        {
            var vertex = line[i];
            var dx = vertex.X - x;
            var dz = vertex.Z - z;
            var squared = (dx * dx) + (dz * dz);
            if (squared < bestSquared)
            {
                bestSquared = squared;
                best = vertex.Progress;
            }
        }

        return best;
    }
}
