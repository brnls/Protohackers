using System.Net.Sockets;
using System.Net;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace ProtoHackers;
class Problem4_UnusualDatabase
{
    public static async Task Init(int port)
    {
        using var client = new UdpClient(port);
        IPEndPoint groupEp = new IPEndPoint(IPAddress.Any, port);
        var requestBuffer = new byte[1000];

        var kvpStore = new Dictionary<Memory<byte>, Memory<byte>>(new ByteArrayEqualityComparer())
        {
            ["version"u8.ToArray().AsMemory()] = "version=UDP DB"u8.ToArray()
        };
        while (true)
        {
            var result = await client.Client.ReceiveFromAsync(requestBuffer, SocketFlags.None, groupEp);
            var messageBuffer = requestBuffer.AsMemory(0, result.ReceivedBytes);
            var equalIndex = messageBuffer.Span.IndexOf((byte)'=');
            if(equalIndex == -1) // query by key
            {
                var exists = kvpStore.TryGetValue(messageBuffer, out var value);
                if (exists)
                {
                    await client.Client.SendToAsync(value, SocketFlags.None, result.RemoteEndPoint);
                }
                else
                {
                    throw new Exception("this is never hit");
                }
            }
            else // insert key/value
            {
                if (messageBuffer[..equalIndex].Span.SequenceEqual("version"u8)) continue;
                var messageBufferCopy = messageBuffer.ToArray().AsMemory();
                kvpStore[messageBufferCopy[..equalIndex]] = messageBufferCopy;
            }
        }
    }

    class ByteArrayEqualityComparer : IEqualityComparer<Memory<byte>>
    {
        public bool Equals(Memory<byte> x, Memory<byte> y) =>
            x.Span.SequenceEqual(y.Span);

        public int GetHashCode([DisallowNull] Memory<byte> obj)
        {
            var hash = new HashCode();
            hash.AddBytes(obj.Span);
            return hash.ToHashCode();
        }
    }

    public static async Task InitTestClient(int serverPort)
    {
        try
        {
            int port = 5010;
            using var client = new UdpClient(port);
            IPEndPoint groupEp = new IPEndPoint(IPAddress.Any, port);
            client.Connect("127.0.0.1", serverPort);
            await Task.Delay(2000);
            while (true)
            {
                Console.WriteLine("sending");
                await client.SendAsync("test"u8.ToArray());
                await Task.Delay(2000);
            }
        }
        catch(Exception ex) { 
            Console.WriteLine(ex.ToString());
        }
    }
}
