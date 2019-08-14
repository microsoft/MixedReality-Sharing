using Microsoft.MixedReality.Sharing.Matchmaking;
using System.Net;

namespace Microsoft.MixedReality.Sharing.Sockets
{
    public struct UDPMulticastRoomConfiguration : IRoomConfiguration
    {
        public string Id { get; }

        public IPAddress Address { get; }

        public ushort InfoPort { get; }

        public ushort DataPort { get; }

        public UDPMulticastRoomConfiguration(string id, IPAddress address, ushort infoPort, ushort dataPort)
        {
            Id = id;
            Address = address;
            InfoPort = infoPort;
            DataPort = dataPort;
        }
    }
}
