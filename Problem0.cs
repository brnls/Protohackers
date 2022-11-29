namespace ProtoHackers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Net;

public class Problem0_EchoServer
{
    public static async Task Init(int port)
    {
        var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        listenSocket.Bind(new IPEndPoint(IPAddress.Any, port));

        listenSocket.Listen();

        while (true)
        {
            var socket = await listenSocket.AcceptAsync();
            _ = Handle(socket);
        }
    }

    static async Task Handle(Socket socket)
    {
        using var stream = new NetworkStream(socket, true);
        await stream.CopyToAsync(stream);
    }
}
