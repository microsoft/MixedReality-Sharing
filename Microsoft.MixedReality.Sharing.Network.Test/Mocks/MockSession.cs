using Microsoft.MixedReality.Sharing.Core;
using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network.Test.Mocks
{
    public class MockSession : SessionBase<MockSession, MockEndpoint>
    {
        internal new Dictionary<MockEndpoint, MockSession> ConnectedEndpointsMap => base.ConnectedEndpointsMap;

        public MockSession(ILogger logger, IEnumerable<IChannelFactory<IChannel>> channelFactories)
            : base(logger, channelFactories)
        {
        }

        private MockSession(MockSession otherSession)
            : base(otherSession.channelFactoriesMap)
        {
        }

        /// <summary>
        /// Considering this is all mock and local, this method will add an endpoint to current session and create a new session that has an endpoint to this session.
        /// All factories are duplicated.
        /// </summary>
        /// <returns></returns>
        public MockSession CreateConection()
        {
            MockSession toReturn = new MockSession(this);
            ConnectedEndpointsMap.Add(new MockEndpoint(this), toReturn);
            toReturn.ConnectedEndpointsMap.Add(new MockEndpoint(toReturn), this);
            return toReturn;
        }
    }
}
