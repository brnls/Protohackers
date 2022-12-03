using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Buffers;
using System.IO.Pipelines;

namespace ProtoHackers;

public class Problem5_MobInTheMiddle
{
    public static Regex BoguscoinRegex = new(@"(?<=( |^))(7[a-zA-Z0-9]{25,34})(?=( |$))");

    public static async Task Init(int port)
    {
        var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        listenSocket.Bind(new IPEndPoint(IPAddress.Any, port));

        listenSocket.Listen();

        while (true)
        {
            var socket = await listenSocket.AcceptAsync();
            _ = Task.Run(() => Handle(socket));
        }
    }

    static async Task Handle(Socket socket)
    {
        using var clientToServerStream = new NetworkStream(socket);
        using var client = new TcpClient("chat.protohackers.com", 16963);
        using var serverToClientStream = client.GetStream();
        async Task ReplaceAddress(Stream sourceStream, Stream destinationStream)
        {
            await foreach(var message in GetMessages(sourceStream))
            {
                await destinationStream.WriteAsync(Encoding.ASCII.GetBytes(BoguscoinRegex.Replace(message, "7YWHMfk9JZe0LM0g1ZauHuiSxhI")));
            }
        }

        await Task.WhenAny(
            Task.Run(() => ReplaceAddress(clientToServerStream, serverToClientStream)),
            Task.Run(() => ReplaceAddress(serverToClientStream, clientToServerStream)));
    }

    // Would like to have used StreamReader here but the protocol requires a new line after every message
    // and stream reader can't distinguish "message<EOF>" versus "message\n" so needed to do it manually
    // with a pipe.
    static async IAsyncEnumerable<string> GetMessages(Stream stream)
    {
        var pipe = PipeReader.Create(stream);

        while (true)
        {
            var result = await pipe.ReadAsync();
            var buffer = result.Buffer;

            while (TryReadLine(ref buffer, out var line))
            {
                yield return Encoding.UTF8.GetString(line);
            }

            pipe.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted) break;
        }

        await pipe.CompleteAsync();
    }

    static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
    {
        var position = buffer.PositionOf((byte)'\n');

        if (position == null)
        {
            line = default;
            return false;
        }

        line = buffer.Slice(0, buffer.GetPosition(1, position.Value));
        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
        return true;
    }
}
