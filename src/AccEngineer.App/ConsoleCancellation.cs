namespace AccEngineer.App;

/// <summary>Ctrl+C / Ctrl+Break handled without tearing down mid-read.</summary>
internal sealed class ConsoleCancellation
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
