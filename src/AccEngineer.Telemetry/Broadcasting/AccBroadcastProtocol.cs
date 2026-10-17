using System.Buffers.Binary;
using System.Text;

namespace AccEngineer.Telemetry.Broadcasting;

/// <summary>
/// Message types a broadcasting client sends to ACC (outbound).
/// Mirrors Kunos's <c>ksBroadcastingNetwork</c> reference, protocol version 4.
/// </summary>
public enum AccBroadcastOutboundMessageType : byte
{
    Register = 1,
    Unregister = 9,
    RequestEntryList = 10,
    RequestTrackData = 11
}

/// <summary>Message types ACC sends to a broadcasting client (inbound).</summary>
public enum AccBroadcastInboundMessageType : byte
{
    RegistrationResult = 1,
    RealtimeUpdate = 2,
    RealtimeCarUpdate = 3,
    EntryList = 4,
    TrackData = 5,
    EntryListCar = 6,
    BroadcastingEvent = 7
}

/// <summary>Where a car is on/off track (<c>RealtimeCarUpdate.car_location</c>).</summary>
public enum AccCarLocation : byte
{
    None = 0,
    Track = 1,
    Pitlane = 2,
    PitEntry = 3,
    PitExit = 4
}

/// <summary>Cup class of a car entry (<c>EntryListCar.cup_category</c>).</summary>
public enum AccCupCategory : byte
{
    Overall = 0,
    ProAm = 1,
    Am = 2,
    Silver = 3,
    National = 4
}

/// <summary>Thrown when an inbound datagram is shorter than the message needs.</summary>
public sealed class AccBroadcastFormatException : Exception
{
    public AccBroadcastFormatException(string message) : base(message) { }
}

/// <summary>Base type for a parsed inbound broadcasting message.</summary>
public abstract record AccBroadcastMessage;

/// <summary>Reply to a registration request.</summary>
public sealed record AccBroadcastRegistrationResult(
    int ConnectionId,
    bool Success,
    bool ReadOnly,
    string Error) : AccBroadcastMessage;

/// <summary>
/// One lap as reported by the broadcasting API. <see cref="LapTimeMs"/> and each
/// split are null when the wire value is the "no time" sentinel (Int32.MaxValue).
/// </summary>
public sealed record AccBroadcastLapInfo(
    int? LapTimeMs,
    ushort CarIndex,
    ushort DriverIndex,
    IReadOnlyList<int?> Splits,
    bool IsInvalid,
    bool IsValidForBest,
    bool IsOutlap,
    bool IsInlap);

/// <summary>
/// Session-level state (<c>realtime_update</c>). <see cref="SessionType"/> and
/// <see cref="SessionPhase"/> are raw ACC enumerations. Times are milliseconds.
/// </summary>
public sealed record AccBroadcastRealtimeUpdate(
    ushort EventIndex,
    ushort SessionIndex,
    byte SessionType,
    byte SessionPhase,
    float SessionTimeMs,
    float SessionEndTimeMs,
    int FocusedCarIndex,
    float TimeOfDay,
    int AmbientTempC,
    int TrackTempC,
    double Clouds,
    double RainLevel,
    double Wetness,
    AccBroadcastLapInfo? BestSessionLap) : AccBroadcastMessage;

/// <summary>
/// Per-car state (<c>realtime_car_update</c>). <see cref="Position"/> is the
/// official overall position (1-based), <see cref="TrackPosition"/> the position
/// within the car's class, and <see cref="DeltaMs"/> the signed gap to the
/// reference lap (negative = ahead). <see cref="GearByte"/> is the raw wire gear
/// byte (0 = reverse, 1 = neutral, 2 = first), the same encoding as ACC shared
/// memory; Kunos's reference subtracts 1.
/// </summary>
public sealed record AccBroadcastRealtimeCarUpdate(
    ushort CarIndex,
    ushort DriverIndex,
    byte DriverCount,
    byte GearByte,
    float WorldPositionX,
    float WorldPositionY,
    float Yaw,
    AccCarLocation Location,
    int SpeedKmh,
    ushort Position,
    ushort CupPosition,
    ushort TrackPosition,
    float SplinePosition,
    int Laps,
    int DeltaMs,
    AccBroadcastLapInfo? BestSessionLap,
    AccBroadcastLapInfo? LastLap,
    AccBroadcastLapInfo? CurrentLap) : AccBroadcastMessage;

