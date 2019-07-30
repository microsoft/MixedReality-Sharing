using Microsoft.MixedReality.Sharing.Network.Channels;
using MorseCode.ITask;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Core
{
    public class EndpointBase<TSession, TEndpoint> : IEndpoint
        where TSession : SessionBase<TSession, TEndpoint>
        where TEndpoint : EndpointBase<TSession, TEndpoint>
    {
        private readonly ConcurrentDictionary<ChannelMapKey, ITask<IChannel>> openedChannels = new ConcurrentDictionary<ChannelMapKey, ITask<IChannel>>();

        public TSession Session { get; }

        ISession IEndpoint.Session => Session;

        protected EndpointBase(TSession session)
        {
            Session = session;
        }

        public async Task<TChannel> GetChannelAsync<TChannel>(string channelId, CancellationToken cancellationToken) where TChannel : IChannel
        {
            Session.ThrowIfDisposed();

            IChannel channel = await openedChannels.GetOrAdd(new ChannelMapKey(typeof(ChannelMapKey), channelId), key => CreateChannelFor(key, cancellationToken));

            return (TChannel)channel;
        }

        private async ITask<IChannel> CreateChannelFor(ChannelMapKey key, CancellationToken cancellationToken)
        {
            return await ChannelsUtility.GetChannelFactory(Session.ChannelFactoriesMap, key.Type).OpenChannelAsync(this, key.ChannelId, cancellationToken);
        }
    }
}
