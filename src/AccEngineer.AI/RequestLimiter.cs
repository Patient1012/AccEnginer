namespace AccEngineer.AI;

/// <summary>
/// Bounds outbound model requests: at most <c>maxConcurrent</c> in flight and no
/// more than <c>maxRequestsPerMinute</c> starts. Telemetry recording never waits
/// on this; only callers that choose to request an interpretation acquire a slot.
/// </summary>
public sealed class RequestLimiter : IDisposable
{
    private readonly SemaphoreSlim _gate;
    private readonly TimeSpan _minInterval;
    private readonly object _sync = new();
    private DateTimeOffset _nextAllowed = DateTimeOffset.MinValue;

    public RequestLimiter(int maxConcurrent = 1, int maxRequestsPerMinute = 20)
    {
        var concurrent = Math.Max(1, maxConcurrent);
        _gate = new SemaphoreSlim(concurrent, concurrent);
        _minInterval = maxRequestsPerMinute <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(60.0 / maxRequestsPerMinute);
    }

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WaitForSpacingAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _gate.Release();
            throw;
        }

        return new Releaser(_gate);
    }

    private async Task WaitForSpacingAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            TimeSpan delay;
            lock (_sync)
            {
                var now = DateTimeOffset.UtcNow;
                if (now >= _nextAllowed)
                {
                    _nextAllowed = now + _minInterval;
                    return;
                }

                delay = _nextAllowed - now;
            }

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose() => _gate.Dispose();

    private sealed class Releaser : IDisposable
    {
        private SemaphoreSlim? _gate;

        public Releaser(SemaphoreSlim gate) => _gate = gate;

        public void Dispose()
        {
            var gate = Interlocked.Exchange(ref _gate, null);
            gate?.Release();
        }
    }
}
