using Microsoft.MixedReality.Sharing.Core;
using System.Net.Sockets;

namespace Microsoft.MixedReality.Sharing.Sockets.Core
{
    public class SocketEndpoint : EndpointBase<SocketSession, SocketEndpoint>
    {
        public Socket Socket { get; }

        internal SocketEndpoint(SocketSession session, Socket socket) : base(session)
        {
            Socket = socket;
        }
    }
}
