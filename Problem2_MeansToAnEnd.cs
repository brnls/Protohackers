namespace ProtoHackers;

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

public class Problem2_MeansToAnEnd
{
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
        using var stream = new NetworkStream(socket, true);
        var requestBuffer = new byte[9];
        var responseBuffer = new byte[4];
        var prices = new List<HistoricalPrice>();
        while (true)
        {
            await stream.ReadExactlyAsync(requestBuffer);
            if (requestBuffer[0] == (byte)'I')
            {
                var historicalPrice = ParseHistoricalPrice(requestBuffer.AsSpan(1));
                prices.Add(historicalPrice);
            }
            else if (requestBuffer[0] == (byte)'Q')
            {
                prices.Sort((a, b) => a.Timestamp - b.Timestamp);
                var query = ParseQuery(requestBuffer.AsSpan(1));
                var average = GetAverage(prices, query);

                BitConverter.TryWriteBytes(responseBuffer, (int)Math.Floor(average));
                BinaryPrimitives.WriteInt32BigEndian(responseBuffer, (int)Math.Floor(average));
                await stream.WriteAsync(responseBuffer);
            }
            else break;
        }
    }

    private static double GetAverage(List<HistoricalPrice> prices, Query query)
    {
        if (query.MinTime > query.MaxTime) return 0;
        return prices
            .SkipWhile(x => x.Timestamp < query.MinTime)
            .TakeWhile(x => x.Timestamp <= query.MaxTime)
            .Select(x => x.Price)
            .DefaultIfEmpty(0)
            .Average();
    }

    static HistoricalPrice ParseHistoricalPrice(Span<byte> span)
    {
        return new HistoricalPrice(
            Timestamp: GetInt(span[..4]),
            Price: GetInt(span[4..8]));
    }

    static Query ParseQuery(Span<byte> span)
    {
        return new Query(
            MinTime: GetInt(span[..4]),
            MaxTime: GetInt(span[4..8]));
    }

    static int GetInt(Span<byte> span)
    {
        return BinaryPrimitives.ReadInt32BigEndian(span);
    }

    record HistoricalPrice(int Timestamp, int Price);
    record Query(int MinTime, int MaxTime);
}
