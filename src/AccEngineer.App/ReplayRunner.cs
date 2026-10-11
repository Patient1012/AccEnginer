using AccEngineer.Storage;
using AccEngineer.Telemetry;

namespace AccEngineer.App;

/// <summary>
/// Replays a JSONL recording without ACC running. Prints samples in recorded
/// order and verifies that sample numbers and receipt timestamps are monotonic,
/// which is required for offline analysis to be trustworthy.
/// </summary>
internal static class ReplayRunner
{
    public static int Run(string path, ConsoleDisplay display)
    {
        SessionRecording recording;
        try
        {
            recording = new SessionRepository().Load(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            display.StatusError($"Failed to load recording: {ex.Message}");
            return 1;
        }

        var metadata = recording.Metadata;
        display.StatusToStdout($"Recording: {Path.GetFullPath(path)}");
        display.StatusToStdout($"Session: {metadata.SessionId} | track {metadata.TrackName ?? "?"} | car {metadata.CarName ?? "?"} | type {metadata.SessionType} | schema v{metadata.SchemaVersion}");

        var previous = recording.Samples.Count > 0 ? recording.Samples[0] : null;
        var outOfOrder = 0;
        var nonIncreasingTimestamp = 0;

        for (var i = 0; i < recording.Samples.Count; i++)
        {
            var sample = recording.Samples[i];
            if (i > 0 && previous is not null)
            {
                if (sample.SampleId <= previous.SampleId) outOfOrder++;
                if (sample.ReceivedAtUtc < previous.ReceivedAtUtc) nonIncreasingTimestamp++;
            }
            previous = sample;

            Console.WriteLine(ConsoleDisplay.FormatSample(sample));
        }

        var first = recording.Samples.Count > 0 ? recording.Samples[0] : null;
        var last = previous;

        if (first is not null && last is not null)
        {
            display.Status($"Replayed {recording.Samples.Count} samples from {first.ReceivedAtUtc:O} to {last.ReceivedAtUtc:O}.");
        }
        else
        {
            display.Status("Replayed 0 samples.");
        }

        if (outOfOrder > 0) display.StatusWarn($"Warning: {outOfOrder} sample(s) out of order.");
        if (nonIncreasingTimestamp > 0) display.StatusWarn($"Warning: {nonIncreasingTimestamp} sample(s) with non-increasing timestamps.");

        if (recording.End is { } end)
        {
            display.Status($"Recording closed: {end.SampleCount} samples, {end.DroppedSampleCount} dropped, completed={end.Completed} at {end.EndedAtUtc:O}.");
        }
        else
        {
            display.StatusWarn("Recording was not closed cleanly (no end record).");
        }

        if (recording.SkippedRecordCount > 0)
        {
            display.Status($"Skipped {recording.SkippedRecordCount} unparseable record(s).");
        }

        return outOfOrder == 0 && nonIncreasingTimestamp == 0 ? 0 : 3;
    }
}
