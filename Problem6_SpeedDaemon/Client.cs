using System.IO.Pipelines;
using System.Threading.Channels;

namespace ProtoHackers.Problem6_SpeedDaemon;

class Client : IClient, IDisposable
{
    private readonly ChannelWriter<object> _serverRequests;
    private readonly CancellationTokenSource cts;
    private readonly Channel<object> _responseChannel;
    private readonly PipeReader _reader;
    private readonly PipeWriter _writer;
    public static readonly Heartbeat Heartbeat = new();

    public Client(
        ChannelWriter<object> requestsChannel,
        PipeWriter writer,
        PipeReader reader)
    {
        _serverRequests = requestsChannel;
        cts = new CancellationTokenSource();
        _writer = writer;
        _responseChannel = Channel.CreateBounded<object>(5);
        _reader = reader;
    }

    public ValueTask WriteResponse(object o, CancellationToken token) => _responseChannel.Writer.WriteAsync(o, token);

    public bool WantsHeartbeat { get; set; }
    public object? ClientInfo { get; set; }
    object IClient.ClientInfo => ClientInfo!;

    public async Task WriteResponsesToStream()
    {
        await foreach (var response in _responseChannel.Reader.ReadAllAsync())
        {
            if (response is Heartbeat h)
            {
                Serialization.Write(h, _writer);
            }
            else if (response is Error e)
            {
                Serialization.Write(e, _writer);
            }
            else if (response is Ticket t)
            {
                Serialization.Write(t, _writer);
            }
            await _writer.FlushAsync();
        }
    }

    public async Task HandleRequests()
    {
        try
        {
            await foreach (var message in Serialization.GetRequests(_reader, cts.Token))
            {
                if (message is WantHeartbeat w)
                {
                    if (WantsHeartbeat)
                    {
                        await WriteResponse(new Error("Already sent a heartbeat request"), cts.Token);
                        break;
                    }
                    WantsHeartbeat = true;
                    _ = Task.Run(async () =>
                    {
                        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(w.Interval / 10.0));
                        while (await timer.WaitForNextTickAsync(cts.Token))
                        {
                            await WriteResponse(Heartbeat, cts.Token);
                        }
                    });
                }
                else if (message is IAmCamera or IAmDispatcher)
                {
                    if (ClientInfo is not null)
                    {
                        await WriteResponse(new Error("Client already defined its type, cannot redefine"), cts.Token);
                        break;
                    }
                    ClientInfo = message;
                    await _serverRequests.WriteAsync(new Connect(this), cts.Token);
                }
                else if (message is Plate p)
                {
                    if (ClientInfo is not IAmCamera c)
                    {
                        await WriteResponse(new Error("Only camera should report plates"), cts.Token);
                        break;
                    }
                    await _serverRequests.WriteAsync(new PlateObserved(p, c), cts.Token);
                }
                else if (message is Error e)
                {
                    await WriteResponse(new Error("Unrecognized message type"), cts.Token);
                    break;
                }
            }
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cts.Token) { }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
        finally
        {
            await _serverRequests.WriteAsync(new Disconnect(this));
            _responseChannel.Writer.TryComplete();
        }
    }

    public void Dispose()
    {
        cts.Cancel();
        cts.Dispose();
    }
}