using System.Buffers.Binary;
using System.Text;
using AccEngineer.Telemetry.Broadcasting;

namespace AccEngineer.Telemetry.Tests;

/// <summary>
/// Tests for <see cref="AccBroadcastClient"/>. A fake transport feeds
/// hand-built datagrams so the registration handshake, keep-alive and state
/// aggregation run without any socket.
/// </summary>
public class AccBroadcastClientTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    private static AccBroadcastClientOptions Options() => new()
    {
        UpdateIntervalMs = 250,
        PollIntervalMs = 100,
        StaleAfter = TimeSpan.FromSeconds(5),
        RegistrationResendInterval = TimeSpan.FromSeconds(4)
    };

    private static (AccBroadcastClient Client, FakeTransport Transport, ManualTimeProvider Time) Build()
    {
        var transport = new FakeTransport();
        var time = new ManualTimeProvider(Start);
        var client = new AccBroadcastClient(Options(), transport, time);
        return (client, transport, time);
    }

    [Fact]
    public void FirstPoll_SendsRegistration()
    {
        var (client, transport, _) = Build();
        using (client)
        {
            client.Poll();

            Assert.Equal(1, transport.CountOf(1));
            Assert.False(client.IsRegistered);
        }
    }

    [Fact]
    public void RegistrationSuccess_RequestsTrackDataAndEntryList()
    {
        var (client, transport, _) = Build();
        using (client)
        {
            client.Poll();
            transport.Receive(Datagrams.RegistrationResult(connectionId: 42, success: true, readOnly: false));

            client.Poll();

            Assert.True(client.IsRegistered);
            Assert.Equal(42, client.ConnectionId);
            Assert.Equal(1, transport.CountOf(11)); // request track data
            Assert.Equal(1, transport.CountOf(10)); // request entry list
        }
    }

    [Fact]
    public void RegistrationFailure_SetsError()
    {
        var (client, transport, _) = Build();
        using (client)
        {
            client.Poll();
            transport.Receive(Datagrams.RegistrationResult(connectionId: 0, success: false, readOnly: false, error: "bad password"));

            client.Poll();

            Assert.False(client.IsRegistered);
            Assert.Equal("bad password", client.Error);
        }
    }

    [Fact]
    public void ReadOnlyRegistration_DoesNotRequestEntryList()
    {
        var (client, transport, _) = Build();
        using (client)
        {
            client.Poll();
            transport.Receive(Datagrams.RegistrationResult(connectionId: 9, success: true, readOnly: true));

            client.Poll();

            Assert.True(client.IsRegistered);
            Assert.Equal(0, transport.CountOf(11));
            Assert.Equal(0, transport.CountOf(10));
        }
    }

    [Fact]
    public void Snapshot_IsNullBeforeAnyData()
    {
        var (client, transport, _) = Build();
        using (client)
        {
            client.Poll();
            transport.Receive(Datagrams.RegistrationResult(1, true, false));
            client.Poll();

            Assert.Null(client.Snapshot());
        }
    }

    [Fact]
    public void AggregatesCompetitors_FromEntryListAndCarUpdates()
    {
        var (client, transport, _) = Build();
        using (client)
        {
            client.Poll(); // register
            FeedSession(transport);
            client.Poll();

            var standings = client.Snapshot();

            Assert.NotNull(standings);
            Assert.Equal("spa", standings!.TrackName);
            Assert.Equal(7004, standings.TrackMeters);
            Assert.Equal(1, standings.FocusedCarIndex);
            Assert.Equal(2, standings.Competitors.Count);

            var leader = standings.Competitors[0];
            Assert.Equal(1, leader.Position);
            Assert.Equal(42, leader.RaceNumber);
            Assert.Equal("Max Power", leader.DriverFullName);
            Assert.Equal("MP", leader.DriverShortName);
            Assert.Equal("Kunos Racing", leader.TeamName);
            Assert.Equal(AccCupCategory.ProAm, leader.CupCategory);
            Assert.Equal((byte)0, leader.CarModelType);
            Assert.Equal(AccCarClass.Gt3, leader.CarClass);
            Assert.Equal(AccDriverCategory.Platinum, leader.DriverCategory);
            Assert.True(leader.IsPlayer);
            Assert.False(leader.IsInPit);
            Assert.Equal(98_000, leader.BestLapMs);
            Assert.Equal(-1234, leader.DeltaMs);

            var second = standings.Competitors[1];
            Assert.Equal(2, second.Position);
            Assert.False(second.IsPlayer);
            Assert.True(second.IsInPit);
            Assert.Equal("AO", second.DriverShortName);
            Assert.Equal((byte)50, second.CarModelType);
            Assert.Equal(AccCarClass.Gt4, second.CarClass);
            Assert.Equal(AccDriverCategory.Silver, second.DriverCategory);
            Assert.NotNull(standings.Player);
            Assert.Equal(1, standings.Player!.CarIndex);
        }
    }

    [Fact]
    public void Competitors_AreSortedByOfficialPosition()
    {
        var (client, transport, _) = Build();
        using (client)
        {
            client.Poll();
            transport.Receive(Datagrams.RegistrationResult(1, true, false));
            client.Poll();
            // Position 2 arrives before position 1.
            transport.Receive(Datagrams.CarUpdate(carIndex: 2, position: 2, spline: 0.9f));
            transport.Receive(Datagrams.CarUpdate(carIndex: 1, position: 1, spline: 0.5f));
            client.Poll();

            var standings = client.Snapshot();

            Assert.NotNull(standings);
            Assert.Equal(new ushort[] { 1, 2 }, standings!.Competitors.Select(c => c.CarIndex));
        }
    }

    [Fact]
    public void Snapshot_WithoutEntryListCar_LeavesModelAndCategoryUnknown()
    {
        // An entry-list id alone carries no vehicle or driver data; nothing may be invented.
        var (client, transport, _) = Build();
        using (client)
        {
            client.Poll();
            transport.Receive(Datagrams.RegistrationResult(1, true, false));
            transport.Receive(Datagrams.TrackData("spa", 11, 7004));
            transport.Receive(Datagrams.EntryList(1, [1]));
            transport.Receive(Datagrams.RealtimeUpdate(focusedCarIndex: 1));
            transport.Receive(Datagrams.CarUpdate(carIndex: 1, position: 1, spline: 0.5f));
            client.Poll();

            var car = client.Snapshot()!.Competitors[0];
            Assert.Null(car.CarModelType);
            Assert.Null(car.DriverCategory);
            Assert.Equal(AccCarClass.Unknown, car.CarClass);
        }
    }

    [Fact]
    public void ReRegisters_WhenFeedGoesQuiet()
    {
        var (client, transport, time) = Build();
        using (client)
        {
            client.Poll();
            Assert.Equal(1, transport.CountOf(1));

            FeedSession(transport);
            client.Poll();

            time.Advance(TimeSpan.FromSeconds(6));
            client.Poll();

            Assert.Equal(2, transport.CountOf(1));
            Assert.False(client.IsRegistered);
        }
    }

    [Fact]
    public void DoesNotSpamRegistration_WithinResendInterval()
    {
        var (client, transport, time) = Build();
        using (client)
        {
            client.Poll();
            time.Advance(TimeSpan.FromSeconds(1));
            client.Poll();
            time.Advance(TimeSpan.FromSeconds(1));
            client.Poll();

            Assert.Equal(1, transport.CountOf(1));
        }
    }

    [Fact]
    public void MalformedDatagram_IsIgnoredAndKeepsState()
    {
        var (client, transport, _) = Build();
        using (client)
        {
            client.Poll();
            FeedSession(transport);
            transport.Receive([0x03, 0x01]); // truncated car update
            client.Poll();

            Assert.NotNull(client.Snapshot());
            Assert.Equal(2, client.Snapshot()!.Competitors.Count);
        }
    }

    private static void FeedSession(FakeTransport transport)
    {
        transport.Receive(Datagrams.RegistrationResult(1, true, false));
        transport.Receive(Datagrams.TrackData("spa", 11, 7004));
        transport.Receive(Datagrams.EntryList(1, [1, 2]));
        transport.Receive(Datagrams.EntryListCar(
            1, "Kunos Racing", 42, AccCupCategory.ProAm, 0, [("Max", "Power", "MP")],
            carModel: 0, driverCategory: AccDriverCategory.Platinum));
        transport.Receive(Datagrams.EntryListCar(
            2, "Rival Team", 7, AccCupCategory.Am, 0, [("Ann", "Other", "AO")],
            carModel: 50, driverCategory: AccDriverCategory.Silver));
        transport.Receive(Datagrams.RealtimeUpdate(focusedCarIndex: 1));
        transport.Receive(Datagrams.CarUpdate(
            carIndex: 1, position: 1, spline: 0.5f, location: AccCarLocation.Track,
            bestLapMs: 98_000, lastLapMs: 99_500, deltaMs: -1234, laps: 6));
        transport.Receive(Datagrams.CarUpdate(
            carIndex: 2, position: 2, spline: 0.4f, location: AccCarLocation.Pitlane,
            bestLapMs: 99_000, lastLapMs: 100_000, deltaMs: 2500, laps: 5));
    }

    private sealed class FakeTransport : IAccBroadcastTransport
    {
        private readonly List<byte[]> _sent = [];
        private readonly Queue<byte[]> _incoming = new();

        public void Receive(byte[] datagram) => _incoming.Enqueue(datagram);

        public int CountOf(byte messageType) => _sent.Count(s => s.Length > 0 && s[0] == messageType);

        public void Send(byte[] datagram) => _sent.Add(datagram);

        public bool TryReceive(out byte[] datagram)
        {
            if (_incoming.Count > 0)
            {
                datagram = _incoming.Dequeue();
                return true;
            }

            datagram = [];
            return false;
        }

        public void Dispose() { }
    }

    /// <summary>Hand-built inbound datagrams matching the documented wire layout.</summary>
    private static class Datagrams
    {
        public static byte[] RegistrationResult(int connectionId, bool success, bool readOnly, string error = "")
        {
            var w = new Wire();
            w.U8(1).I32(connectionId).U8((byte)(success ? 1 : 0)).U8((byte)(readOnly ? 1 : 0)).Str(error);
            return w.Bytes();
        }

        public static byte[] TrackData(string name, int trackId, int meters)
        {
            var w = new Wire();
            w.U8(5).I32(1).Str(name).I32(trackId).I32(meters);
            return w.Bytes();
        }

        public static byte[] EntryList(int connectionId, ushort[] carIds)
        {
            var w = new Wire();
            w.U8(4).I32(connectionId).U16((ushort)carIds.Length);
            foreach (var id in carIds)
            {
                w.U16(id);
            }

            return w.Bytes();
        }

        public static byte[] EntryListCar(
            ushort carId, string team, int raceNumber, AccCupCategory cup, byte currentDriver,
            (string First, string Last, string Short)[] drivers,
            byte carModel = 1, AccDriverCategory driverCategory = AccDriverCategory.Bronze)
        {
            var w = new Wire();
            w.U8(6).U16(carId).U8(carModel).Str(team).I32(raceNumber).U8((byte)cup).U8(currentDriver).U16(24).U8((byte)drivers.Length);
            foreach (var driver in drivers)
            {
                w.Str(driver.First).Str(driver.Last).Str(driver.Short).U8((byte)driverCategory).U16(24);
            }

            return w.Bytes();
        }

        public static byte[] RealtimeUpdate(int focusedCarIndex, byte phase = 4, float sessionTimeMs = 90_000)
        {
            var w = new Wire();
            w.U8(2)
                .U16(0).U16(0).U8(2).U8(phase).F32(sessionTimeMs).F32(0).I32(focusedCarIndex)
                .Str("").Str("").Str("")
                .U8(0)
                .F32(0).U8(20).U8(30).U8(0).U8(0).U8(0)
                .Lap(AccBroadcastProtocol.NoTimeSentinel, 0, 0, [], false, false, false, false);
            return w.Bytes();
        }

        public static byte[] CarUpdate(
            ushort carIndex,
            ushort position,
            float spline,
            AccCarLocation location = AccCarLocation.Track,
            int bestLapMs = AccBroadcastProtocol.NoTimeSentinel,
            int lastLapMs = AccBroadcastProtocol.NoTimeSentinel,
            int deltaMs = 0,
            int laps = 0)
        {
            var w = new Wire();
            w.U8(3)
                .U16(carIndex).U16(0).U8(1).U8(2)
                .F32(0).F32(0).F32(0)
                .U8((byte)location)
                .U16(200)
                .U16(position).U16(position).U16(position)
                .F32(spline)
                .U16((ushort)laps)
                .I32(deltaMs)
                .Lap(bestLapMs, carIndex, 0, [], false, false, false, false)
                .Lap(lastLapMs, carIndex, 0, [], false, false, false, false)
                .Lap(AccBroadcastProtocol.NoTimeSentinel, carIndex, 0, [], false, false, false, false);
            return w.Bytes();
        }
    }

    private sealed class Wire
    {
        private readonly List<byte> _bytes = [];

        public Wire U8(byte v) { _bytes.Add(v); return this; }

        public Wire U16(ushort v)
        {
            Span<byte> tmp = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(tmp, v);
            _bytes.AddRange(tmp.ToArray());
            return this;
        }

        public Wire I32(int v)
        {
            Span<byte> tmp = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(tmp, v);
            _bytes.AddRange(tmp.ToArray());
            return this;
        }

        public Wire F32(float v)
        {
            Span<byte> tmp = stackalloc byte[4];
            BinaryPrimitives.WriteSingleLittleEndian(tmp, v);
            _bytes.AddRange(tmp.ToArray());
            return this;
        }

        public Wire Str(string v)
        {
            var raw = Encoding.UTF8.GetBytes(v);
            return U16((ushort)raw.Length).Raw(raw);
        }

        public Wire Raw(byte[] v) { _bytes.AddRange(v); return this; }

        public Wire Lap(int timeMs, ushort car, ushort driver, int[] splits, bool invalid, bool validForBest, bool outlap, bool inlap)
        {
            I32(timeMs).U16(car).U16(driver).U8((byte)splits.Length);
            foreach (var split in splits)
            {
                I32(split);
            }

            return U8((byte)(invalid ? 1 : 0)).U8((byte)(validForBest ? 1 : 0)).U8((byte)(outlap ? 1 : 0)).U8((byte)(inlap ? 1 : 0));
        }

        public byte[] Bytes() => _bytes.ToArray();
    }
}
