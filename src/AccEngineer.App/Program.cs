using System.Globalization;
using AccEngineer.Telemetry;

// ACC Telemetry console reader (Milestone 1).
// Prints live ACC telemetry while ACC runs; waits/reconnects otherwise.
// stdout = sample lines, stderr = status messages (when output is redirected).
// Usage: AccEngineer.App [--poll-ms N] [--once]

var pollMs = 50;
var once = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--poll-ms" when i + 1 < args.Length && int.TryParse(args[i + 1], out var ms) && ms > 0:
            pollMs = ms;
            i++;
            break;
        case "--once":
            once = true;
            break;
        case "--help" or "-h":
            Console.WriteLine("Usage: AccEngineer.App [--poll-ms N] [--once]");
            Console.WriteLine("  --poll-ms N  Polling interval in milliseconds (default 50).");
            Console.WriteLine("  --once       Read once, print the result, and exit (exit code 0 = sample, 1 = no data).");
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return 2;
    }
}

var interactive = !Console.IsOutputRedirected;
using var reader = new AccSharedMemoryReader();
var validator = new TelemetryValidator();
var cancellation = new ConsoleCancellation();

long sampleCount = 0;
long waitingReports = 0;
TelemetrySample? last = null;
string? lastHeader = null;
var lastStatusUtc = DateTimeOffset.MinValue;
var lastInlineWidth = 0;
var awaitingFirstPacket = true;

WriteStatus("Waiting for ACC shared memory (start ACC and enter a session). Ctrl+C to exit.");

while (!cancellation.IsRequested)
{
    TelemetrySample? sample;
    try
    {
        sample = reader.ReadLatest();
    }
    catch (Exception ex)
    {
        // Telemetry capture must never crash; report and keep trying.
        WriteStatus($"Read error ({ex.GetType().Name}): {ex.Message}");
        Thread.Sleep(Math.Max(pollMs, 500));
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

        var header = FormatHeader(sample);
        if (header is not null && header != lastHeader)
        {
            lastHeader = header;
            WriteStatus(header);
        }

        if (interactive)
        {
            RenderInline(FormatSample(sample));
        }
        else
        {
            Console.WriteLine(FormatSample(sample));
        }

        if (once) break;
    }
    else if (!reader.IsConnected)
    {
        if (once)
        {
            WriteStatus("ACC shared memory not available (is ACC running and in a session?).");
            return 1;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - lastStatusUtc > TimeSpan.FromSeconds(5))
        {
            lastStatusUtc = now;
            waitingReports++;
            if (interactive)
            {
                var msg = awaitingFirstPacket && waitingReports == 1
                    ? null // initial message already printed
                    : $"No connection to ACC shared memory; retrying... (attempt {waitingReports})";
                if (msg is not null) WriteStatus(msg);
            }
            else
            {
                WriteStatus($"No connection to ACC shared memory; retrying... (attempt {waitingReports})");
            }
        }

        if (interactive) RenderInline("[disconnected] waiting for ACC...");
    }
    else if (interactive)
    {
        // Connected, but no new physics packet since the last poll.
        var staleHint = last is not null && last.Quality.HasFlag(DataQualityFlags.Stale)
            ? "STALE — no new data from ACC"
            : last is not null ? FormatSample(last) : "connected, awaiting first packet";
        RenderInline(staleHint);
    }

    cancellation.Wait(pollMs);
}

if (interactive) Console.WriteLine();

if (last is not null)
{
    WriteStatus($"Stopped. Samples read: {sampleCount}, last lap: {last.LapNumber}, last speed: {Fmt(last.SpeedKph)} km/h.");
}
else
{
    WriteStatus($"Stopped. Samples read: {sampleCount}.");
}

return 0;

static string FormatHeader(TelemetrySample s)
{
    var parts = new List<string> { $"session {s.SessionId}" };
    if (s.TrackName is not null) parts.Add($"track {s.TrackName}");
    if (s.CarName is not null) parts.Add($"car {s.CarName}");
    parts.Add($"type {s.SessionType.ToString().ToLowerInvariant()}");
    parts.Add($"state {s.SessionState}");
    if (s.TyreCompound is { Length: > 0 } compound) parts.Add($"tyres {compound}");
    return string.Join(" | ", parts);
}

static string FormatSample(TelemetrySample s)
{
    var flags = s.Quality == DataQualityFlags.None ? "-" : s.Quality.ToString();

    var line =
        $"[{s.SessionState,-6}] " +
        $"{Fmt(s.SpeedKph),7} km/h  " +
        $"thr {Pct(s.ThrottleInput),3}%  " +
        $"brk {Pct(s.BrakeInput),3}%  " +
        $"str {Fmt(s.SteeringInput),6}  " +
        $"gear {FormatGear(s.Gear),2}  " +
        $"rpm {s.EngineRpm?.ToString(CultureInfo.InvariantCulture) ?? "?",5}  " +
        $"lap {s.LapNumber}{(s.IsLapValid ? "" : " (invalid)")}  " +
        $"dist {Fmt(s.TrackProgressMeters),7} m  " +
        $"fuel {Fmt(s.FuelLevel),5} kg  " +
        $"flags {flags}";

    if (s.Quality.HasFlag(DataQualityFlags.Stale)) line = "STALE " + line;
    if (s.Quality.HasFlag(DataQualityFlags.SessionChanged)) line += "  << session changed";
    if (s.Quality.HasFlag(DataQualityFlags.PitStop)) line += "  (pit)";

    return line;
}

static string FormatGear(int? raw) => raw switch
{
    null => "?",
    0 => "R",
    1 => "N",
    >= 2 => (raw.Value - 1).ToString(CultureInfo.InvariantCulture),
    _ => raw.Value.ToString(CultureInfo.InvariantCulture)
};

static string Fmt(float? value) =>
    value is null ? "?" : value.Value.ToString("0.0", CultureInfo.InvariantCulture);

static string Pct(float? value) =>
    value is null ? "?" : (value.Value * 100f).ToString("0", CultureInfo.InvariantCulture);

void WriteStatus(string message)
{
    if (interactive)
    {
        Console.WriteLine();
        Console.Error.WriteLine(message);
        lastInlineWidth = 0;
    }
    else
    {
        Console.Error.WriteLine(message);
    }
}

void RenderInline(string line)
{
    if (!interactive) return;
    if (Console.IsOutputRedirected)
    {
        Console.WriteLine(line);
        return;
    }

    if (lastInlineWidth > 0)
    {
        Console.Write("\r" + line.PadRight(lastInlineWidth));
    }
    else
    {
        Console.Write("\r" + line);
    }

    lastInlineWidth = line.Length;
}

/// <summary>Ctrl+C / Ctrl+Break handled without tearing down mid-read.</summary>
sealed class ConsoleCancellation
{
    private volatile bool _requested;

    public ConsoleCancellation()
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _requested = true;
        };
    }

    public bool IsRequested => _requested;

    public void Wait(int milliseconds)
    {
        var remaining = milliseconds;
        while (remaining > 0 && !_requested)
        {
            var slice = Math.Min(remaining, 50);
            Thread.Sleep(slice);
            remaining -= slice;
        }
    }
}
