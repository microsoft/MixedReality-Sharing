using Microsoft.MixedReality.Sharing.Network.Channels;
using System.Collections.Concurrent;

namespace Microsoft.MixedReality.Sharing.Core
{
    /// <summary>
    /// Helper base class implementing the <see cref="IEndpoint"/>.
    /// </summary>
    /// <typeparam name="TSession">The type of session this endpoint belongs to.</typeparam>
    /// <typeparam name="TEndpoint">The type of this endpoint, to establish a strongly typed link.</typeparam>
    public class EndpointBase<TSession, TEndpoint> : IEndpoint
        where TSession : SessionBase<TSession, TEndpoint>
        where TEndpoint : EndpointBase<TSession, TEndpoint>
    {
        private readonly ConcurrentDictionary<ChannelMapKey, IChannel> openedChannels = new ConcurrentDictionary<ChannelMapKey, IChannel>();

        /// <summary>
        /// The session this endpoint belongs to.
        /// </summary>
        public TSession Session { get; }

        /// <summary>
        /// The session this endpoint belongs to.
        /// </summary>
        ISession IEndpoint.Session => Session;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="session">The <see cref="TSession"/> this endpoint is part of.</param>
        protected EndpointBase(TSession session)
        {
            Session = session;
        }

        /// <summary>
        /// Gets a channel to communicate directly to the client on the other side of this endpoint.
        /// </summary>
        public TChannel GetChannel<TChannel>(string channelId) where TChannel : IChannel
        {
            Session.ThrowIfDisposed();

            return (TChannel)openedChannels.GetOrAdd(new ChannelMapKey(typeof(ChannelMapKey), channelId), CreateChannelFor);
        }

        private IChannel CreateChannelFor(ChannelMapKey key)
        {
            return ChannelsUtility.GetChannelFactory(Session.ChannelFactoriesMap, key.Type).GetChannel(this, key.ChannelId);
        }
    }
}
