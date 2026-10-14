namespace AccEngineer.UI.Services;

/// <summary>A point in map (drawing) space produced by <see cref="TrackMapCalibration"/>.</summary>
public readonly record struct MapCoordinate(double X, double Y);

/// <summary>
/// Pure world-to-map fit used by the track map. Given a set of world points (the
/// cached static track outline) and a target size, it computes a uniform scale and
/// offset so the outline is centred with padding - the same transform can then be
/// applied to every live car marker so they stay consistent.
///
/// Intentionally free of WPF types so the calibration can be unit tested.
/// </summary>
public readonly record struct TrackMapCalibration(double Scale, double OffsetX, double OffsetZ, double MinX, double MinZ)
{
    /// <summary>
    /// Builds a calibration from world points. Returns null when there is nothing to
    /// fit (no finite points) or the target size is degenerate.
    /// </summary>
    public static TrackMapCalibration? TryCreate(
        IEnumerable<(double X, double Z)> worldPoints,
        double width,
        double height,
        double padding = 8.0)
    {
        if (width <= 2 || height <= 2) return null;

        var minX = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var minZ = double.PositiveInfinity;
        var maxZ = double.NegativeInfinity;
        var any = false;

        foreach (var (x, z) in worldPoints)
        {
            if (double.IsNaN(x) || double.IsNaN(z) || double.IsInfinity(x) || double.IsInfinity(z)) continue;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (z < minZ) minZ = z;
            if (z > maxZ) maxZ = z;
            any = true;
        }

        if (!any) return null;

        // Keep a minimum span so a tiny or single-point set does not zoom to infinity.
        var spanX = Math.Max(50, maxX - minX);
        var spanZ = Math.Max(Math.Max(maxZ - minZ, 1), 1);

        var scale = Math.Min(
            (width - padding * 2) / spanX,
            (height - padding * 2) / spanZ);

        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0) scale = 1;

        var offsetX = (width - spanX * scale) / 2.0 - minX * scale;
        var offsetZ = (height - spanZ * scale) / 2.0 - minZ * scale;

        return new TrackMapCalibration(scale, offsetX, offsetZ, minX, minZ);
    }

    /// <summary>Projects a world (X, Z) position into map drawing space.</summary>
    public MapCoordinate Project(double x, double z) => new(x * Scale + OffsetX, z * Scale + OffsetZ);
}
