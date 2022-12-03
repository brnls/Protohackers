using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Collections.Concurrent;

namespace ProtoHackers;
public class Problem3_BudgetChat
{
    public static async Task Init(int port)
    {
        var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        listenSocket.Bind(new IPEndPoint(IPAddress.IPv6Any, port));

        listenSocket.Listen();

        var chatServer = new ChatServer();
        _ = Task.Run(chatServer.Start);

        while (true)
        {
            var socket = await listenSocket.AcceptAsync();
            _ = Task.Run(() => chatServer.AddConnection(socket));
        }
    }

    class ChatServer
    {
        private static Regex UserNameRegex = new("^[a-zA-Z0-9]*$");
        private Channel<object> _messagesChannel = Channel.CreateBounded<object>(100);
        private ConcurrentDictionary<string, User> UserConnections = new();

        public async Task Start()
        {
            await foreach (var message in _messagesChannel.Reader.ReadAllAsync())
            {
                try
                {
                    Console.WriteLine($"Processing {message}");
                    if (message is UserLeft u)
                    {
                        await Broadcast(u.User.ConnectionId, $"* {u.User.Username} has left the room");
                    }
                    else if (message is UserJoined j)
                    {
                        var userList = UserConnections.Values.Where(x => x.Username != j.User.Username).Select(x => x.Username);
                        await Task.WhenAll(
                            Broadcast(j.User.ConnectionId, $"* {j.User.Username} has joined the room"),
                            Send( j.User.ConnectionId, $"* The room contains: {string.Join(",", userList)}"));
                    }
                    else if (message is UserMessage m)
                    {
                        await Broadcast(m.User.ConnectionId, $"[{m.User.Username}] {m.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in chat server loop: {ex}");
                    await Task.Delay(5000);
                }
            }
        }

        public async Task AddConnection(Socket socket)
        {
            await using var stream = new NetworkStream(socket, true);
            using var sr = new StreamReader(stream, Encoding.ASCII);
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Welcome! What is your name?\n"));
            var name = await sr.ReadLineAsync();

            if (!(name is string s && UserNameRegex.IsMatch(s)))
            {
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"Invalid name\n"));
                return;
            }

            var user = new User(Guid.NewGuid().ToString("D"), name, stream);
            try
            {
                UserConnections.TryAdd(user.ConnectionId, user);

                await _messagesChannel.Writer.WriteAsync(new UserJoined(user));
                while ((await sr.ReadLineAsync()) is string msg)
                {
                    await _messagesChannel.Writer.WriteAsync(new UserMessage(user, msg));
                }
            }
            finally
            {
                UserConnections.TryRemove(user.ConnectionId, out _);
                _messagesChannel.Writer.TryWrite(new UserLeft(user));
            }
        }

        public async Task Send(string fromUserId, string message)
        {
            if (!UserConnections.TryGetValue(fromUserId, out var user)) return;
            await WriteMessage(user.stream, message);
        }

        public async Task Broadcast(string fromUserId, string message)
        {
            await Task.WhenAll(UserConnections.Where(x => x.Key != fromUserId).Select(x => Task.Run(() =>
                WriteMessage(x.Value.stream, message)
            )));
        }

        private static async Task WriteMessage(NetworkStream stream, string message)
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes(message));
            stream.WriteByte((byte)'\n');
        }
    }

    record User(string ConnectionId, string Username, NetworkStream stream);
    record UserJoined(User User);
    record UserLeft(User User);
    record UserMessage(User User, string Message);
}
