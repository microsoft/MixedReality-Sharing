using Microsoft.MixedReality.Sharing.Network.Channels;
using Microsoft.MixedReality.Sharing.Utilities;
using MorseCode.ITask;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Core
{
    public abstract class SessionBase<TSession, TEndpoint> : DisposableBase, ISession
        where TEndpoint : class, IEndpoint
        where TSession : SessionBase<TSession, TEndpoint>
    {
        protected readonly ILogger logger;

        protected readonly Dictionary<Type, IChannelFactory<IChannel>> channelFactoriesMap;

        private readonly ConcurrentDictionary<ChannelMapKey, ITask<IChannel>> openedChannels = new ConcurrentDictionary<ChannelMapKey, ITask<IChannel>>();

        protected Dictionary<TEndpoint, TSession> ConnectedEndpointsMap { get; } = new Dictionary<TEndpoint, TSession>();

        public SessionState State
        {
            get
            {
                if (IsDisposed)
                {
                    return SessionState.Disposed;
                }

                return OnGetState();
            }
        }

        internal IReadOnlyDictionary<Type, IChannelFactory<IChannel>> ChannelFactoriesMap { get; }

        public IEnumerable<IEndpoint> ConnectedEndpoints
        {
            get
            {
                ThrowIfDisposed();

                // TODO anborod make readonly
                return ConnectedEndpointsMap.Keys;
            }
        }

        public SessionBase(ILogger logger, IEnumerable<IChannelFactory<IChannel>> channelFactories)
            : this(new Dictionary<Type, IChannelFactory<IChannel>>())
        {
            this.logger = logger;

            ChannelsUtility.ProcessChannelFactories(channelFactoriesMap, channelFactories, logger);
        }

        protected SessionBase(Dictionary<Type, IChannelFactory<IChannel>> channelFactoriesMap)
        {
            this.channelFactoriesMap = channelFactoriesMap;
            ChannelFactoriesMap = new ReadOnlyDictionary<Type, IChannelFactory<IChannel>>(channelFactoriesMap);
        }

        public async Task<TChannel> GetChannelAsync<TChannel>(string sessionId, CancellationToken cancellationToken) where TChannel : IChannel
        {
            ThrowIfDisposed();

            IChannel channel = await openedChannels.GetOrAdd(new ChannelMapKey(typeof(TChannel), sessionId), key => CreateChannelFor(key, cancellationToken));
            return (TChannel)channel;
        }

        public async Task<bool> TryReconnectAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (State == SessionState.Joined)
            {
                return true;
            }

            return await OnTryReconnectAsync(cancellationToken);
        }

        protected abstract Task<bool> OnTryReconnectAsync(CancellationToken cancellationToken);

        protected abstract SessionState OnGetState();

        private async ITask<IChannel> CreateChannelFor(ChannelMapKey key, CancellationToken cancellationToken)
        {
            return await ChannelsUtility.GetChannelFactory(ChannelFactoriesMap, key.Type).OpenChannelAsync(this, key.ChannelId, cancellationToken);
        }
    }
}
