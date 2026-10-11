using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace AccEngineer.Storage;

/// <summary>Compression applied to the recording stream. gzip is auto-detected when loading.</summary>
public enum RecordingCompression
{
    None = 0,
    Gzip = 1
}

public sealed class SessionRecorderOptions
{
    /// <summary>Directory that receives recording files.</summary>
    public string Directory { get; init; } =
        Path.Combine(Environment.CurrentDirectory, SessionPaths.DefaultDirectoryName);

    /// <summary>Bounded queue size between the capture thread and the writer thread.</summary>
    public int QueueCapacity { get; init; } = 20_000;

    /// <summary>How often buffered samples are flushed to disk.</summary>
    public TimeSpan FlushInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Compression applied around the recording stream (default: gzip).</summary>
    public RecordingCompression Compression { get; init; } = RecordingCompression.Gzip;
}

/// <summary>
/// Append-only session recorder. A recording is a text stream of records —
/// one <c>{"type":"session",...}</c> header, one compact sample line per telemetry
/// sample in capture order, and one <c>{"type":"end",...}</c> summary written when
/// the recording is closed cleanly. Samples are encoded with the compact
/// schema-version-2 representation (relative timestamps, integer enums) and the
/// stream is gzip-compressed by default.
///
/// A dedicated writer thread drains a bounded queue, so capture is never blocked:
/// when the queue is full, samples are dropped and counted rather than stalling
/// the reader. A header is written immediately, so even a hard-killed recording
/// still carries session metadata.
/// </summary>
public sealed class JsonlSessionRecorder : ISessionRecorder
{
    private readonly SessionRecorderOptions _options;
    private readonly BlockingCollection<Telemetry.TelemetrySample> _queue;
    private readonly StreamWriter _writer;
    private readonly Thread _writerThread;
    private readonly Stopwatch _sinceFlush = Stopwatch.StartNew();

    private readonly object _lifecycle = new();
    private Exception? _fault;
    private int _written;
    private int _dropped;
    private bool _completed;
    private bool _disposed;

    public JsonlSessionRecorder(SessionRecorderOptions options, SessionMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metadata);
        if (string.IsNullOrWhiteSpace(metadata.SessionId))
            throw new ArgumentException("SessionId must not be empty.", nameof(metadata));

        _options = options;
        Metadata = metadata;

        System.IO.Directory.CreateDirectory(options.Directory);
        FilePath = SessionPaths.ResolveUniquePath(options.Directory, metadata.StartedAtUtc, metadata.SessionId);

        _queue = new BlockingCollection<Telemetry.TelemetrySample>(options.QueueCapacity);

        var fileStream = new FileStream(
            FilePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 1 << 16);
        Stream baseStream = fileStream;
        if (options.Compression == RecordingCompression.Gzip)
        {
            baseStream = new GZipStream(baseStream, CompressionLevel.Optimal);
        }

        _writer = new StreamWriter(baseStream);
        _writer.NewLine = "\n";

        WriteHeader();

        _writerThread = new Thread(WriteLoop)
        {
            IsBackground = true,
            Name = "session-recorder-writer"
        };
        _writerThread.Start();
    }

    public string FilePath { get; }

    public SessionMetadata Metadata { get; }

    public int WrittenSampleCount => Volatile.Read(ref _written);

    public int DroppedSampleCount => Volatile.Read(ref _dropped);

    /// <summary>Non-null when the writer thread failed (e.g. disk full).</summary>
    public Exception? Fault => Volatile.Read(ref _fault);

    public void Record(Telemetry.TelemetrySample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        lock (_lifecycle)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(JsonlSessionRecorder));
            if (_completed)
                throw new InvalidOperationException("The recording has already been completed.");
        }

        if (Fault is not null || !_queue.TryAdd(sample))
        {
            Interlocked.Increment(ref _dropped);
        }
    }

    public void Complete()
    {
        lock (_lifecycle)
        {
            if (_completed) return;
            _completed = true;
        }

        _queue.CompleteAdding();
        _writerThread.Join();

        WriteEndRecord();

        // Drain anything the writer thread discarded (it only discards on fault).
        while (_queue.TryTake(out _)) Interlocked.Increment(ref _dropped);
        _queue.Dispose();

        _writer.Flush();
        _writer.Dispose();
    }

    public void Dispose()
    {
        lock (_lifecycle)
        {
            if (_disposed) return;
            _disposed = true;
        }
        Complete();
    }

    private void WriteLoop()
    {
        try
        {
            foreach (var sample in _queue.GetConsumingEnumerable())
            {
                WriteSample(sample);
                if (_sinceFlush.Elapsed >= _options.FlushInterval)
                {
                    _writer.Flush();
                    _sinceFlush.Restart();
                }
            }
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _fault, ex);
            // Drain and discard so producers never block; the fault is reported
            // through Fault and the end record.
            try
            {
                while (_queue.TryTake(out _)) { }
            }
            catch (ObjectDisposedException)
            {
                // Queue already disposed during shutdown; nothing left to drain.
            }
        }
    }

    private void WriteHeader()
    {
        _writer.Write("{\"type\":\"session\",\"metadata\":");
        _writer.Write(JsonSerializer.Serialize(Metadata, TelemetryJson.Options));
        _writer.Write("}\n");
        _writer.Flush();
    }

    private void WriteSample(Telemetry.TelemetrySample sample)
    {
        var body = JsonSerializer.Serialize(CompactSample.From(sample, Metadata.StartedAtUtc), TelemetryJson.Options);
        var inner = body.Length >= 2 ? body[1..^1] : body;
        _writer.Write("{\"type\":\"sample\"," + inner + "}\n");
        Interlocked.Increment(ref _written);
    }

    private void WriteEndRecord()
    {
        var end = new SessionEndInfo
        {
            EndedAtUtc = DateTimeOffset.UtcNow,
            SampleCount = WrittenSampleCount,
            DroppedSampleCount = DroppedSampleCount,
            Completed = Fault is null
        };
        _writer.Write("{\"type\":\"end\",\"end\":");
        _writer.Write(JsonSerializer.Serialize(end, TelemetryJson.Options));
        _writer.Write("}\n");
    }
}
