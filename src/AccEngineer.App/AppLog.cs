using System.IO;

namespace AccEngineer.App;

internal enum LogLevel
{
    Quiet,
    Normal,
    Verbose
}

/// <summary>
/// Optional file logging for the console app (roadmap §14). When no log file is
/// configured all calls are no-ops, so behaviour is unchanged by default. The log
/// records timestamps and severity, and never contains secrets (API keys are read
/// from the environment and are not written).
/// </summary>
internal static class AppLog
{
    private static readonly object Gate = new();
    private static string? _path;
    private static LogLevel _level = LogLevel.Normal;

    public static LogLevel Level => _level;

    public static bool IsVerbose => _level == LogLevel.Verbose;

    public static void Initialize(string? path, LogLevel level)
    {
        _level = level;
        if (string.IsNullOrWhiteSpace(path))
        {
            _path = null;
            return;
        }

        try
        {
            var full = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _path = full;
            Write("INFO", $"Log started (level={level}).");
        }
        catch (Exception ex)
        {
            _path = null;
            Console.Error.WriteLine($"Could not open log file: {ex.Message}");
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message) => Write("ERROR", message);

    public static void Verbose(string message)
    {
        if (_level == LogLevel.Verbose)
        {
            Write("DEBUG", message);
        }
    }

    private static void Write(string severity, string message)
    {
        var path = _path;
        if (path is null)
        {
            return;
        }

        if (_level == LogLevel.Quiet && severity != "ERROR")
        {
            return;
        }

        lock (Gate)
        {
            try
            {
                File.AppendAllText(
                    path,
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{severity}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never break the app.
            }
        }
    }
}
