using System.Net;

namespace Microsoft.MixedReality.Sharing.Sockets
{
    public class UDPMulticastSettings
    {
        public IPAddress LocalIPAddress { get; }

        public IPAddress GroupIPAddress { get; }

        public ushort MulticastPort { get; }

        public UDPMulticastSettings(IPAddress localIpAddress, IPAddress groupIPAddress, ushort multicastPort)
        {
            LocalIPAddress = localIpAddress;
            GroupIPAddress = groupIPAddress;
            MulticastPort = multicastPort;
        }
    }
}
