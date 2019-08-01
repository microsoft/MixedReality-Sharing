using Microsoft.MixedReality.Sharing.Core;
using Microsoft.MixedReality.Sharing.Utilities;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Sharing.Network.Test.Mocks
{
    public class MockSession : SessionBase<MockSession, MockEndpoint>
    {
        internal Dictionary<MockEndpoint, MockSession> ConnectedEndpointsMap { get; } = new Dictionary<MockEndpoint, MockSession>();

        public MockSession(ILogger logger, IEnumerable<IChannelFactory<IChannel>> channelFactories)
            : base(logger, channelFactories)
        {
        }

        private MockSession(MockSession otherSession)
            : base(otherSession.logger, otherSession.channelFactoriesMap)
        {
        }

        /// <summary>
        /// Considering this is all mock and local, this method will add an endpoint to current session and create a new session that has an endpoint to this session.
        /// All factories are duplicated.
        /// </summary>
        /// <returns></returns>
        public MockSession CreateConnection()
        {
            MockSession remoteSession = new MockSession(this);
            MockEndpoint localEndpoint = new MockEndpoint(this);

            ConnectedEndpointsMap.Add(localEndpoint, remoteSession);
            OnEndpointConnected(localEndpoint);

            MockEndpoint remoteEndpoint = new MockEndpoint(remoteSession);
            remoteSession.ConnectedEndpointsMap.Add(remoteEndpoint, this);
            remoteSession.OnEndpointConnected(remoteEndpoint);

            return remoteSession;
        }
    }
}
