using System.Globalization;
using System.Text;

namespace AccEngineer.Storage;

/// <summary>File naming and directory rules for session recordings.</summary>
public static class SessionPaths
{
    public const string DefaultDirectoryName = "recordings";

    public const string FileExtension = ".jsonl";

    /// <summary>Builds <c>yyyyMMdd-HHmmss-{sessionId}.jsonl</c> for the given session.</summary>
    public static string BuildFileName(DateTimeOffset startedAtUtc, string sessionId)
    {
        var stamp = startedAtUtc.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return $"{stamp}-{Sanitize(sessionId)}{FileExtension}";
    }

    /// <summary>Replaces characters that are invalid in file names.</summary>
    public static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }
        return builder.ToString();
    }

    /// <summary>Resolves a unique path, appending <c>-2</c>, <c>-3</c>, ... if the name is taken.</summary>
    public static string ResolveUniquePath(string directory, DateTimeOffset startedAtUtc, string sessionId)
    {
        var baseName = BuildFileName(startedAtUtc, sessionId);
        var candidate = Path.Combine(directory, baseName);
        if (!File.Exists(candidate)) return candidate;

        var stem = Path.GetFileNameWithoutExtension(baseName);
        var extension = Path.GetExtension(baseName);
        for (var suffix = 2; ; suffix++)
        {
            candidate = Path.Combine(directory, $"{stem}-{suffix}{extension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
