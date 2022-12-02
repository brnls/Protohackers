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
                    throw new Exception("this branch isn't hit?");
                }
            }
            else // insert key/value
            {
                if (messageBuffer[..equalIndex].Span.SequenceEqual("version"u8)) continue;
                var kvp = messageBuffer.ToArray().AsMemory();
                kvpStore[kvp[..equalIndex]] = kvp;
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
}
