using Microsoft.MixedReality.Sharing.Matchmaking;
using Microsoft.MixedReality.Sharing.Sockets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Test.Mocks
{
    public class MockSessionFactory : ISessionFactory<UDPMulticastRoomConfiguration>
    {
        public Task<UDPMulticastRoomConfiguration> HostNewRoomAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new UDPMulticastRoomConfiguration("Random ID: " + Guid.NewGuid(), 5477, 5478));
        }

        public Task<ISession> JoinSessionAsync(UDPMulticastRoomConfiguration configuration)
        {
            throw new NotImplementedException();
        }
    }
}
