using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace ProtoHackers.Problem6_SpeedDaemon;

public static class Problem
{
    public static async Task Init(int port)
    {
        var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        listenSocket.Bind(new IPEndPoint(IPAddress.Any, port));

        listenSocket.Listen();

        Channel<object> requests = Channel.CreateBounded<object>(10);
        var ticketingServer = new TicketingServer(requests.Reader);
        _ = Task.Run(ticketingServer.Run);

        while (true)
        {
            var socket = await listenSocket.AcceptAsync();
            _ = Task.Run(() => HandleClientRequests(socket, requests.Writer));
        }
    }

    private static async Task HandleClientRequests(Socket socket, ChannelWriter<object> clientToServerChannel)
    {
        using var stream = new NetworkStream(socket, true);
        var reader = PipeReader.Create(stream);
        var writer = PipeWriter.Create(stream);
        using var client = new Client(clientToServerChannel, writer, reader);
        await Task.WhenAll(
            client.WriteResponsesToStream(),
            client.HandleRequests());
    }
}