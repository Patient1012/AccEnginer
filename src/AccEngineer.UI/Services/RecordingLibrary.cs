using System.IO;
using AccEngineer.Storage;

namespace AccEngineer.UI.Services;

/// <summary>A recording on disk plus whatever session metadata could be read.</summary>
public sealed record RecordingInfo(
    string FilePath,
    SessionMetadata? Metadata,
    DateTimeOffset LastWriteUtc,
    long SizeBytes,
    string? Error)
{
    public string FileName => Path.GetFileName(FilePath);

    public bool IsReadable => Error is null;

    public string TrackText => Metadata?.TrackName ?? Display.Missing;

    public string CarText => Metadata?.CarName ?? Display.Missing;

    public string SessionTypeText => Metadata?.SessionType.ToString() ?? Display.Missing;

    public string StartedText => Metadata?.StartedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? Display.Missing;

    public string SizeText => SizeBytes < 1024 * 1024
        ? $"{SizeBytes / 1024.0:0} KB"
        : $"{SizeBytes / (1024.0 * 1024.0):0.0} MB";
}

/// <summary>Enumerates and loads JSONL recordings from the configured directory.</summary>
public sealed class RecordingLibrary
{
    private readonly AppSettings _settings;
    private readonly SessionRepository _repository = new();

    public RecordingLibrary(AppSettings settings) =>
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    public IReadOnlyList<RecordingInfo> List()
    {
        var directory = _settings.RecordingsDirectory;
        if (!Directory.Exists(directory)) return Array.Empty<RecordingInfo>();

        var results = new List<RecordingInfo>();
        foreach (var file in Directory.EnumerateFiles(directory, "*" + SessionPaths.FileExtension))
        {
            results.Add(Read(file));
        }

        return results
            .OrderByDescending(r => r.Metadata?.StartedAtUtc ?? r.LastWriteUtc)
            .ToArray();
    }

    public RecordingInfo Read(string path)
    {
        var info = new FileInfo(path);
        try
        {
            var metadata = _repository.ReadMetadata(path);
            return new RecordingInfo(path, metadata, info.LastWriteTimeUtc, info.Length, null);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return new RecordingInfo(path, null, info.LastWriteTimeUtc, info.Length, ex.Message);
        }
    }

    public SessionRecording Load(string path) => _repository.Load(path);

    public void Delete(string path) => File.Delete(path);
}
