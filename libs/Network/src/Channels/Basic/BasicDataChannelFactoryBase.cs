using MorseCode.ITask;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Channels
{
    public abstract class BasicDataChannelFactoryBase : IChannelFactory<ReliableChannel>, IChannelFactory<UnreliableChannel>, IChannelFactory<ReliableOrderedChannel>, IChannelFactory<UnreliableOrderedChannel>
    {
        public abstract string Name { get; }

        async ITask<UnreliableChannel> IChannelFactory<UnreliableChannel>.OpenChannelAsync(ISession session, string channelId, CancellationToken cancellationToken)
        {
            return await OpenUnreliableChannelAsync(session, channelId, cancellationToken);
        }

        async ITask<UnreliableChannel> IChannelFactory<UnreliableChannel>.OpenChannelAsync(IEndpoint endpoint, string channelId, CancellationToken cancellationToken)
        {
            return await OpenUnreliableChannelAsync(endpoint, channelId, cancellationToken);
        }

        async ITask<ReliableOrderedChannel> IChannelFactory<ReliableOrderedChannel>.OpenChannelAsync(ISession session, string channelId, CancellationToken cancellationToken)
        {
            return await OpenReliableOrderedChannelAsync(session, channelId, cancellationToken);
        }

        async ITask<ReliableOrderedChannel> IChannelFactory<ReliableOrderedChannel>.OpenChannelAsync(IEndpoint endpoint, string channelId, CancellationToken cancellationToken)
        {
            return await OpenReliableOrderedChannelAsync(endpoint, channelId, cancellationToken);
        }

        async ITask<UnreliableOrderedChannel> IChannelFactory<UnreliableOrderedChannel>.OpenChannelAsync(ISession session, string channelId, CancellationToken cancellationToken)
        {
            return await OpenUnreliableOrderedChannelAsync(session, channelId, cancellationToken);
        }

        async ITask<UnreliableOrderedChannel> IChannelFactory<UnreliableOrderedChannel>.OpenChannelAsync(IEndpoint endpoint, string channelId, CancellationToken cancellationToken)
        {
            return await OpenUnreliableOrderedChannelAsync(endpoint, channelId, cancellationToken);
        }

        async ITask<ReliableChannel> IChannelFactory<ReliableChannel>.OpenChannelAsync(ISession session, string channelId, CancellationToken cancellationToken)
        {
            return await OpenReliableChannelAsync(session, channelId, cancellationToken);
        }

        async ITask<ReliableChannel> IChannelFactory<ReliableChannel>.OpenChannelAsync(IEndpoint endpoint, string channelId, CancellationToken cancellationToken)
        {
            return await OpenReliableChannelAsync(endpoint, channelId, cancellationToken);
        }

        protected abstract Task<UnreliableChannel> OpenUnreliableChannelAsync(ISession session, string channelId, CancellationToken cancellationToken);
        protected abstract Task<UnreliableChannel> OpenUnreliableChannelAsync(IEndpoint endpoint, string channelId, CancellationToken cancellationToken);
        protected abstract Task<ReliableOrderedChannel> OpenReliableOrderedChannelAsync(ISession session, string channelId, CancellationToken cancellationToken);
        protected abstract Task<ReliableOrderedChannel> OpenReliableOrderedChannelAsync(IEndpoint endpoint, string channelId, CancellationToken cancellationToken);
        protected abstract Task<UnreliableOrderedChannel> OpenUnreliableOrderedChannelAsync(ISession session, string channelId, CancellationToken cancellationToken);
        protected abstract Task<UnreliableOrderedChannel> OpenUnreliableOrderedChannelAsync(IEndpoint endpoint, string channelId, CancellationToken cancellationToken);
        protected abstract Task<ReliableChannel> OpenReliableChannelAsync(ISession session, string channelId, CancellationToken cancellationToken);
        protected abstract Task<ReliableChannel> OpenReliableChannelAsync(IEndpoint endpoint, string channelId, CancellationToken cancellationToken);
    }
}
