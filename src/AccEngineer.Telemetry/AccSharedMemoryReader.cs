using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace AccEngineer.Telemetry;

/// <summary>
/// Options for <see cref="AccSharedMemoryReader"/>. Defaults are chosen for a
/// ~50-100 ms console polling loop and should be tuned after measuring the real
/// interface (see roadmap: make sampling rates configurable after measuring).
/// </summary>
public sealed class AccReaderOptions
{
    /// <summary>No new physics packet for this long marks the next sample Stale.</summary>
    public TimeSpan StaleThreshold { get; init; } = TimeSpan.FromMilliseconds(1000);

    /// <summary>No new physics packet for this long drops the connection; the reader then reconnects automatically.</summary>
    public TimeSpan DisconnectThreshold { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Minimum delay between connection attempts while ACC is not running.</summary>
    public TimeSpan ReconnectRetryInterval { get; init; } = TimeSpan.FromSeconds(2);
}

/// <summary>
/// Reads ACC telemetry from the three Windows shared-memory regions
/// (<c>Local\acpmf_physics</c>, <c>Local\acpmf_graphics</c>, <c>Local\acpmf_static</c>).
///
/// Behaviour:
/// <list type="bullet">
/// <item>ReadLatest returns a sample only when the physics packet id advances,
/// one final sample flagged <see cref="DataQualityFlags.Stale"/> when the stale
/// threshold is crossed, and otherwise null (no new data).</item>
/// <item>When packets stop advancing past the disconnect threshold the
/// connection is dropped and re-established automatically on later calls.</item>
/// <item>The static page is re-read on every call, so session metadata stays
/// fresh across session changes.</item>
/// </list>
///
/// Intended for single-threaded polling by one consumer.
/// </summary>
public sealed class AccSharedMemoryReader : ITrackTelemetryReader
{
    internal const string PhysicsMapName = @"Local\acpmf_physics";
    internal const string GraphicsMapName = @"Local\acpmf_graphics";
    internal const string StaticMapName = @"Local\acpmf_static";

    private readonly AccReaderOptions _options;
    private readonly AccSampleBuilder _builder = new();

    private MemoryMappedFile? _physicsFile;
    private MemoryMappedFile? _graphicsFile;
    private MemoryMappedFile? _staticFile;
    private MemoryMappedViewAccessor? _physicsView;
    private MemoryMappedViewAccessor? _graphicsView;
    private MemoryMappedViewAccessor? _staticView;

    private byte[] _physicsBuffer = Array.Empty<byte>();
    private byte[] _graphicsBuffer = Array.Empty<byte>();
    private byte[] _staticBuffer = Array.Empty<byte>();

    private int? _lastPhysicsPacketId;
    private DateTimeOffset _lastPacketChangeUtc;
    private bool _staleReported;
    private DateTimeOffset _nextRetryUtc;

    public AccSharedMemoryReader(AccReaderOptions? options = null)
    {
        _options = options ?? new AccReaderOptions();
    }

    public bool IsConnected =>
        _physicsView != null && _graphicsView != null && _staticView != null;

    public TelemetrySample? ReadLatest()
    {
        var now = DateTimeOffset.UtcNow;

        if (!IsConnected)
        {
            if (now < _nextRetryUtc) return null;
            if (!TryConnect())
            {
                _nextRetryUtc = now + _options.ReconnectRetryInterval;
                return null;
            }
        }

        if (!TryReadPages(out var physics, out var graphics, out var statics))
        {
            Disconnect();
            _nextRetryUtc = now + _options.ReconnectRetryInterval;
            return null;
        }

        if (_lastPhysicsPacketId is null)
        {
            // First packet after (re)connect: start tracking, no sample yet so we
            // never report data captured before the tracking baseline exists.
            _lastPhysicsPacketId = physics.PacketId;
            _lastPacketChangeUtc = now;
            _staleReported = false;
            return null;
        }

        if (physics.PacketId == _lastPhysicsPacketId.Value)
        {
            var age = now - _lastPacketChangeUtc;

            if (age >= _options.DisconnectThreshold)
            {
                // ACC exited or froze while we held handles: drop and retry.
                Disconnect();
                _nextRetryUtc = now + _options.ReconnectRetryInterval;
                return null;
            }

            if (age >= _options.StaleThreshold && !_staleReported)
            {
                _staleReported = true;
                return _builder.Build(physics, graphics, statics, now, Stopwatch.GetTimestamp())
                    .WithQuality(DataQualityFlags.Stale);
            }

            return null;
        }

        var flags = DataQualityFlags.None;
        if (physics.PacketId < _lastPhysicsPacketId.Value)
        {
            flags |= DataQualityFlags.OutOfOrder;
        }

        _lastPhysicsPacketId = physics.PacketId;
        _lastPacketChangeUtc = now;
        _staleReported = false;

        var sample = _builder.Build(physics, graphics, statics, now, Stopwatch.GetTimestamp());
        return flags == DataQualityFlags.None ? sample : sample.WithQuality(flags);
    }

