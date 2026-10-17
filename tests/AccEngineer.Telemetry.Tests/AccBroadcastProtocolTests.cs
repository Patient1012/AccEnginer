using System.Buffers.Binary;
using System.Text;
using AccEngineer.Telemetry.Broadcasting;

namespace AccEngineer.Telemetry.Tests;

/// <summary>
/// Tests for the ACC UDP Broadcasting protocol codec. Fixtures are encoded by
/// hand from the documented wire layout (little-endian, u16-length-prefixed
/// UTF-8 strings) so the decoder is exercised independently of the encoder.
/// </summary>
public class AccBroadcastProtocolTests
{
    [Fact]
    public void BuildRegister_WritesTypeVersionAndFields()
    {
        var bytes = AccBroadcastProtocol.BuildRegister("Engineer", "pw", 250, "cmd");

        Assert.Equal(1, bytes[0]); // Register
        Assert.Equal(4, bytes[1]); // protocol version
        var offset = 2;
        Assert.Equal("Engineer", ReadString(bytes, ref offset));
        Assert.Equal("pw", ReadString(bytes, ref offset));
        Assert.Equal(250, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset)));
        offset += 4;
        Assert.Equal("cmd", ReadString(bytes, ref offset));
        Assert.Equal(bytes.Length, offset);
    }

    [Fact]
    public void BuildRequests_WriteTypeAndConnectionId()
    {
        Assert.Equal(new byte[] { 9, 0x11, 0x22, 0x33, 0x44 }, AccBroadcastProtocol.BuildUnregister(0x44332211));
        Assert.Equal(new byte[] { 10, 0x11, 0x22, 0x33, 0x44 }, AccBroadcastProtocol.BuildRequestEntryList(0x44332211));
        Assert.Equal(new byte[] { 11, 0x11, 0x22, 0x33, 0x44 }, AccBroadcastProtocol.BuildRequestTrackData(0x44332211));
    }

    [Fact]
    public void Parse_RegistrationResult_ReadsConnectionAndError()
    {
        var w = new Wire();
        w.U8(1).I32(7).U8(1).U8(0).Str("ok");

        var result = Assert.IsType<AccBroadcastRegistrationResult>(AccBroadcastProtocol.Parse(w.Bytes()));

        Assert.Equal(7, result.ConnectionId);
        Assert.True(result.Success);
        Assert.False(result.ReadOnly);
        Assert.Equal("ok", result.Error);
    }

    [Fact]
    public void Parse_RealtimeCarUpdate_ReadsPositionSplineAndSignedDelta()
    {
        var w = new Wire();
        w.U8(3)
            .U16(12)          // car index
            .U16(0)           // driver index
            .U8(1)            // driver count
            .U8(3)            // raw gear byte (3 -> third on the wire)
            .F32(10.5f).F32(-4.25f).F32(1.5f) // world x, y, yaw
            .U8(1)            // car location: track
            .U16(212)         // km/h
            .U16(4)           // official position
            .U16(2)           // cup position
            .U16(3)           // track position
            .F32(0.42f)       // spline
            .U16(6)           // laps
            .I32(-1234)       // signed delta (ahead)
            .Lap(98_000, 12, 0, [30_000, 60_000], true, true, false, false)
            .Lap(99_500, 12, 0, [31_000, 61_000], false, false, false, false)
            .Lap(AccBroadcastProtocol.NoTimeSentinel, 12, 0, [], false, false, false, false);

        var car = Assert.IsType<AccBroadcastRealtimeCarUpdate>(AccBroadcastProtocol.Parse(w.Bytes()));

        Assert.Equal(12, car.CarIndex);
        Assert.Equal(3, car.GearByte);
        Assert.Equal(212, car.SpeedKmh);
        Assert.Equal(4, car.Position);
        Assert.Equal(0.42f, car.SplinePosition);
        Assert.Equal(6, car.Laps);
        Assert.Equal(-1234, car.DeltaMs);
        Assert.Equal(AccCarLocation.Track, car.Location);
        Assert.Equal(98_000, car.BestSessionLap!.LapTimeMs);
        Assert.Equal(2, car.BestSessionLap.Splits.Count);
        Assert.Equal(60_000, car.BestSessionLap.Splits[1]);
        Assert.Null(car.CurrentLap!.LapTimeMs);
        Assert.Empty(car.CurrentLap.Splits);
    }

    [Fact]
    public void Parse_RealtimeCarUpdate_TreatsSentinelSplitsAsNull()
    {
        var w = new Wire();
        w.U8(3).U16(1).U16(0).U8(1).U8(0)
            .F32(0).F32(0).F32(0).U8(0).U16(0).U16(0).U16(0).U16(0).F32(0).U16(0).I32(0)
            .Lap(0, 0, 0, [AccBroadcastProtocol.NoTimeSentinel, 12_345], false, false, false, false)
            .Lap(0, 0, 0, [], false, false, false, false)
            .Lap(0, 0, 0, [], false, false, false, false);

        var car = Assert.IsType<AccBroadcastRealtimeCarUpdate>(AccBroadcastProtocol.Parse(w.Bytes()));

        Assert.Null(car.BestSessionLap!.Splits[0]);
        Assert.Equal(12_345, car.BestSessionLap.Splits[1]);
    }

    [Fact]
    public void Parse_RealtimeUpdate_ReadsPrefixAndSkipsCameraStrings()
    {
        var w = new Wire();
        w.U8(2)
            .U16(5).U16(2)     // event, session index
            .U8(2).U8(4)       // session type, phase
            .F32(90_000)       // session time ms
            .F32(0)            // session end time
            .I32(3)            // focused car index
            .Str("set").Str("cam").Str("hud") // camera/hud strings skipped
            .U8(0)             // not replaying
            .F32(43_200)       // time of day
            .U8(24).U8(31)     // ambient, track temp
            .U8(12).U8(3).U8(8) // clouds, rain, wetness
            .Lap(97_500, 3, 0, [30_000], false, true, false, false);

        var update = Assert.IsType<AccBroadcastRealtimeUpdate>(AccBroadcastProtocol.Parse(w.Bytes()));

        Assert.Equal(5, update.EventIndex);
        Assert.Equal(2, update.SessionIndex);
        Assert.Equal(3, update.FocusedCarIndex);
        Assert.Equal(90_000, update.SessionTimeMs);
        Assert.Equal(24, update.AmbientTempC);
        Assert.Equal(31, update.TrackTempC);
        Assert.Equal(1.2, update.Clouds, 3);
        Assert.Equal(97_500, update.BestSessionLap!.LapTimeMs);
    }

    [Fact]
    public void Parse_RealtimeUpdate_SkipsReplayTimesWhenReplaying()
    {
        var w = new Wire();
        w.U8(2)
            .U16(0).U16(0).U8(2).U8(4).F32(0).F32(0).I32(0)
            .Str("").Str("").Str("")
            .U8(1)             // replaying
            .F32(1_000).F32(2_000) // replay session + remaining time
            .F32(0).U8(0).U8(0).U8(0).U8(0).U8(0)
            .Lap(AccBroadcastProtocol.NoTimeSentinel, 0, 0, [], false, false, false, false);

        var update = Assert.IsType<AccBroadcastRealtimeUpdate>(AccBroadcastProtocol.Parse(w.Bytes()));

        Assert.Null(update.BestSessionLap!.LapTimeMs);
    }

    [Fact]
    public void Parse_EntryList_ReadsCarIds()
    {
        var w = new Wire();
        w.U8(4).I32(7).U16(3).U16(1).U16(2).U16(5);

        var list = Assert.IsType<AccBroadcastEntryList>(AccBroadcastProtocol.Parse(w.Bytes()));

        Assert.Equal(7, list.ConnectionId);
        Assert.Equal(new ushort[] { 1, 2, 5 }, list.CarIds);
    }

    [Fact]
    public void Parse_EntryListCar_ReadsTeamNumberAndDrivers()
    {
        var w = new Wire();
        w.U8(6)
            .U16(12)              // car id
            .U8(7)                // car model type
            .Str("Kunos Racing")  // team
            .I32(42)              // race number
            .U8(1)                // cup category: ProAm
            .U8(1)                // current driver index
            .U16(24)              // nationality
            .U8(2);               // two drivers
        w.Str("Max").Str("Power").Str("MP").U8(1).U16(24);
        w.Str("Ann").Str("Other").Str("AO").U8(2).U16(99);

        var car = Assert.IsType<AccBroadcastEntryListCar>(AccBroadcastProtocol.Parse(w.Bytes()));

        Assert.Equal(12, car.CarId);
        Assert.Equal("Kunos Racing", car.TeamName);
        Assert.Equal(42, car.RaceNumber);
        Assert.Equal(AccCupCategory.ProAm, car.CupCategory);
        Assert.Equal(1, car.CurrentDriverIndex);
        Assert.Equal(2, car.Drivers.Count);
        Assert.Equal("Max", car.Drivers[0].FirstName);
        Assert.Equal("Power", car.Drivers[0].LastName);
        Assert.Equal("AO", car.Drivers[1].ShortName);
        Assert.Equal(99, car.Drivers[1].Nationality);
    }

    [Fact]
    public void Parse_TrackData_ReadsNameAndLength()
    {
        var w = new Wire();
        w.U8(5).I32(7).Str("spa").I32(11).I32(7004);

        var track = Assert.IsType<AccBroadcastTrackData>(AccBroadcastProtocol.Parse(w.Bytes()));

        Assert.Equal("spa", track.TrackName);
        Assert.Equal(11, track.TrackId);
        Assert.Equal(7004, track.TrackMeters);
    }

    [Fact]
    public void Parse_BroadcastingEvent_ReadsFields()
    {
        var w = new Wire();
        w.U8(7).U8(3).Str("penalty").I32(12_345).I32(4);

        var evt = Assert.IsType<AccBroadcastEvent>(AccBroadcastProtocol.Parse(w.Bytes()));

        Assert.Equal(3, evt.Type);
        Assert.Equal("penalty", evt.Message);
        Assert.Equal(12_345, evt.TimeMs);
        Assert.Equal(4, evt.CarId);
    }

    [Fact]
    public void Parse_UnknownMessageType_ReturnsNull()
    {
        var w = new Wire();
        w.U8(200);

        Assert.Null(AccBroadcastProtocol.Parse(w.Bytes()));
    }

    [Fact]
    public void Parse_TruncatedDatagram_Throws()
    {
        var w = new Wire();
        w.U8(3).U16(1); // claims a realtime car update but stops early

        Assert.Throws<AccBroadcastFormatException>(() => AccBroadcastProtocol.Parse(w.Bytes()));
    }

    [Fact]
    public void Parse_EmptyDatagram_Throws()
    {
        Assert.Throws<AccBroadcastFormatException>(() => AccBroadcastProtocol.Parse(ReadOnlySpan<byte>.Empty));
    }

    private static string ReadString(byte[] bytes, ref int offset)
    {
        var length = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
        offset += 2;
        var value = Encoding.UTF8.GetString(bytes, offset, length);
        offset += length;
        return value;
    }

    /// <summary>Minimal little-endian wire fixture builder.</summary>
    private sealed class Wire
    {
        private readonly List<byte> _bytes = [];

        public Wire U8(byte v)
        {
            _bytes.Add(v);
            return this;
        }

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

        public Wire Raw(byte[] v)
        {
            _bytes.AddRange(v);
            return this;
        }

        public Wire Lap(int timeMs, ushort car, ushort driver, int[] splits, bool invalid, bool validForBest, bool outlap, bool inlap)
        {
            I32(timeMs).U16(car).U16(driver).U8((byte)splits.Length);
            foreach (var split in splits)
            {
                I32(split);
            }

            return U8((byte)(invalid ? 1 : 0))
                .U8((byte)(validForBest ? 1 : 0))
                .U8((byte)(outlap ? 1 : 0))
                .U8((byte)(inlap ? 1 : 0));
        }

        public byte[] Bytes() => _bytes.ToArray();
    }
}