/// <summary>The IDs of cars in the session (<c>entry_list</c>).</summary>
public sealed record AccBroadcastEntryList(
    int ConnectionId,
    IReadOnlyList<ushort> CarIds) : AccBroadcastMessage;

/// <summary>One driver assigned to a car entry.</summary>
public sealed record AccBroadcastDriverInfo(
    string FirstName,
    string LastName,
    string ShortName,
    byte Category,
    ushort Nationality);

/// <summary>
/// Identity and crew of one car (<c>entry_list_car</c>). Names come only from
/// the rival game session via this message; they are never invented.
/// </summary>
public sealed record AccBroadcastEntryListCar(
    ushort CarId,
    byte CarModelType,
    string TeamName,
    int RaceNumber,
    AccCupCategory CupCategory,
    byte CurrentDriverIndex,
    ushort Nationality,
    IReadOnlyList<AccBroadcastDriverInfo> Drivers) : AccBroadcastMessage;

/// <summary>Track identity and length (<c>track_data</c>). <see cref="TrackMeters"/> is used to convert spline positions into gaps.</summary>
public sealed record AccBroadcastTrackData(
    int ConnectionId,
    string TrackName,
    int TrackId,
    int TrackMeters) : AccBroadcastMessage;

/// <summary>A broadcasting event such as a penalty or session message.</summary>
public sealed record AccBroadcastEvent(
    byte Type,
    string Message,
    int TimeMs,
    int CarId) : AccBroadcastMessage;

/// <summary>
/// Pure (socket-free) encoder/decoder for the ACC UDP Broadcasting API
/// (protocol version 4). Little-endian throughout; every inbound datagram carries
/// exactly one message, so the decoder reads only the fields it needs.
/// Layouts are cross-checked against Kunos's <c>ksBroadcastingNetwork</c>
/// reference and two independent community readers. Not yet confirmed against a
/// live ACC instance from this project; see <c>docs/overlay.md</c>.
/// </summary>
public static class AccBroadcastProtocol
{
    public const byte ProtocolVersion = 4;

    /// <summary>Sentinel ACC uses for "no time recorded yet".</summary>
    public const int NoTimeSentinel = int.MaxValue;

    /// <summary>Parses one inbound datagram, or null for message types this parser does not decode.</summary>
    public static AccBroadcastMessage? Parse(ReadOnlySpan<byte> datagram)
    {
        var reader = new Reader(datagram);
        var type = reader.ReadByte();

        return type switch
        {
            (byte)AccBroadcastInboundMessageType.RegistrationResult => ParseRegistrationResult(ref reader),
            (byte)AccBroadcastInboundMessageType.RealtimeUpdate => ParseRealtimeUpdate(ref reader),
            (byte)AccBroadcastInboundMessageType.RealtimeCarUpdate => ParseRealtimeCarUpdate(ref reader),
            (byte)AccBroadcastInboundMessageType.EntryList => ParseEntryList(ref reader),
            (byte)AccBroadcastInboundMessageType.TrackData => ParseTrackData(ref reader),
            (byte)AccBroadcastInboundMessageType.EntryListCar => ParseEntryListCar(ref reader),
            (byte)AccBroadcastInboundMessageType.BroadcastingEvent => ParseBroadcastingEvent(ref reader),
            _ => null
        };
    }

    /// <summary>Builds the registration request. Passwords may be empty strings.</summary>
    public static byte[] BuildRegister(
        string displayName,
        string connectionPassword,
        int updateIntervalMs,
        string commandPassword)
    {
        var writer = new Writer();
        writer.WriteByte((byte)AccBroadcastOutboundMessageType.Register);
        writer.WriteByte(ProtocolVersion);
        writer.WriteString(displayName);
        writer.WriteString(connectionPassword);
        writer.WriteInt32(updateIntervalMs);
        writer.WriteString(commandPassword);
        return writer.ToArray();
    }

    /// <summary>Builds the deregistration request.</summary>
    public static byte[] BuildUnregister(int connectionId)
    {
        var writer = new Writer();
        writer.WriteByte((byte)AccBroadcastOutboundMessageType.Unregister);
        writer.WriteInt32(connectionId);
        return writer.ToArray();
    }

