namespace AccEngineer.Telemetry.Broadcasting;

/// <summary>Configuration for <see cref="AccBroadcastClient"/>.</summary>
public sealed record AccBroadcastClientOptions
{
    /// <summary>ACC broadcasting endpoint. ACC exposes this on the local machine.</summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>Matches <c>udpListenerPort</c> in ACC's <c>broadcasting.json</c>.</summary>
    public int Port { get; init; } = 9000;

    public string DisplayName { get; init; } = "AccEngineer";
    public string ConnectionPassword { get; init; } = "";
    public string CommandPassword { get; init; } = "";

    /// <summary>How often ACC should push realtime updates, in milliseconds.</summary>
    public int UpdateIntervalMs { get; init; } = 250;

    /// <summary>Local poll cadence for draining the socket and posting keep-alives.</summary>
    public int PollIntervalMs { get; init; } = 100;

    /// <summary>Re-register if no datagram arrives for this long.</summary>
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Minimum spacing between registration attempts.</summary>
    public TimeSpan RegistrationResendInterval { get; init; } = TimeSpan.FromSeconds(4);
}

/// <summary>
/// Consumes the ACC UDP Broadcasting API into a live <see cref="CompetitorStandings"/>
/// snapshot. Handles the registration handshake, re-registers when the feed goes
/// quiet, requests the entry list and track data once per connection, and merges
/// entry-list identity with per-car realtime updates.
/// </summary>
public sealed class AccBroadcastClient : IDisposable
{
    private readonly AccBroadcastClientOptions _options;
    private readonly IAccBroadcastTransport _transport;
    private readonly TimeProvider _time;
    private readonly Action<string>? _log;
    private readonly object _gate = new();
    private readonly object _lifecycle = new();

    private readonly Dictionary<ushort, CarEntry> _entries = new();
    private readonly Dictionary<ushort, CarState> _cars = new();
    private TrackInfo? _track;
    private SessionInfo? _session;

    private int _connectionId = -1;
    private bool _registered;
    private bool _requestedEntry;
    private DateTimeOffset _lastRegisterUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastReceiveUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastDataUtc = DateTimeOffset.MinValue;
    private string? _error;

    private Thread? _thread;
    private volatile bool _running;

    public AccBroadcastClient(
        AccBroadcastClientOptions options,
        IAccBroadcastTransport transport,
        TimeProvider? timeProvider = null,
        Action<string>? log = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _time = timeProvider ?? TimeProvider.System;
        _log = log;
    }

    public bool IsRegistered
    {
        get { lock (_gate) return _registered; }
    }

    public string? Error
    {
        get { lock (_gate) return _error; }
    }

    public int ConnectionId
    {
        get { lock (_gate) return _connectionId; }
    }

    public void Start()
    {
        lock (_lifecycle)
        {
            if (_running) return;
            _running = true;
            _thread = new Thread(Loop)
            {
                IsBackground = true,
                Name = "acc-broadcast"
            };
            _thread.Start();
        }
    }

    public void Stop()
    {
        Thread? thread;
        lock (_lifecycle)
        {
            if (!_running) return;
            _running = false;
            thread = _thread;
            _thread = null;
        }

        thread?.Join(TimeSpan.FromSeconds(2));

        lock (_gate)
        {
            _registered = false;
        }
    }

    private void Loop()
    {
        var interval = Math.Clamp(_options.PollIntervalMs, 10, 1000);
        while (_running)
        {
            try
            {
                Poll();
            }
            catch (Exception ex)
            {
                _log?.Invoke($"Broadcast loop error ({ex.GetType().Name}): {ex.Message}");
            }

            Thread.Sleep(interval);
        }
    }

    /// <summary>One tick: drain received datagrams, then post a registration/keep-alive if needed.</summary>
    public void Poll()
    {
        var now = _time.GetUtcNow();
        Drain(now);
        MaybeRegister(now);
    }

    private void Drain(DateTimeOffset now)
    {
        while (_transport.TryReceive(out var datagram))
        {
            lock (_gate)
            {
                _lastReceiveUtc = now;
                _lastDataUtc = now;
            }

            AccBroadcastMessage? message;
            try
            {
                message = AccBroadcastProtocol.Parse(datagram);
            }
            catch (AccBroadcastFormatException ex)
            {
                _log?.Invoke($"Ignored malformed broadcast datagram: {ex.Message}");
                continue;
            }

            if (message is not null)
            {
                Apply(message);
            }
        }
    }

    private void MaybeRegister(DateTimeOffset now)
    {
        bool stale;
        bool registered;
        lock (_gate)
        {
            registered = _registered;
            stale = registered && now - _lastReceiveUtc > _options.StaleAfter;
        }

        if (registered && !stale) return;
        if (now - _lastRegisterUtc < _options.RegistrationResendInterval) return;

        var request = AccBroadcastProtocol.BuildRegister(
            _options.DisplayName, _options.ConnectionPassword, _options.UpdateIntervalMs, _options.CommandPassword);

        try
        {
            _transport.Send(request);
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _error = $"Failed to send registration: {ex.Message}";
                _registered = false;
            }
            return;
        }

