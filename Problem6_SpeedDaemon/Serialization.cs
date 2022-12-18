using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;

namespace ProtoHackers.Problem6_SpeedDaemon;

class Serialization
{
    public static async IAsyncEnumerable<object> GetRequests(PipeReader pipe, [EnumeratorCancellation] CancellationToken token)
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
        var messageSize = 1 + ticket.Plate.Length + 1 + 16;
        var span = writer.GetSpan(messageSize);
        span[0] = 0x21;
        span[1] = (byte)ticket.Plate.Length;
        int index = 2;
        Encoding.ASCII.GetBytes(ticket.Plate, span[index..]);
        index = index + ticket.Plate.Length;
        BinaryPrimitives.WriteInt16BigEndian(span[index..], (short)ticket.Road);
        index = index + 2;
        BinaryPrimitives.WriteInt16BigEndian(span[index..], (short)ticket.Mile1);
        index = index + 2;
        BinaryPrimitives.WriteInt32BigEndian(span[index..], (int)ticket.Timestamp1);
        index = index + 4;
        BinaryPrimitives.WriteInt16BigEndian(span[index..], (short)ticket.Mile2);
        index = index + 2;
        BinaryPrimitives.WriteInt32BigEndian(span[index..], (int)ticket.Timestamp2);
        index = index + 4;
        BinaryPrimitives.WriteInt16BigEndian(span[index..], (short)ticket.Speed);
        writer.Advance(messageSize);
    }

    public static void Write(Error error, IBufferWriter<byte> writer)
    {
        Span<byte> span = stackalloc byte[2];
        span[0] = 0x10;
        span[1] = (byte)error.Msg.Length;
        writer.Write(span);
        Encoding.ASCII.GetBytes(error.Msg, writer);
    }

    public static void Write(Heartbeat _, IBufferWriter<byte> writer)
    {
        writer.Write(Heartbeat.Span);
    }

    private static object? TryReadRequestFromSequence(ref ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);
        var result = TryReadRequest(ref reader);
        if(result != null)
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
