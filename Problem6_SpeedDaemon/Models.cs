using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtoHackers.Problem6_SpeedDaemon
{
    interface IClient
    {
        ValueTask WriteResponse(object o, CancellationToken token);
        public object ClientInfo { get; }
    }

    enum ClientType
    {
        Camera,
        Dispatcher
    }
    //Client -> Server messages
    record Plate(string Value, uint Timestamp);
    record WantHeartbeat(uint Interval);
    record IAmCamera(ushort Road, ushort Mile, ushort Limit);
    record IAmDispatcher(ushort[] Roads);

    // Server -> Client messages
    record Heartbeat();
    record Ticket(
        string Plate,
        ushort Road,
        ushort Mile1,
        uint Timestamp1,
        ushort Mile2,
        uint Timestamp2,
        ushort Speed);
    record Error(string Msg);

    // Client Control
    record Disconnect(IClient Client);
    record Connect(IClient Client);
    record PlateObserved(Plate Plate, IAmCamera Camera);
}
