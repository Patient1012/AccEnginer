using AccEngineer.Storage;
using AccEngineer.Telemetry;

namespace AccEngineer.App;

internal sealed record AppOptions
{
    public int PollMs { get; init; } = 50;

    public bool Once { get; init; }

    public bool Record { get; init; }

    public string RecordDirectory { get; init; } =
        Path.Combine(Environment.CurrentDirectory, SessionPaths.DefaultDirectoryName);

    public string? ReplayPath { get; init; }
}

/// <summary>Live ACC telemetry display, optionally recording the session to JSONL.</summary>
internal static class LiveRunner
{
    public static int Run(AppOptions options)
    {
        var display = new ConsoleDisplay();
        using var reader = new AccSharedMemoryReader();
        var validator = new TelemetryValidator();
        var cancellation = new ConsoleCancellation();

        long sampleCount = 0;
        string? lastHeader = null;
        var lastStatusUtc = DateTimeOffset.MinValue;
        var waitingReports = 0;
        var awaitingFirstPacket = true;

        TelemetrySample? last = null;
        ISessionRecorder? recorder = null;
        string? recordedSessionId = null;

        display.Status("Waiting for ACC shared memory (start ACC and enter a session). Ctrl+C to exit.");

        try
        {
            while (!cancellation.IsRequested)
            {
                TelemetrySample? sample;
                try
                {
                    sample = reader.ReadLatest();
                }
                catch (Exception ex)
                {
                    display.Status($"Read error ({ex.GetType().Name}): {ex.Message}");
                    Thread.Sleep(Math.Max(options.PollMs, 500));
                    continue;
                }

                if (sample is not null)
                {
                    awaitingFirstPacket = false;
                    var extra = validator.Validate(sample, DateTimeOffset.UtcNow);
                    if (extra != DataQualityFlags.None)
                    {
                        sample = sample with { Quality = sample.Quality | extra };
                    }

                    last = sample;
                    sampleCount++;

                    var header = ConsoleDisplay.FormatHeader(sample);
                    if (header != lastHeader)
                    {
                        lastHeader = header;
                        display.Status(header);
                    }

                    if (options.Record)
                    {
                        recorder = RecordSample(display, options, recorder, ref recordedSessionId, sample);
                    }

                    display.RenderInline(ConsoleDisplay.FormatSample(sample));

                    if (options.Once) break;
                }
                else if (!reader.IsConnected)
                {
                    if (options.Once)
                    {
                        display.Status("ACC shared memory not available (is ACC running and in a session?).");
                        return 1;
                    }

                    var now = DateTimeOffset.UtcNow;
                    if (now - lastStatusUtc > TimeSpan.FromSeconds(5))
                    {
                        lastStatusUtc = now;
                        waitingReports++;
                        if (!display.Interactive || !awaitingFirstPacket || waitingReports > 1)
                        {
                            display.Status($"No connection to ACC shared memory; retrying... (attempt {waitingReports})");
                        }
                    }

                    display.RenderInline("[disconnected] waiting for ACC...");
                }
                else
                {
                    var hint = last is not null && last.Quality.HasFlag(DataQualityFlags.Stale)
                        ? "STALE — no new data from ACC"
                        : last is not null ? ConsoleDisplay.FormatSample(last) : "connected, awaiting first packet";
                    display.RenderInline(hint);
                }

                cancellation.Wait(options.PollMs);
            }
        }
        finally
        {
            display.EndInline();
            CompleteRecorder(display, recorder);
        }

        if (last is not null)
        {
            display.Status($"Stopped. Samples read: {sampleCount}, last lap: {last.LapNumber}, last speed: {ConsoleDisplay.Fmt(last.SpeedKph)} km/h.");
        }
        else
        {
            display.Status($"Stopped. Samples read: {sampleCount}.");
        }

        return 0;
    }

    /// <summary>
    /// Starts a recorder for the first sample and rotates it when the session
    /// identity changes, so each recording maps to one session.
    /// </summary>
    private static ISessionRecorder RecordSample(
        ConsoleDisplay display,
        AppOptions options,
        ISessionRecorder? recorder,
        ref string? recordedSessionId,
        TelemetrySample sample)
    {
        if (recorder is not null && sample.SessionId == recordedSessionId)
        {
            recorder.Record(sample);
            return recorder;
        }

        CompleteRecorder(display, recorder);

        var metadata = new SessionMetadata
        {
            SchemaVersion = RecordingSchema.Version,
            SessionId = sample.SessionId,
            StartedAtUtc = sample.ReceivedAtUtc,
            TrackName = sample.TrackName,
            CarName = sample.CarName,
            SessionType = sample.SessionType
        };

        var newRecorder = new JsonlSessionRecorder(
            new SessionRecorderOptions { Directory = options.RecordDirectory },
            metadata);

        recordedSessionId = sample.SessionId;
        display.Status($"Recording to {newRecorder.FilePath}");
        newRecorder.Record(sample);
        return newRecorder;
    }

    private static void CompleteRecorder(ConsoleDisplay display, ISessionRecorder? recorder)
    {
        if (recorder is null) return;

        try
        {
            recorder.Complete();
            var message = $"Recording closed: {recorder.FilePath} ({recorder.WrittenSampleCount} samples";
            if (recorder.DroppedSampleCount > 0) message += $", {recorder.DroppedSampleCount} dropped";
            message += recorder is JsonlSessionRecorder { Fault: { } fault }
                ? $", writer fault: {fault.Message})"
                : ")";
            display.Status(message);
        }
        catch (Exception ex)
        {
            display.Status($"Failed to close recording cleanly: {ex.Message}");
        }
    }
}
