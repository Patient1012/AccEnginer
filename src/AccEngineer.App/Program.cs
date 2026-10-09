using AccEngineer.App;

// ACC Telemetry console.
//   (none)             live display
//   --record           live display + record the session to JSONL
//   --replay <file>    replay a recording offline (no ACC needed)
// stdout carries sample lines, stderr carries status messages.

var options = new AppOptions();
string? replayPath = null;
var errors = new List<string>();

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--poll-ms":
            if (i + 1 < args.Length && int.TryParse(args[i + 1], out var ms) && ms > 0)
            {
                options = options with { PollMs = ms };
                i++;
            }
            else
            {
                errors.Add("--poll-ms requires a positive integer.");
            }
            break;
        case "--record":
            options = options with { Record = true };
            break;
        case "--record-dir":
            if (i + 1 < args.Length && args[i + 1].Length > 0)
            {
                options = options with { RecordDirectory = args[i + 1] };
                i++;
            }
            else
            {
                errors.Add("--record-dir requires a directory path.");
            }
            break;
        case "--replay":
            if (i + 1 < args.Length && args[i + 1].Length > 0)
            {
                replayPath = args[i + 1];
                i++;
            }
            else
            {
                errors.Add("--replay requires a recording file path.");
            }
            break;
        case "--once":
            options = options with { Once = true };
            break;
        case "--help" or "-h":
            PrintHelp();
            return 0;
        default:
            errors.Add($"Unknown argument: {args[i]}");
            break;
    }
}

if (replayPath is not null && options.Once)
{
    errors.Add("--once cannot be combined with --replay.");
}

if (errors.Count > 0)
{
    foreach (var error in errors) Console.Error.WriteLine(error);
    PrintHelp();
    return 2;
}

if (replayPath is not null)
{
    return ReplayRunner.Run(replayPath, new ConsoleDisplay());
}

return LiveRunner.Run(options);

static void PrintHelp()
{
    Console.WriteLine("Usage: AccEngineer.App [options]");
    Console.WriteLine("  (no options)         Live ACC telemetry display.");
    Console.WriteLine("  --record             Also record the session to a JSONL file per session.");
    Console.WriteLine("  --replay <file>      Replay a recorded session offline (no ACC needed).");
    Console.WriteLine("  --record-dir <dir>   Recording output directory (default: ./recordings).");
    Console.WriteLine("  --poll-ms <n>        Polling interval in milliseconds (default 50).");
    Console.WriteLine("  --once               Read once, print the result, and exit (exit 0 = sample, 1 = no data).");
    Console.WriteLine("  --help               Show this help.");
}
