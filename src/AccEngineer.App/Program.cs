using AccEngineer.App;

// ACC Telemetry console.
//   (none)             live display
//   --record           live display + record the session to JSONL
//   --replay <file>    replay a recording offline (no ACC needed)
//   --analyze <file>   analyse a recording offline, write JSON + Markdown reports
// stdout carries sample lines / report text, stderr carries status messages.

var options = new AppOptions();
string? replayPath = null;
string? analyzePath = null;
string? outputDir = null;
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
        case "--analyze":
            if (i + 1 < args.Length)
            {
                analyzePath = args[++i];
            }
            else
            {
                errors.Add("--analyze requires a recording file path.");
            }
            break;
        case "--out":
            if (i + 1 < args.Length)
            {
                outputDir = args[++i];
            }
            else
            {
                errors.Add("--out requires a directory path.");
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

if (analyzePath is not null)
{
    if (replayPath is not null) errors.Add("--analyze cannot be combined with --replay.");
    if (options.Once) errors.Add("--analyze cannot be combined with --once.");
    if (options.Record) errors.Add("--analyze cannot be combined with --record.");
}
else if (outputDir is not null)
{
    errors.Add("--out can only be used with --analyze.");
}

if (errors.Count > 0)
{
    foreach (var error in errors) Console.Error.WriteLine(error);
    PrintHelp();
    return 2;
}

if (analyzePath is not null)
{
    return AnalyzeRunner.Run(analyzePath, outputDir, new ConsoleDisplay());
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
    Console.WriteLine("  --analyze <file>     Analyse a recorded session and write JSON + Markdown reports.");
    Console.WriteLine("  --out <dir>          Report output directory for --analyze (default: ./reports).");
    Console.WriteLine("  --record-dir <dir>   Recording output directory (default: ./recordings).");
    Console.WriteLine("  --poll-ms <n>        Polling interval in milliseconds (default 50).");
    Console.WriteLine("  --once               Read once, print the result, and exit (exit 0 = sample, 1 = no data).");
    Console.WriteLine("  --help               Show this help.");
}
