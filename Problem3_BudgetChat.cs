namespace ProtoHackers;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Collections.Concurrent;
using System.Buffers;
using System.IO.Pipelines;

public class Problem3_BudgetChat
{
    public static async Task Init(int port)
    {
        var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        listenSocket.Bind(new IPEndPoint(IPAddress.IPv6Any, port));

        listenSocket.Listen();

        var connectionHandler = new ConnectionHandler();
        var chatServer = new ChatServer(connectionHandler);
        _ = Task.Run(chatServer.HandleMessages);

        while (true)
        {
            var socket = await listenSocket.AcceptAsync();
            _ = Task.Run(() => Handle(socket, connectionHandler));
        }
    }

    static Regex UserNameRegex = new Regex("^[a-zA-Z0-9]*$");

    private static async Task Handle(Socket socket, ConnectionHandler handler)
    {
        var stream = new NetworkStream(socket, true);
        await stream.WriteAsync(Encoding.UTF8.GetBytes($"Welcome! What is your name?\n"));
        var nameBuffer = new byte[17];
        await stream.ReadAsync(nameBuffer);
        var endIndex = Array.FindIndex(nameBuffer, x => x == (byte)'\n');

        static async Task InvalidName(NetworkStream stream)
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes($"Invalid name\n"));
            stream.Close();
        }

        Console.WriteLine($"Getting name {Encoding.UTF8.GetString(nameBuffer)}");
        if (endIndex == -1)
        {
            await InvalidName(stream);
            return;
        }
        var name = Encoding.UTF8.GetString(nameBuffer.AsMemory(0, endIndex).Span);

        if (!UserNameRegex.IsMatch(name))
        {
            await InvalidName(stream);
            return;
        }

        handler.AddConnection(name, stream);
    }


    class ChatServer
    {
        private readonly ConnectionHandler _handler;

        public ChatServer(ConnectionHandler handler)
        {
            _handler = handler;
        }

        public async Task HandleMessages()
        {
            await foreach(var message in _handler.Messages)
            {
                try
                {
                    Console.WriteLine($"Processing {message}");
                    if (message is UserLeft u)
                    {
                        await _handler.SendAllBut(u.User, $"* {u.User.Username} has left the room");
                    }
                    else if (message is UserJoined j)
                    {
                        var userList = _handler.Users.Where(x => x.Username != j.User.Username).Select(x => x.Username);
                        var broadcastJoinedTask = _handler.SendAllBut(j.User, $"* {j.User.Username} has joined the room");
                        var listUsersTask = _handler.Send(
                            j.User,
                            $"* The room contains: {string.Join(",", userList)}");
                        await Task.WhenAll(broadcastJoinedTask, listUsersTask);
                    }
                    else if (message is UserMessage m)
                    {
                        await _handler.SendAllBut(m.User, $"[{m.User.Username}] {m.Message}");
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Error in chat server loop: {ex}");
                    await Task.Delay(5000);
                }
            }
        }
    }

    class ConnectionHandler
    {
        private Channel<object> _messagesChannel = Channel.CreateBounded<object>(100);
        private ConcurrentDictionary<User, NetworkStream> UserConnections = new();

        public IAsyncEnumerable<object> Messages => _messagesChannel.Reader.ReadAllAsync();
        public IEnumerable<User> Users => UserConnections.Keys;

        public void AddConnection(string userName, NetworkStream stream)
        {
            var user = new User(Guid.NewGuid().ToString("D"), userName);
            UserConnections.TryAdd(user, stream);
            _ = Task.Run(() => HandleUserInput(user, stream));
        }

        private async Task HandleUserInput(User user, NetworkStream stream)
        {
            await _messagesChannel.Writer.WriteAsync(new UserJoined(user));
            try
            {
                var reader = PipeReader.Create(stream);
                while (true)
                {
                    var result = await reader.ReadAsync();
                    var buffer = result.Buffer;

                    while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                    {
                        await _messagesChannel.Writer.WriteAsync(
                            new UserMessage(
                                user,
                                EncodingExtensions.GetString(Encoding.UTF8, line)));
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);
                    if(result.IsCompleted)
                    {
                        break;
                    }
                }
                await reader.CompleteAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error handling user {user.Username}: {e}");
            }
            finally
            {
                UserConnections.TryRemove(user, out _);
                _messagesChannel.Writer.TryWrite(new UserLeft(user));
                stream.Dispose();
            }
        }

        public async Task Send(User user, string message)
        {
            if(!UserConnections.TryGetValue(user, out var stream)) return;
            await WriteMessage(stream, message);
        }

        public async Task SendAllBut(User user, string message)
        {
            await Task.WhenAll(UserConnections.Where(x => x.Key != user).Select(x => Task.Run(() =>
                WriteMessage(x.Value, message)
            )));
        }

        private static async Task WriteMessage(NetworkStream stream, string message)
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(message));
            stream.WriteByte((byte)'\n');
        }

        static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
        {
            var position = buffer.PositionOf((byte)'\n');

            if (position == null)
            {
                line = default;
                return false;
            }

            line = buffer.Slice(0, position.Value);
            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
            return true;
        }
    }

    record User(string ConnectionId, string Username);
    record UserJoined(User User);
    record UserLeft(User User);
    record UserMessage(User User, string Message);
}
