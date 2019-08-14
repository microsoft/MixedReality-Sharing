using Microsoft.MixedReality.Sharing.Core;
using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Sockets.Core
{
    public class SocketSession : SessionBase<SocketSession, SocketEndpoint>
    {
        public static SocketSession CreateServerSessionAsync(ILogger logger, IEnumerable<IChannelFactory<IChannel>> channelFactories, IPAddress address, ushort port)
        {
            SocketSession toReturn = new SocketSession(logger, channelFactories, address, port);

            Task.Run(() => toReturn.StartServerAsync(toReturn.DisposeCancellationToken), toReturn.DisposeCancellationToken).FireAndForget();

            return toReturn;
        }

        public static async Task<SocketSession> CreateClientSessionAsync(ILogger logger, IEnumerable<IChannelFactory<IChannel>> channelFactories, IPAddress address, ushort port, CancellationToken cancellationToken)
        {
            SocketSession toReturn = new SocketSession(logger, channelFactories, address, port);
            try
            {
                await toReturn.ConnectClientAsync(cancellationToken);
            }
            catch
            {
                toReturn.Dispose();
                throw;
            }
            return toReturn;
        }

        private readonly IPAddress address;
        private readonly ushort port;
        private readonly Socket socket;

        private SocketSession(ILogger logger, IEnumerable<IChannelFactory<IChannel>> channelFactories, IPAddress address, ushort port)
            : base(logger, channelFactories)
        {
            this.address = address;
            this.port = port;

            // Server or client will share same
            socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Consider only keeping for debug
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
        }

        private async Task StartServerAsync(CancellationToken cancellationToken)
        {
            try
            {
                socket.Bind(new IPEndPoint(address, port));
                socket.Listen(100);

                while (!cancellationToken.IsCancellationRequested)
                {
                    Socket client = await socket.AcceptAsync();
                    OnEndpointConnected(new SocketEndpoint(this, client));
                }
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.OperationAborted)
                {
                    throw;
                }
            }
            catch (ObjectDisposedException) { }
        }

        private async Task ConnectClientAsync(CancellationToken cancellationToken)
        {
            try
            {
                await socket.ConnectAsync(address, port);
                OnEndpointConnected(new SocketEndpoint(this, socket));

            }
            catch (ObjectDisposedException) { }
        }

        protected override void OnManagedDispose()
        {
            base.OnManagedDispose();

            socket.Close();
            socket.Dispose();
        }
    }
}
