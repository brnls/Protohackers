using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace ProtoHackers.Problem6_SpeedDaemon;

public class TestClient
{
    public static async Task Run()
    {
        //var requests = Channel.CreateUnbounded<object>();
        //var server = new TicketingServer(requests.Reader);
        //_ = server.Run();

        //var clientPipe = new Pipe();
        //var client = new Client(requests.Writer, clientPipe.Writer, clientPipe.Reader);
        //client.ClientInfo = new IAmDispatcher(new ushort[] { 1 });
        //_ = client.WriteResponsesToStream();
        //requests.Writer.TryWrite(new Connect(client));
        //requests.Writer.TryWrite(new PlateObserved(new Plate("abc", 35858110), new IAmCamera(1, 90, 50)));
        //requests.Writer.TryWrite(new PlateObserved(new Plate("abc", 35862538), new IAmCamera(1, 2, 50)));
        //requests.Writer.TryWrite(new PlateObserved(new Plate("abc", 35824143), new IAmCamera(1, 765, 50)));
        //requests.Writer.TryWrite(new PlateObserved(new Plate("abc", 35853732), new IAmCamera(1, 177, 50)));
        //requests.Writer.TryWrite(new PlateObserved(new Plate("abc", 35827464), new IAmCamera(1, 699, 50)));
        //requests.Writer.TryWrite(new PlateObserved(new Plate("abc", 35840045), new IAmCamera(1, 449, 50)));
        //await Task.Delay(-1);

        await Task.WhenAll(Enumerable.Range(0, 10).Select(async x =>
        {
            using var client1 = new TcpClient();
            await client1.ConnectAsync(IPAddress.Loopback, 5011);
            var stream = client1.GetStream();

            //await stream.WriteAsync(Convert.FromHexString("8014840AAB0064200745453036554E4D00009D71"));
            //await stream.WriteAsync(Convert.FromHexString("8014840AAB0064200745453036554E4D00009D71"));
            await stream.WriteAsync(Convert.FromHexString("8101"));
            await stream.WriteAsync(Convert.FromHexString("1484"));
            await stream.WriteAsync(Convert.FromHexString("8101"));
            await stream.WriteAsync(Convert.FromHexString("1484"));
            _ = stream.ReadByte();
            var b = stream.ReadByte();
            var buf = new byte[b];
            stream.Read(buf);
            Console.WriteLine(Encoding.UTF8.GetString(buf));

        }));
        //using var client2 = new TcpClient();
        //await client2.ConnectAsync(IPAddress.Loopback, 5011);
        //var stream2 = client2.GetStream();

        //await stream2.WriteAsync(Convert.FromHexString("8014840AA10064200745453036554E4D00009C45"));

        //using var client3 = new TcpClient();
        //await client3.ConnectAsync(IPAddress.Loopback, 5011);
        //var stream3 = client3.GetStream();
        //await stream3.WriteAsync(Convert.FromHexString("8101"));
        //await stream3.FlushAsync();
        //await Task.Delay(5000);
        //await stream3.WriteAsync(Convert.FromHexString("1484"));
        //var bytes = new byte[8000];
        //var read = await stream3.ReadAsync(bytes);
        //var result = bytes[..read];

        //client3.Close();
        //client3.Close();
        //client3.Dispose();
        //stream3.Dispose();
        //await Task.Delay(-1);
        //// Plate
        //await stream.WriteAsync(new byte[]
        //{
        //    0x20, //type
        //    0x04, 0x55, 0x4e, 0x31, 0x58, // "UNIX"
        //    0x00, 0x00, 0x03, 0xe8 // 1000 unit32
        //});

        //// IAmDispatcher
        //await stream.WriteAsync(new byte[]
        //{
        //    0x81, //type
        //    0x03, // road count
        //    0x00, 0x42, // 66
        //    0x01, 0x70, // 368
        //    0x13, 0x88  // 5000
        //});
    }
}