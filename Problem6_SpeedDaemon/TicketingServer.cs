using System.Threading.Channels;

namespace ProtoHackers.Problem6_SpeedDaemon;

class TicketingServer
{
    private readonly Dictionary<(string plate, ushort road), List<(uint timestamp, ushort mile)>> _plateObservations = new();
    private readonly List<IClient> _clients = new();
    private readonly List<Ticket> _pendingTickets = new();
    private readonly HashSet<(uint day, string plate)> _ticketsGiven = new();
    private readonly ChannelReader<object> _messageReader;

    public TicketingServer(ChannelReader<object> messageReader)
    {
        _messageReader = messageReader;
    }

    public async Task Run()
    {
        await foreach (var message in _messageReader.ReadAllAsync())
        {
            if (message is Connect c)
            {
                _clients.Add(c.Client);
            }
            else if (message is Disconnect d)
            {
                _clients.Remove(d.Client);
            }
            else if (message is PlateObserved p)
            {
                // Check if we need to issue any new tickets
                List<(uint timestamp, ushort mile)> observations = null!;
                if (!_plateObservations.TryGetValue((p.Plate.Value, p.Camera.Road), out observations!))
                {
                    observations = new List<(uint timestamp, ushort mile)>();
                    _plateObservations[(p.Plate.Value, p.Camera.Road)] = observations;
                }
                var newObservation = (p.Plate.Timestamp, p.Camera.Mile);
                var insertIndex = observations.FindIndex(x => p.Plate.Timestamp < x.timestamp) switch
                {
                    -1 => observations.Count == 0 ? 0 : observations.Count,
                    int x => x
                };

                observations.Insert(insertIndex, newObservation);

                // compare with the plate observation previous to this one
                if (insertIndex != 0)
                {
                    var (firstTimestamp, firstMile) = observations[insertIndex - 1];
                    var (secondTimestamp, secondMile) = observations[insertIndex];
                    EvaluateForTicket(p, firstTimestamp, firstMile, secondTimestamp, secondMile);
                }

                // compare with the plate observation after this one
                if (observations.Count > insertIndex + 1)
                {
                    var (firstTimestamp, firstMile) = observations[insertIndex];
                    var (secondTimestamp, secondMile) = observations[insertIndex + 1];
                    EvaluateForTicket(p, firstTimestamp, firstMile, secondTimestamp, secondMile);
                }
            }

            await IssuePendingTickets();
        }
    }

    private void EvaluateForTicket(
        PlateObserved p,
        uint firstTimestamp,
        ushort firstMile,
        uint secondTimestamp,
        ushort secondMile)
    {
        var totalSeconds = secondTimestamp - firstTimestamp;
        var totalMiles = Math.Abs(secondMile - firstMile);
        var speed = 3600 * totalMiles / (float)totalSeconds;

        if (speed > p.Camera.Limit)
        {

            var firstDay = GetDay(firstTimestamp);
            var secondDay = GetDay(secondTimestamp);

            var firstTicket = (firstDay, p.Plate.Value);
            var secondTicket = (secondDay, p.Plate.Value);
            if (!(_ticketsGiven.Contains(firstTicket) || _ticketsGiven.Contains(secondTicket)))
            {
                _pendingTickets.Add(new Ticket(
                    p.Plate.Value,
                    p.Camera.Road,
                    firstMile,
                    firstTimestamp,
                    secondMile,
                    secondTimestamp,
                    (ushort)(speed * 100)));
                _ticketsGiven.Add((firstDay, p.Plate.Value));
                _ticketsGiven.Add((secondDay, p.Plate.Value));
            }
        }
    }

    async Task IssuePendingTickets()
    {
        var ticketsIssued = new List<Ticket>();
        foreach (var ticket in _pendingTickets)
        {
            var dispatcherClient = _clients.FirstOrDefault(x => x.ClientInfo is IAmDispatcher d
                && d?.Roads.Contains(ticket.Road) == true);

            if (dispatcherClient is IClient c)
            {
                await c.WriteResponse(ticket, default);
                ticketsIssued.Add(ticket);
            }
        }

        foreach (var removed in ticketsIssued)
        {
            _pendingTickets.Remove(removed);
        }
    }

    uint GetDay(uint timestamp) => timestamp / 86400;
}
