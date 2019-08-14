using Microsoft.MixedReality.Sharing.Matchmaking;
using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Sockets.Core
{
    public class SocketSessionFactory : ISessionFactory<UDPMulticastRoomConfiguration>
    {
        private readonly ILogger logger;
        private readonly IEnumerable<IChannelFactory<IChannel>> channelFactories;
        private readonly IPAddress localAddress = IPAddress.Any;

        public SocketSessionFactory(ILogger logger, IEnumerable<IChannelFactory<IChannel>> channelFactories)
        {
            this.logger = logger;
            this.channelFactories = channelFactories;
        }

        public Task<KeyValuePair<UDPMulticastRoomConfiguration, ISession>> HostNewRoomAsync(IDictionary<string, string> attributes, CancellationToken cancellationToken)
        {
            UDPMulticastRoomConfiguration roomConfig = new UDPMulticastRoomConfiguration(Guid.NewGuid().ToString(), localAddress, 5477, 5478);
            SocketSession session = SocketSession.CreateServerSessionAsync(logger, channelFactories, roomConfig.Address, roomConfig.DataPort);

            return Task.FromResult(new KeyValuePair<UDPMulticastRoomConfiguration, ISession>(roomConfig, session));
        }

        public async Task<ISession> JoinSessionAsync(UDPMulticastRoomConfiguration configuration, IReadOnlyDictionary<string, string> attributes, CancellationToken cancellationToken)
        {
            return await SocketSession.CreateClientSessionAsync(logger, channelFactories, configuration.Address, configuration.DataPort, cancellationToken);
        }
    }
}
