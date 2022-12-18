using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;

namespace ProtoHackers.Problem6_SpeedDaemon;

class Serialization
{
    public static async IAsyncEnumerable<object> ReadRequests(PipeReader pipe, [EnumeratorCancellation] CancellationToken token)
    {
        while (true)
        {
            var result = await pipe.ReadAsync(token);
            var buffer = result.Buffer;

            while (TryReadRequestFromSequence(ref buffer) is object requeest)
            {
                yield return requeest;
            }

            pipe.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted) break;
        }

        await pipe.CompleteAsync();
    }

    public static void Write(Ticket ticket, IBufferWriter<byte> writer)
    {
        int msgLength = 1 + ticket.Plate.Length + 1 + 16;
        var span = writer.GetSpan(msgLength);
        Write(ref span, 0x21);
        Write(ref span, ticket.Plate);
        Write(ref span, ticket.Road);
        Write(ref span, ticket.Mile1);
        Write(ref span, ticket.Timestamp1);
        Write(ref span, ticket.Mile2);
        Write(ref span, ticket.Timestamp2);
        Write(ref span, ticket.Speed);
        writer.Advance(msgLength);
    }

    public static void Write(Error error, IBufferWriter<byte> writer)
    {
        var msgLength = 1 + 1 + error.Msg.Length;
        var span = writer.GetSpan(msgLength);
        Write(ref span, 0x10);
        Write(ref span, error.Msg);
        writer.Advance(msgLength);
    }

    public static void Write(Heartbeat _, IBufferWriter<byte> writer)
    {
        writer.Write(Heartbeat.Span);
    }

    private static void Write(ref Span<byte> span, byte value)
    {
        span[0] = value;
        span = span[1..];
    }

    private static void Write(ref Span<byte> span, ushort value)
    {
        BinaryPrimitives.WriteInt16BigEndian(span, (short)value);
        span = span[2..];
    }

    private static void Write(ref Span<byte> span, uint value)
    {
        BinaryPrimitives.WriteInt32BigEndian(span, (int)value);
        span = span[4..];
    }

    private static void Write(ref Span<byte> span, string value)
    {
        span[0] = (byte)value.Length;
        Encoding.ASCII.GetBytes(value, span[1..]);
        span = span[(value.Length + 1)..];
    }

    private static object? TryReadRequestFromSequence(ref ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);
        var result = TryReadRequest(ref reader);
        if (result != null)
        {
            buffer = buffer.Slice(reader.Position);
        }
        return result;
    }

    private static object? TryReadRequest(ref SequenceReader<byte> reader)
    {
        var success = reader.TryRead(out var type);
        if (!success)
        {
            return null;
        }

        // The type checks cannot be combined with "TryParse..." in a single conditional. It is
        // possible we have received a partial message, in which case we should return null and not
        // "Error" so we can attempt to parse the full message when we receive more data from the pipe.
        if (type is 0x20)
        {
            if (TryParseLengthPrefixedString(ref reader) is string str &&
                reader.TryReadBigEndian(out int timestamp))
            {
                return new Plate(str, (uint)timestamp);
            }
        }
        else if (type is 0x40)
        {
            if (reader.TryReadBigEndian(out int timestamp))
            {
                return new WantHeartbeat((uint)timestamp);
            }
        }
        else if (type is 0x80)
        {
            if (reader.TryReadBigEndian(out short road) &&
                reader.TryReadBigEndian(out short mile) &&
                reader.TryReadBigEndian(out short limit))
            {
                return new IAmCamera((ushort)road, (ushort)mile, (ushort)limit);
            }
        }
        else if (type is 0x81)
        {
            if (TryParseBytePrefixedU16Array(ref reader) is ushort[] roads)
            {
                return new IAmDispatcher(roads);
            }
        }
        else return new Error("Unexpected message type");

        return null;
    }

    private static string? TryParseLengthPrefixedString(ref SequenceReader<byte> reader)
    {
        if (!reader.TryRead(out var stringLength))
        {
            return null;
        }

        if (!reader.TryReadExact(stringLength, out var stringBytes))
        {
            return null;
        };

        return Encoding.ASCII.GetString(stringBytes);
    }

    private static ushort[]? TryParseBytePrefixedU16Array(ref SequenceReader<byte> buffer)
    {
        if (!buffer.TryRead(out var itemCount))
        {
            return null;
        }

        if (buffer.UnreadSequence.Length < 2 * itemCount) return null;
        var arr = new ushort[itemCount];
        for (byte i = 0; i < itemCount; i++)
        {
            // This can't fail to parse because we checked the buffer length above
            if (buffer.TryReadBigEndian(out short value))
            {
                arr[i] = (ushort)value;
            }
        }

        return arr;
    }

    static readonly ReadOnlyMemory<byte> Heartbeat = new byte[] { 0x41 };
}