        lock (_gate)
        {
            _lastRegisterUtc = now;
            _registered = false;
            _requestedEntry = false;
        }
    }

    private void Apply(AccBroadcastMessage message)
    {
        lock (_gate)
        {
            switch (message)
            {
                case AccBroadcastRegistrationResult result:
                    _connectionId = result.ConnectionId;
                    _registered = result.Success;
                    _error = result.Success
                        ? null
                        : string.IsNullOrWhiteSpace(result.Error) ? "Registration rejected by ACC." : result.Error;
                    break;

                case AccBroadcastTrackData track:
                    _track = new TrackInfo(track.TrackName, track.TrackMeters);
                    break;

                case AccBroadcastEntryList list:
                    _entries.Clear();
                    foreach (var id in list.CarIds)
                    {
                        _entries[id] = new CarEntry();
                    }
                    break;

                case AccBroadcastEntryListCar car:
                    _entries[car.CarId] = new CarEntry
                    {
                        TeamName = car.TeamName,
                        RaceNumber = car.RaceNumber,
                        CupCategory = car.CupCategory,
                        CurrentDriver = car.Drivers.Count > 0
                            ? car.Drivers[Math.Min(car.CurrentDriverIndex, car.Drivers.Count - 1)]
                            : null
                    };
                    break;

                case AccBroadcastRealtimeUpdate update:
                    _session = new SessionInfo(update.FocusedCarIndex, update.SessionTimeMs, update.SessionPhase);
                    break;

                case AccBroadcastRealtimeCarUpdate update:
                    _cars[update.CarIndex] = new CarState
                    {
                        Position = update.Position,
                        TrackPosition = update.TrackPosition,
                        Location = update.Location,
                        Laps = update.Laps,
                        DeltaMs = update.DeltaMs,
                        BestLapMs = update.BestSessionLap?.LapTimeMs,
                        LastLapMs = update.LastLap?.LapTimeMs,
                        SplinePosition = update.SplinePosition,
                        SpeedKmh = update.SpeedKmh
                    };
                    break;
            }
        }

        if (message is AccBroadcastRegistrationResult { Success: true, ReadOnly: false } && TryClaimEntryRequest())
        {
            _transport.Send(AccBroadcastProtocol.BuildRequestTrackData(ConnectionId));
            _transport.Send(AccBroadcastProtocol.BuildRequestEntryList(ConnectionId));
        }
    }

    private bool TryClaimEntryRequest()
    {
        lock (_gate)
        {
            if (_requestedEntry) return false;
            _requestedEntry = true;
            return true;
        }
    }

    /// <summary>Builds the current standings, or null when nothing has been received yet.</summary>
    public CompetitorStandings? Snapshot()
    {
        lock (_gate)
        {
            if (_cars.Count == 0 && _entries.Count == 0 && _track is null && _session is null)
            {
                return null;
            }

            var indices = new HashSet<ushort>();
            foreach (var id in _entries.Keys) indices.Add(id);
            foreach (var id in _cars.Keys) indices.Add(id);

            var competitors = new List<Competitor>(indices.Count);
            foreach (var index in indices)
            {
                _cars.TryGetValue(index, out var car);
                _entries.TryGetValue(index, out var entry);
                competitors.Add(BuildCompetitor(index, car, entry));
            }

            competitors.Sort(static (a, b) =>
            {
                var position = (a.Position ?? int.MaxValue).CompareTo(b.Position ?? int.MaxValue);
                return position != 0 ? position : b.SplinePosition.CompareTo(a.SplinePosition);
            });

            return new CompetitorStandings(
                _track?.Name,
                _track?.Meters,
                _session?.SessionTimeMs ?? 0f,
                _session?.Phase ?? 0,
                _session?.FocusedCarIndex ?? -1,
                competitors,
                _lastDataUtc);
        }
    }

    private Competitor BuildCompetitor(ushort index, CarState? car, CarEntry? entry)
    {
        var driver = entry?.CurrentDriver;
        return new Competitor(
            index,
            IsPlayer: _session?.FocusedCarIndex == index,
            DriverShortName: NullIfBlank(driver?.ShortName),
            DriverFullName: JoinName(driver),
            RaceNumber: entry is null || entry.RaceNumber <= 0 ? null : entry.RaceNumber,
            TeamName: NullIfBlank(entry?.TeamName),
            CupCategory: entry?.CupCategory,
            Location: car?.Location ?? AccCarLocation.None,
            Position: car is null || car.Position == 0 ? null : car.Position,
            TrackPosition: car is null || car.TrackPosition == 0 ? null : car.TrackPosition,
            LapCount: car?.Laps,
            DeltaMs: car?.DeltaMs,
            BestLapMs: car?.BestLapMs,
            LastLapMs: car?.LastLapMs,
            SplinePosition: car?.SplinePosition ?? 0f,
            SpeedKmh: car?.SpeedKmh);
    }

    private static string? JoinName(AccBroadcastDriverInfo? driver)
    {
        if (driver is null) return null;
        var joined = $"{driver.FirstName} {driver.LastName}".Trim();
        return string.IsNullOrWhiteSpace(joined) ? NullIfBlank(driver.ShortName) : joined;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void Dispose()
    {
        Stop();
        _transport.Dispose();
    }

    private sealed class CarState
    {
        public ushort Position;
        public ushort TrackPosition;
        public AccCarLocation Location;
        public int Laps;
        public int DeltaMs;
        public int? BestLapMs;
        public int? LastLapMs;
        public float SplinePosition;
        public int? SpeedKmh;
    }

    private sealed class CarEntry
    {
        public string TeamName = string.Empty;
        public int RaceNumber;
        public AccCupCategory CupCategory;
        public AccBroadcastDriverInfo? CurrentDriver;
    }

    private sealed record TrackInfo(string Name, int Meters);

    private sealed record SessionInfo(int FocusedCarIndex, float SessionTimeMs, byte Phase);
}