    /// <summary>Builds a request for the full entry list.</summary>
    public static byte[] BuildRequestEntryList(int connectionId)
    {
        var writer = new Writer();
        writer.WriteByte((byte)AccBroadcastOutboundMessageType.RequestEntryList);
        writer.WriteInt32(connectionId);
        return writer.ToArray();
    }

    /// <summary>Builds a request for track data (name and length).</summary>
    public static byte[] BuildRequestTrackData(int connectionId)
    {
        var writer = new Writer();
        writer.WriteByte((byte)AccBroadcastOutboundMessageType.RequestTrackData);
        writer.WriteInt32(connectionId);
        return writer.ToArray();
    }

    private static AccBroadcastRegistrationResult ParseRegistrationResult(ref Reader r)
    {
        var connectionId = r.ReadInt32();
        var success = r.ReadByte() > 0;
        var readOnly = r.ReadByte() > 0;
        var error = r.ReadString();
        return new AccBroadcastRegistrationResult(connectionId, success, readOnly, error);
    }

    private static AccBroadcastRealtimeUpdate ParseRealtimeUpdate(ref Reader r)
    {
        var eventIndex = r.ReadUInt16();
        var sessionIndex = r.ReadUInt16();
        var sessionType = r.ReadByte();
        var phase = r.ReadByte();
        var sessionTime = r.ReadFloat();
        var sessionEndTime = r.ReadFloat();
        var focused = r.ReadInt32();
        r.SkipString(); // active camera set
        r.SkipString(); // active camera
        r.SkipString(); // current HUD page
        var replay = r.ReadByte() > 0;
        if (replay)
        {
            r.Skip(8); // replay session time + replay remaining time (two floats)
        }

        var timeOfDay = r.ReadFloat();
        var ambient = r.ReadByte();
        var trackTemp = r.ReadByte();
        var clouds = r.ReadByte() / 10.0;
        var rain = r.ReadByte() / 10.0;
        var wetness = r.ReadByte() / 10.0;
        var best = r.ReadLap();

        return new AccBroadcastRealtimeUpdate(
            eventIndex, sessionIndex, sessionType, phase, sessionTime, sessionEndTime,
            focused, timeOfDay, ambient, trackTemp, clouds, rain, wetness, best);
    }

    private static AccBroadcastRealtimeCarUpdate ParseRealtimeCarUpdate(ref Reader r)
    {
        var carIndex = r.ReadUInt16();
        var driverIndex = r.ReadUInt16();
        var driverCount = r.ReadByte();
        var gear = r.ReadByte();
        var x = r.ReadFloat();
        var y = r.ReadFloat();
        var yaw = r.ReadFloat();
        var location = (AccCarLocation)r.ReadByte();
        var speedKmh = r.ReadUInt16();
        var position = r.ReadUInt16();
        var cupPosition = r.ReadUInt16();
        var trackPosition = r.ReadUInt16();
        var spline = r.ReadFloat();
        var laps = r.ReadUInt16();
        var delta = r.ReadInt32();
        var best = r.ReadLap();
        var last = r.ReadLap();
        var current = r.ReadLap();

        return new AccBroadcastRealtimeCarUpdate(
            carIndex, driverIndex, driverCount, gear, x, y, yaw, location, speedKmh,
            position, cupPosition, trackPosition, spline, laps, delta, best, last, current);
    }

    private static AccBroadcastEntryList ParseEntryList(ref Reader r)
    {
        var connectionId = r.ReadInt32();
        var count = r.ReadUInt16();
        var ids = new ushort[count];
        for (var i = 0; i < count; i++)
        {
            ids[i] = r.ReadUInt16();
        }

        return new AccBroadcastEntryList(connectionId, ids);
    }

    private static AccBroadcastEntryListCar ParseEntryListCar(ref Reader r)
    {
        var carId = r.ReadUInt16();
        var carModelType = r.ReadByte();
        var teamName = r.ReadString();
        var raceNumber = r.ReadInt32();
        var cupCategory = (AccCupCategory)r.ReadByte();
        var currentDriverIndex = r.ReadByte();
        var nationality = r.ReadUInt16();

        var driverCount = r.ReadByte();
        var drivers = new AccBroadcastDriverInfo[driverCount];
        for (var i = 0; i < driverCount; i++)
        {
            var first = r.ReadString();
            var last = r.ReadString();
            var shortName = r.ReadString();
            var category = r.ReadByte();
            var driverNationality = r.ReadUInt16();
            drivers[i] = new AccBroadcastDriverInfo(first, last, shortName, category, driverNationality);
        }

        return new AccBroadcastEntryListCar(
            carId, carModelType, teamName, raceNumber, cupCategory,
            currentDriverIndex, nationality, drivers);
    }

