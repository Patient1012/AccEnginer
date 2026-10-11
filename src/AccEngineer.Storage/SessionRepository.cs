using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace AccEngineer.Storage;

/// <summary>
/// Loads session recordings produced by <see cref="JsonlSessionRecorder"/>.
/// Supports both schema versions: v1 (<c>{"type":"sample","sample":{...}}</c>)
/// and v2 (compact sample objects with relative timestamps). gzip compression is
/// auto-detected by magic bytes, so uncompressed and compressed files load the
/// same way. Tolerant of a truncated final line (interrupted session), which is
/// counted in <see cref="SessionRecording.SkippedRecordCount"/> rather than
/// failing the load. During recording, a partially written gzip stream may be
/// unreadable until the recording is closed cleanly.
///
/// Note: entire recordings are loaded into memory. At the intended capture rates
/// this is small; streaming access can be added when multi-hour sessions or the
/// analysis engine need it.
/// </summary>
public sealed class SessionRepository
{
    /// <summary>Reads only the header record (cheap session listing).</summary>
    public SessionMetadata ReadMetadata(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var reader = OpenReader(path);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;

            var metadata = TryParseMetadata(line);
            if (metadata is not null) return metadata;
        }
        throw new InvalidDataException($"No session header found in recording: {path}");
    }

    public SessionRecording Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Recording not found: {path}", path);

        SessionMetadata? metadata = null;
        SessionEndInfo? end = null;
        var samples = new List<Telemetry.TelemetrySample>();
        var skipped = 0;

        using var reader = OpenReader(path);
        string? line;
        var lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (line.Length == 0) continue;

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var type = root.TryGetProperty("type", out var typeElement)
                    ? typeElement.GetString()
                    : null;

                switch (type)
                {
                    case "session":
                        metadata = root.GetProperty("metadata").Deserialize<SessionMetadata>(TelemetryJson.Options);
                        break;
                    case "sample":
                        var sample = root.TryGetProperty("sample", out _)
                            ? root.GetProperty("sample").Deserialize<Telemetry.TelemetrySample>(TelemetryJson.Options)
                            : root.Deserialize<CompactSample>(TelemetryJson.Options)?.ToFull(RequireMetadata(metadata, lineNumber, path));
                        if (sample is not null) samples.Add(sample);
                        break;
                    case "end":
                        end = root.GetProperty("end").Deserialize<SessionEndInfo>(TelemetryJson.Options);
                        break;
                    default:
                        skipped++;
                        break;
                }
            }
            catch (JsonException)
            {
                // Truncated or corrupt line: tolerate and count (expected for an
                // interrupted final write).
                skipped++;
            }
        }

        if (metadata is null)
            throw new InvalidDataException($"Recording has no session header (line {lineNumber}): {path}");

        return new SessionRecording
        {
            Metadata = metadata,
            Samples = samples,
            End = end,
            SkippedRecordCount = skipped
        };
    }

    /// <summary>
    /// Opens a recording for reading with <see cref="FileShare.ReadWrite"/> so a
    /// session can be inspected while it is still being recorded. gzip files are
    /// detected by their magic bytes and decompressed transparently.
    /// </summary>
    private static StreamReader OpenReader(string path)
    {
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (IsGzip(stream))
        {
            stream = new GZipStream(stream, CompressionMode.Decompress);
        }

        return new StreamReader(stream, Encoding.UTF8);
    }

    private static bool IsGzip(Stream stream)
    {
        Span<byte> magic = stackalloc byte[2];
        var read = stream.Read(magic);
        if (stream.CanSeek) stream.Position = 0;
        return read == 2 && magic[0] == 0x1F && magic[1] == 0x8B;
    }

    private static SessionMetadata RequireMetadata(SessionMetadata? metadata, int lineNumber, string path) =>
        metadata ?? throw new InvalidDataException($"Sample seen before session header (line {lineNumber}): {path}");

    private static SessionMetadata? TryParseMetadata(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var type) ||
                type.GetString() != "session" ||
                !root.TryGetProperty("metadata", out var metadata))
            {
                return null;
            }

            return metadata.Deserialize<SessionMetadata>(TelemetryJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