    public void Dispose()
    {
        Disconnect();
    }

    private bool TryConnect()
    {
        // ACC and its shared memory exist only on Windows; elsewhere the reader
        // simply reports that it is not connected.
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            var physicsFile = MemoryMappedFile.OpenExisting(PhysicsMapName);
            MemoryMappedFile graphicsFile;
            MemoryMappedFile staticFile;
            try
            {
                graphicsFile = MemoryMappedFile.OpenExisting(GraphicsMapName);
                staticFile = MemoryMappedFile.OpenExisting(StaticMapName);
            }
            catch
            {
                physicsFile.Dispose();
                throw;
            }

            var physicsView = physicsFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            MemoryMappedViewAccessor graphicsView;
            MemoryMappedViewAccessor staticView;
            try
            {
                graphicsView = graphicsFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                staticView = staticFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            }
            catch
            {
                physicsView.Dispose();
                graphicsFile.Dispose();
                staticFile.Dispose();
                throw;
            }

            _physicsFile = physicsFile;
            _graphicsFile = graphicsFile;
            _staticFile = staticFile;
            _physicsView = physicsView;
            _graphicsView = graphicsView;
            _staticView = staticView;

            _physicsBuffer = new byte[Marshal.SizeOf<AccPhysicsPage>()];
            _graphicsBuffer = new byte[Marshal.SizeOf<AccGraphicsPage>()];
            _staticBuffer = new byte[Marshal.SizeOf<AccStaticPage>()];

            _lastPhysicsPacketId = null;
            _staleReported = false;
            return true;
        }
        catch (FileNotFoundException)
        {
            return false; // ACC not running
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool TryReadPages(out AccPhysicsPage physics, out AccGraphicsPage graphics, out AccStaticPage statics)
    {
        physics = default;
        graphics = default;
        statics = default;

        try
        {
            ReadBytes(_physicsView!, _physicsBuffer);
            ReadBytes(_graphicsView!, _graphicsBuffer);
            ReadBytes(_staticView!, _staticBuffer);

            physics = BytesToStruct<AccPhysicsPage>(_physicsBuffer);
            graphics = BytesToStruct<AccGraphicsPage>(_graphicsBuffer);
            statics = BytesToStruct<AccStaticPage>(_staticBuffer);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false; // layout/capacity mismatch; treated as a failed read
        }
    }

    /// <summary>
    /// Copies as much of the page as fits in the buffer. Buffers are sized to the
    /// struct, and ACC's regions are larger than our (deliberately truncated)
    /// struct prefix, so in practice the whole buffer is filled.
    /// </summary>
    private static void ReadBytes(MemoryMappedViewAccessor view, byte[] buffer)
    {
        Array.Clear(buffer);
        var toRead = (int)Math.Min(buffer.Length, view.Capacity);
        view.ReadArray(0, buffer, 0, toRead);
    }

    private static T BytesToStruct<T>(byte[] buffer) where T : struct
    {
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    private void Disconnect()
    {
        _physicsView?.Dispose();
        _graphicsView?.Dispose();
        _staticView?.Dispose();
        _physicsFile?.Dispose();
        _graphicsFile?.Dispose();
        _staticFile?.Dispose();

        _physicsView = null;
        _graphicsView = null;
        _staticView = null;
        _physicsFile = null;
        _graphicsFile = null;
        _staticFile = null;

        _lastPhysicsPacketId = null;
        _staleReported = false;
    }
}

internal static class TelemetrySampleExtensions
{
    /// <summary>Returns a copy of the sample with additional quality flags merged in.</summary>
    public static TelemetrySample WithQuality(this TelemetrySample sample, DataQualityFlags flags) =>
        sample with { Quality = sample.Quality | flags };
}