    private static AccBroadcastTrackData ParseTrackData(ref Reader r)
    {
        var connectionId = r.ReadInt32();
        var name = r.ReadString();
        var trackId = r.ReadInt32();
        var meters = r.ReadInt32();
        return new AccBroadcastTrackData(connectionId, name, trackId, meters);
    }

    private static AccBroadcastEvent ParseBroadcastingEvent(ref Reader r)
    {
        var type = r.ReadByte();
        var message = r.ReadString();
        var timeMs = r.ReadInt32();
        var carId = r.ReadInt32();
        return new AccBroadcastEvent(type, message, timeMs, carId);
    }

    /// <summary>Little-endian forward cursor over one datagram.</summary>
    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _pos;

        public Reader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _pos = 0;
        }

        public byte ReadByte()
        {
            Ensure(1);
            return _data[_pos++];
        }

        public ushort ReadUInt16()
        {
            Ensure(2);
            var value = BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(_pos));
            _pos += 2;
            return value;
        }

        public int ReadInt32()
        {
            Ensure(4);
            var value = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(_pos));
            _pos += 4;
            return value;
        }

        public float ReadFloat()
        {
            Ensure(4);
            var value = BinaryPrimitives.ReadSingleLittleEndian(_data.Slice(_pos));
            _pos += 4;
            return value;
        }

        public string ReadString()
        {
            var length = ReadUInt16();
            Ensure(length);
            var value = Encoding.UTF8.GetString(_data.Slice(_pos, length));
            _pos += length;
            return value;
        }

        public void Skip(int count)
        {
            Ensure(count);
            _pos += count;
        }

        /// <summary>Skips a length-prefixed UTF-8 string without decoding it.</summary>
        public void SkipString() => Skip(ReadUInt16());

        public AccBroadcastLapInfo? ReadLap()
        {
            var lapTime = ReadInt32();
            var carIndex = ReadUInt16();
            var driverIndex = ReadUInt16();
            var splitCount = ReadByte();
            var splits = new int?[splitCount];
            for (var i = 0; i < splitCount; i++)
            {
                splits[i] = Normalise(ReadInt32());
            }

            var isInvalid = ReadByte() > 0;
            var isValidForBest = ReadByte() > 0;
            var isOutlap = ReadByte() > 0;
            var isInlap = ReadByte() > 0;

            return new AccBroadcastLapInfo(
                Normalise(lapTime), carIndex, driverIndex, splits,
                isInvalid, isValidForBest, isOutlap, isInlap);
        }

        private static int? Normalise(int ms) =>
            ms == NoTimeSentinel || ms < 0 ? null : ms;

        private void Ensure(int count)
        {
            if (_pos + count > _data.Length)
            {
                throw new AccBroadcastFormatException(
                    $"Datagram truncated: need {count} byte(s) at offset {_pos} but only {_data.Length} present.");
            }
        }
    }

    /// <summary>Little-endian forward writer for outbound messages.</summary>
    private sealed class Writer
    {
        private byte[] _buffer = new byte[64];
        private int _length;

        public void WriteByte(byte value)
        {
            Ensure(1);
            _buffer[_length++] = value;
        }

        public void WriteInt32(int value)
        {
            Ensure(4);
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_length), value);
            _length += 4;
        }

        public void WriteString(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > ushort.MaxValue)
            {
                throw new ArgumentException("String too long for a length-prefixed field.", nameof(value));
            }

            WriteUInt16((ushort)bytes.Length);
            Ensure(bytes.Length);
            bytes.CopyTo(_buffer, _length);
            _length += bytes.Length;
        }

        private void WriteUInt16(ushort value)
        {
            Ensure(2);
            BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(_length), value);
            _length += 2;
        }

        public byte[] ToArray() => _buffer.AsSpan(0, _length).ToArray();

        private void Ensure(int count)
        {
            if (_length + count <= _buffer.Length)
            {
                return;
            }

            var grown = new byte[Math.Max(_buffer.Length * 2, _length + count)];
            _buffer.AsSpan(0, _length).CopyTo(grown);
            _buffer = grown;
        }
    }
}
