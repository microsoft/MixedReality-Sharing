using Microsoft.MixedReality.Sharing.Channels;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network.Test.Mocks
{
    internal class MockBasicChannelFactory : BasicDataChannelFactoryBase
    {
        public override string Name => "Mock Implementation of Basic Channels";

        protected async override Task<ReliableChannel> OpenReliableChannelAsync(ISession session, string channelId, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return new MockReliableChannel(channelId, (MockSession)session, null);
        }

        protected async override Task<ReliableChannel> OpenReliableChannelAsync(IEndpoint endpoint, string channelId, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return new MockReliableChannel(channelId, (MockSession)endpoint.Session, (MockEndpoint)endpoint);
        }

        protected async override Task<ReliableOrderedChannel> OpenReliableOrderedChannelAsync(ISession session, string channelId, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return new MockReliableOrderedChannel(channelId, (MockSession)session, null);
        }

        protected async override Task<ReliableOrderedChannel> OpenReliableOrderedChannelAsync(IEndpoint endpoint, string channelId, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return new MockReliableOrderedChannel(channelId, (MockSession)endpoint.Session, (MockEndpoint)endpoint);
        }

        protected async override Task<UnreliableChannel> OpenUnreliableChannelAsync(ISession session, string channelId, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return new MockUnreliableChannel(channelId, (MockSession)session, null);
        }

        protected async override Task<UnreliableChannel> OpenUnreliableChannelAsync(IEndpoint endpoint, string channelId, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return new MockUnreliableChannel(channelId, (MockSession)endpoint.Session, (MockEndpoint)endpoint);
        }

        protected async override Task<UnreliableOrderedChannel> OpenUnreliableOrderedChannelAsync(ISession session, string channelId, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return new MockUnreliableOrderedChannel(channelId, (MockSession)session, null);
        }

        protected async override Task<UnreliableOrderedChannel> OpenUnreliableOrderedChannelAsync(IEndpoint endpoint, string channelId, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return new MockUnreliableOrderedChannel(channelId, (MockSession)endpoint.Session, (MockEndpoint)endpoint);
        }

        private static async Task SendMessageAsync<TChannel>(Stream stream, MockSession ownerSession, MockEndpoint targetLocalEndpoint, Action<TChannel, IEndpoint, byte[]> raiseMessageReceived, CancellationToken cancellationToken)
            where TChannel : BasicDataChannel
        {
            byte[] bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);

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

                raiseMessageReceived(await targetRemoteEndpoint.GetChannelAsync<TChannel>(cancellationToken), targetRemoteEndpoint, bytes);
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

                    raiseMessageReceived(await targetRemoteSession.GetChannelAsync<TChannel>(cancellationToken), targetRemoteEndpoint, bytes);
                }
            }
        }

        private class MockReliableChannel : ReliableChannel
        {
            private readonly MockSession ownerSession;
            private readonly MockEndpoint targetLocalEndpoint;

            public MockReliableChannel(string id, MockSession ownerSession, MockEndpoint targetLocalEndpoint)
                : base(id)
            {
                this.ownerSession = ownerSession;
                this.targetLocalEndpoint = targetLocalEndpoint;
            }

            public async override Task SendMessageAsync(Stream stream, CancellationToken cancellationToken)
            {
                await SendMessageAsync<ReliableChannel>(stream, ownerSession, targetLocalEndpoint, (c, e, b) => ((MockReliableChannel)c).RaiseMessageReceived(e, b), cancellationToken);
            }
        }

        private class MockUnreliableChannel : UnreliableChannel
        {
            private readonly MockSession ownerSession;
            private readonly MockEndpoint targetLocalEndpoint;

            public MockUnreliableChannel(string id, MockSession ownerSession, MockEndpoint targetLocalEndpoint)
                : base(id)
            {
                this.ownerSession = ownerSession;
                this.targetLocalEndpoint = targetLocalEndpoint;
            }

            public async override Task SendMessageAsync(Stream stream, CancellationToken cancellationToken)
            {
                await SendMessageAsync<UnreliableChannel>(stream, ownerSession, targetLocalEndpoint, (c, e, b) => ((MockUnreliableChannel)c).RaiseMessageReceived(e, b), cancellationToken);
            }
        }

        private class MockReliableOrderedChannel : ReliableOrderedChannel
        {
            private readonly MockSession ownerSession;
            private readonly MockEndpoint targetLocalEndpoint;

            public MockReliableOrderedChannel(string id, MockSession ownerSession, MockEndpoint targetLocalEndpoint)
                : base(id)
            {
                this.ownerSession = ownerSession;
                this.targetLocalEndpoint = targetLocalEndpoint;
            }

            public async override Task SendMessageAsync(Stream stream, CancellationToken cancellationToken)
            {
                await SendMessageAsync<ReliableOrderedChannel>(stream, ownerSession, targetLocalEndpoint, (c, e, b) => ((MockReliableOrderedChannel)c).RaiseMessageReceived(e, b), cancellationToken);
            }
        }

        private class MockUnreliableOrderedChannel : UnreliableOrderedChannel
        {
            private readonly MockSession ownerSession;
            private readonly MockEndpoint targetLocalEndpoint;

            public MockUnreliableOrderedChannel(string id, MockSession ownerSession, MockEndpoint targetLocalEndpoint)
                : base(id)
            {
                this.ownerSession = ownerSession;
                this.targetLocalEndpoint = targetLocalEndpoint;
            }

            public async override Task SendMessageAsync(Stream stream, CancellationToken cancellationToken)
            {
                await SendMessageAsync<UnreliableOrderedChannel>(stream, ownerSession, targetLocalEndpoint, (c, e, b) => ((MockUnreliableOrderedChannel)c).RaiseMessageReceived(e, b), cancellationToken);
            }
        }
    }
}
