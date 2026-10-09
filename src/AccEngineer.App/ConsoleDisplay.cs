using System.Globalization;
using AccEngineer.Telemetry;

namespace AccEngineer.App;

/// <summary>
/// Console rendering shared by live and replay modes. Sample/status lines go to
/// stdout, status messages to stderr, so recordings and logs can be piped cleanly.
/// </summary>
internal sealed class ConsoleDisplay
{
    private readonly bool _interactive = !Console.IsOutputRedirected;
    private int _lastInlineWidth;

    public bool Interactive => _interactive;

    public static string FormatHeader(TelemetrySample s)
    {
        var parts = new List<string> { $"session {s.SessionId}" };
        if (s.TrackName is not null) parts.Add($"track {s.TrackName}");
        if (s.CarName is not null) parts.Add($"car {s.CarName}");
        parts.Add($"type {s.SessionType.ToString().ToLowerInvariant()}");
        parts.Add($"state {s.SessionState}");
        if (s.TyreCompound is { Length: > 0 } compound) parts.Add($"tyres {compound}");
        return string.Join(" | ", parts);
    }

    public static string FormatSample(TelemetrySample s)
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

    public static string FormatGear(int? raw) => raw switch
    {
        null => "?",
        0 => "R",
        1 => "N",
        >= 2 => (raw.Value - 1).ToString(CultureInfo.InvariantCulture),
        _ => raw.Value.ToString(CultureInfo.InvariantCulture)
    };

    public static string Fmt(float? value) =>
        value is null ? "?" : value.Value.ToString("0.0", CultureInfo.InvariantCulture);

    public static string Pct(float? value) =>
        value is null ? "?" : (value.Value * 100f).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>Writes a status message (stderr) and clears the inline line if present.</summary>
    public void Status(string message)
    {
        Console.Out.Flush();
        if (_interactive)
        {
            Console.WriteLine();
            Console.Error.WriteLine(message);
            _lastInlineWidth = 0;
        }
        else
        {
            Console.Error.WriteLine(message);
        }
    }

    public void StatusToStdout(string message) => Console.WriteLine(message);

    /// <summary>Renders a line in place when interactive, otherwise as a normal line.</summary>
    public void RenderInline(string line)
    {
        if (!_interactive)
        {
            Console.WriteLine(line);
            return;
        }

        if (_lastInlineWidth > 0)
        {
            Console.Write("\r" + line.PadRight(_lastInlineWidth));
        }
        else
        {
            Console.Write("\r" + line);
        }

        _lastInlineWidth = line.Length;
    }

    public void EndInline()
    {
        if (_interactive && _lastInlineWidth > 0)
        {
            Console.WriteLine();
            _lastInlineWidth = 0;
        }
    }
}
