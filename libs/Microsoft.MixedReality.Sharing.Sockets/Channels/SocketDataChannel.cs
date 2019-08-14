using Microsoft.MixedReality.Sharing.Channels;
using Microsoft.MixedReality.Sharing.Sockets.Core;
using Microsoft.MixedReality.Sharing.Utilities;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Sockets.Channels
{
    public class SocketDataChannel : BasicDataChannel
    {
        private readonly SocketSession session;
        private readonly SocketEndpoint endpoint;

        public SocketDataChannel(SocketSession session, SocketEndpoint endpoint, string id)
            : base(id)
        {
            this.session = session;
            this.endpoint = endpoint;
        }

        protected override void OnSendMessage(Stream stream)
        {
            if (endpoint != null)
            {
                Task.Run(() => endpoint.Socket.SendDataAsync(stream).IgnoreSocketAbort()).FireAndForget();
            }
        }
    }
}
