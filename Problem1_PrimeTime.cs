using System.Net.Sockets;
using System.Net;
using System.IO.Pipelines;
using System.Text.Json;
using System.Buffers;
using System.Text.Unicode;
using System.Text;

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
                _ = Handle(socket);
            }
        }

        // largely copied from https://learn.microsoft.com/en-us/dotnet/standard/io/pipelines#pipe-basic-usage
        static async Task Handle(Socket socket)
        {
            using var stream = new NetworkStream(socket, true);
            var reader = PipeReader.Create(stream);

            try
            {
                while (true)
                {
                    ReadResult result = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                    {
                        // Process the line.
                        var handleResult = await HandleMessage(line, stream);
                        if (!handleResult)
                        {
                            throw new Exception();
                        }
                    }

                    // Tell the PipeReader how much of the buffer has been consumed.
                    reader.AdvanceTo(buffer.Start, buffer.End);

                    // Stop reading if there's no more data coming.
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            catch (Exception e) 
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes("malformed"));
                Console.WriteLine(e);
            }

            // Mark the PipeReader as complete.
            await reader.CompleteAsync();
        }

        static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
        {
            SequencePosition? position = buffer.PositionOf((byte)'\n');

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
            var jsonMessage = JsonDocument.Parse(message);

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
            double upperBound = Math.Sqrt(n);
            for(long i = 2; i <= upperBound; i++)
            {
                if (n % i == 0) return false;
            }
            return true;
        }
    }
}
