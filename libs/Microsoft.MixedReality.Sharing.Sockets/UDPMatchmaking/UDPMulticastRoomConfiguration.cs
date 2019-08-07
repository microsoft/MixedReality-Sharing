using Microsoft.MixedReality.Sharing.Matchmaking;

namespace Microsoft.MixedReality.Sharing.Sockets
{
    public struct UDPMulticastRoomConfiguration : IRoomConfiguration
    {
        public string Id { get; }

        public ushort InfoPort { get; }

        public ushort DataPort { get; }

        public UDPMulticastRoomConfiguration(string id, ushort infoPort, ushort dataPort)
        {
            Id = id;
            InfoPort = infoPort;
            DataPort = dataPort;
        }
    }
}
