using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network.Test.Mocks
{
    internal class MockSimpleChannelFactory : SimpleMessageChannelFactoryBase<MockSession>
    {
        private class MockSimpleChannel : SimpleMessageChannelBase<MockSession>
        {
            private readonly MockSession ownerSession;
            private readonly MockEndpoint targetLocalEndpoint;

            public MockSimpleChannel(SimpleChannelType simpleChannelType, MockSession ownerSession, MockEndpoint targetLocalEndpoint) : base(simpleChannelType)
            {
                this.ownerSession = ownerSession;
                this.targetLocalEndpoint = targetLocalEndpoint;
            }

            protected override async Task OnSendMessageAsync(IMessage message, CancellationToken cancellationToken)
            {
                byte[] bytes;
                using (Stream stream = await message.OpenReadAsync(cancellationToken))
                {
                    bytes = new byte[stream.Length];
                    stream.Read(bytes, 0, bytes.Length);
                }

                if (targetLocalEndpoint != null)
                {
                    if (!ownerSession.ConnectedEndpointsMap.TryGetValue(targetLocalEndpoint, out MockSession targetRemoteSession))
                    {
                        throw new InvalidOperationException("Endpoint disconnected.");
                    }

                    //target remote endpoint represents this session in the other session endpoint list
                    MockEndpoint targetRemoteEndpoint = targetRemoteSession.ConnectedEndpointsMap.FirstOrDefault(t => t.Value == ownerSession).Key;

                    if (targetRemoteEndpoint == null)
                    {
                        throw new InvalidOperationException("Something wrong with the mock connection.");
                    }

                    switch (simpleChannelType)
                    {
                        case SimpleChannelType.Reliable:
                            ((MockSimpleChannel)await targetRemoteEndpoint.GetChannelAsync<ReliableMessage>(cancellationToken)).OnMessageReceived(targetRemoteEndpoint, bytes);
                            break;
                        case SimpleChannelType.Unreliable:
                            ((MockSimpleChannel)await targetRemoteEndpoint.GetChannelAsync<UnreliableMessage>(cancellationToken)).OnMessageReceived(targetRemoteEndpoint, bytes);
                            break;
                        case SimpleChannelType.ReliableOrdered:
                            ((MockSimpleChannel)await targetRemoteEndpoint.GetChannelAsync<ReliableOrderedMessage>(cancellationToken)).OnMessageReceived(targetRemoteEndpoint, bytes);
                            break;
                        case SimpleChannelType.UnreliableOrdered:
                            ((MockSimpleChannel)await targetRemoteEndpoint.GetChannelAsync<UnreliableOrderedMessage>(cancellationToken)).OnMessageReceived(targetRemoteEndpoint, bytes);
                            break;
                    }
                }
                else
                {
                    // Broadcast to all endpoints
                    foreach (MockSession targetRemoteSession in ownerSession.ConnectedEndpointsMap.Values)
                    {
                        //target remote endpoint represents this session in the other session endpoint list
                        MockEndpoint targetRemoteEndpoint = targetRemoteSession.ConnectedEndpointsMap.FirstOrDefault(t => t.Value == ownerSession).Key;

                        if (targetRemoteEndpoint == null)
                        {
                            throw new InvalidOperationException("Something wrong with the mock connection.");
                        }

                        switch (simpleChannelType)
                        {
                            case SimpleChannelType.Reliable:
                                ((MockSimpleChannel)await targetRemoteSession.GetChannelAsync<ReliableMessage>(cancellationToken)).OnMessageReceived(targetRemoteEndpoint, bytes);
                                break;
                            case SimpleChannelType.Unreliable:
                                ((MockSimpleChannel)await targetRemoteSession.GetChannelAsync<UnreliableMessage>(cancellationToken)).OnMessageReceived(targetRemoteEndpoint, bytes);
                                break;
                            case SimpleChannelType.ReliableOrdered:
                                ((MockSimpleChannel)await targetRemoteSession.GetChannelAsync<ReliableOrderedMessage>(cancellationToken)).OnMessageReceived(targetRemoteEndpoint, bytes);
                                break;
                            case SimpleChannelType.UnreliableOrdered:
                                ((MockSimpleChannel)await targetRemoteSession.GetChannelAsync<UnreliableOrderedMessage>(cancellationToken)).OnMessageReceived(targetRemoteEndpoint, bytes);
                                break;
                        }
                    }
                }
            }

            protected override async Task<bool> OnTryReconnectAsync(CancellationToken cancellationToken)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                return true;
            }
        }

        protected override async Task<SimpleMessageChannelBase<MockSession>> CreateSimpleChannelAsync(SimpleChannelType type, MockSession session, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return new MockSimpleChannel(type, session, null);
        }

        protected override async Task<SimpleMessageChannelBase<MockSession>> CreateSimpleChannelAsync(SimpleChannelType type, IEndpoint<MockSession> endpoint, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return new MockSimpleChannel(type, endpoint.Session, (MockEndpoint)endpoint);
        }
    }
}
