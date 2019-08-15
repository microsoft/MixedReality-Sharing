using Microsoft.MixedReality.Sharing.Matchmaking;
using System;
using System.Net;

namespace Microsoft.MixedReality.Sharing.Sockets
{
    public struct UDPMulticastRoomConfiguration : IRoomConfiguration
    {
        private readonly string id;

        public Guid Id { get; }

        string IRoomConfiguration.Id => id;

        public IPAddress Address { get; }

        public ushort InfoPort { get; }

        public ushort DataPort { get; }

        public UDPMulticastRoomConfiguration(IPAddress address, ushort infoPort, ushort dataPort)
            : this(Guid.NewGuid(), address, infoPort, dataPort)
        {

        }
        internal UDPMulticastRoomConfiguration(Guid id, IPAddress address, ushort infoPort, ushort dataPort)
        {
            Id = id;
            this.id = id.ToString();

            Address = address;
            InfoPort = infoPort;
            DataPort = dataPort;
        }
    }
}
