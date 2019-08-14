using Microsoft.MixedReality.Sharing.Channels;
using Microsoft.MixedReality.Sharing.Sockets.Core;

namespace Microsoft.MixedReality.Sharing.Sockets.Channels
{
    public class SocketDataChannelFactory : BasicDataChannelFactoryBase<SocketSession, SocketEndpoint>
    {
        protected override BasicDataChannel GetChannel(SocketSession session, string channelId)
        {
            return new SocketDataChannel(session, null, channelId);
        }

        protected override BasicDataChannel GetChannel(SocketEndpoint endpoint, string channelId)
        {
            return new SocketDataChannel(endpoint.Session, endpoint, channelId);
        }
    }
}
