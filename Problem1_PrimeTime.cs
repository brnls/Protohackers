using System.Net.Sockets;
using System.Net;
using System.IO.Pipelines;
using System.Text.Json;
using System.Buffers;
using System.Text;
using System.Diagnostics.CodeAnalysis;

namespace ProtoHackers
{
    class Problem1_PrimeTime
    {
        public static async Task Init(int port)
        {
            var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(IPAddress.IPv6Any, port));

            listenSocket.Listen();

            while (true)
            {
                var socket = await listenSocket.AcceptAsync();
                _ = Task.Run(() => Handle(socket));
            }
        }

        // largely copied from https://learn.microsoft.com/en-us/dotnet/standard/io/pipelines#pipe-basic-usage
        static async Task Handle(Socket socket)
        {
            await using var stream = new NetworkStream(socket, true);
            var reader = PipeReader.Create(stream);

            var malformed = false;
            while (!malformed)
            {
                var result = await reader.ReadAsync();
                var buffer = result.Buffer;

                while (!malformed && TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                {
                    // Process the line.
                    malformed = !(await HandleMessage(line, stream));
                }

                // Tell the PipeReader how much of the buffer has been consumed.
                reader.AdvanceTo(buffer.Start, buffer.End);

                // Stop reading if there's no more data coming.
                if (malformed || result.IsCompleted)
                {
                    break;
                }
            }

            if(malformed)
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes("malformed"));
            }
            // Mark the PipeReader as complete.
            await reader.CompleteAsync();
        }

        static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
        {
            var position = buffer.PositionOf((byte)'\n');

            if (position == null)
            {
                line = default;
                return false;
            }

            // Skip the line + the \n.
            line = buffer.Slice(0, position.Value);
            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
            return true;
        }

        static async Task<bool> HandleMessage(ReadOnlySequence<byte> message, Stream stream)
        {
            static bool TryParse(ReadOnlySequence<byte> message, [NotNullWhen(true)] out JsonDocument? doc)
            {
                var reader = new Utf8JsonReader(message);
                return JsonDocument.TryParseValue(ref reader, out doc);
            }

            if (!TryParse(message, out var jsonMessage)) return false;

            if (!(jsonMessage.RootElement.TryGetProperty("method", out JsonElement method) &&
                method.ValueKind == JsonValueKind.String &&
                method.GetString() == "isPrime")) return false;

            if (!(jsonMessage.RootElement.TryGetProperty("number", out JsonElement num) &&
                num.ValueKind == JsonValueKind.Number
                )) return false;

            await using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("method", "isPrime");
                writer.WriteBoolean("prime", num.TryGetInt64(out var integer) && IsPrime(integer));
                writer.WriteEndObject();
            }
            stream.WriteByte((byte)'\n');
            return true;
        }

        static bool IsPrime(long n)
        {
            if(n <= 1) return false;
            var upperBound = Math.Sqrt(n);
            for(long i = 2; i <= upperBound; i++)
            {
                if (n % i == 0) return false;
            }
            return true;
        }
    }
}
