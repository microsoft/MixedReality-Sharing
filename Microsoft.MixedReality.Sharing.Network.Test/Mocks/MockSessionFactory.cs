using Microsoft.MixedReality.Sharing.Matchmaking;
using Microsoft.MixedReality.Sharing.Network.Test.Mocks;
using Microsoft.MixedReality.Sharing.Sockets;
using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Test.Mocks
{
    public class MockSessionFactory : ISessionFactory<UDPMulticastRoomConfiguration>
    {
        private readonly ILogger logger;
        private readonly IEnumerable<IChannelFactory<IChannel>> channelFactories;
        private readonly IPAddress localAddress = IPAddress.Any;

        public MockSessionFactory(ILogger logger, IEnumerable<IChannelFactory<IChannel>> channelFactories)
        {
            this.logger = logger;
            this.channelFactories = channelFactories;
        }

        public Task<KeyValuePair<UDPMulticastRoomConfiguration, ISession>> HostNewRoomAsync(IDictionary<string, string> attributes, CancellationToken cancellationToken)
        {
            return Task.FromResult(new KeyValuePair<UDPMulticastRoomConfiguration, ISession>(new UDPMulticastRoomConfiguration(IPAddress.Any, 5477, 5478), new MockSession(logger, channelFactories)));
        }

        public Task<ISession> JoinSessionAsync(UDPMulticastRoomConfiguration configuration, IReadOnlyDictionary<string, string> attributes, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
