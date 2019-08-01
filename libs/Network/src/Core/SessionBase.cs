// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.Network.Channels;
using Microsoft.MixedReality.Sharing.Utilities;
using Microsoft.MixedReality.Sharing.Utilities.Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Microsoft.MixedReality.Sharing.Core
{
    /// <summary>
    /// Helper base class implementing <see cref="ISession"/>. This class establishes a strongly-typed relationship with endpoints associated with this session.
    /// </summary>
    /// <typeparam name="TSession">The type of session.</typeparam>
    /// <typeparam name="TEndpoint">The type of endpoint.</typeparam>
    public abstract class SessionBase<TSession, TEndpoint> : DisposableBase, ISession
        where TEndpoint : class, IEndpoint
        where TSession : SessionBase<TSession, TEndpoint>
    {
        private readonly ConcurrentDictionary<ChannelMapKey, IChannel> openedChannels = new ConcurrentDictionary<ChannelMapKey, IChannel>();
        // There is no ConcurrentHashSet<T> using the keys here for that.
        private readonly ConcurrentDictionary<TEndpoint, object> connectedEndpoints = new ConcurrentDictionary<TEndpoint, object>();
        private readonly IReadOnlyCollection<IEndpoint> connectedEndpointsReadOnly;

        /// <summary>
        /// Logger.
        /// </summary>
        protected readonly ILogger logger;

        /// <summary>
        /// Channel factory map.
        /// </summary>
        protected readonly Dictionary<Type, IChannelFactory<IChannel>> channelFactoriesMap;

        /// <summary>
        /// Internal access to the factory map.
        /// </summary>
        internal IReadOnlyDictionary<Type, IChannelFactory<IChannel>> ChannelFactoriesMap { get; }

        /// <summary>
        /// A list of connceted endpoints.
        /// </summary>
        public IReadOnlyCollection<IEndpoint> ConnectedEndpoints
        {
            get
            {
                ThrowIfDisposed();

                // TODO anborod make readonly
                return connectedEndpointsReadOnly;
            }
        }

        protected SessionBase(ILogger logger, IEnumerable<IChannelFactory<IChannel>> channelFactories)
            : this(logger, new Dictionary<Type, IChannelFactory<IChannel>>())
        {
            ChannelsUtility.ProcessChannelFactories(channelFactoriesMap, channelFactories, logger);
        }

        protected SessionBase(ILogger logger, Dictionary<Type, IChannelFactory<IChannel>> channelFactoriesMap)
        {
            this.logger = logger;
            this.channelFactoriesMap = channelFactoriesMap;

            connectedEndpointsReadOnly = new ReadOnlyCollectionWrapper<IEndpoint>(() => connectedEndpoints.Count, () => connectedEndpoints.Keys.GetEnumerator()); // Keys are a snapshot at the time this will be called
            ChannelFactoriesMap = new ReadOnlyDictionary<Type, IChannelFactory<IChannel>>(channelFactoriesMap);
        }

        /// <summary>
        /// Gets a channel to communicate with every <see cref="IEndpoint"/> in the session.
        /// </summary>
        public TChannel GetChannel<TChannel>(string sessionId) where TChannel : IChannel
        {
            ThrowIfDisposed();

            return (TChannel)openedChannels.GetOrAdd(new ChannelMapKey(typeof(TChannel), sessionId), CreateChannelFor);
        }

        /// <summary>
        /// Call this method to add a new connected endpoint.
        /// </summary>
        /// <param name="endpoint">Endpoint that connected.</param>
        protected void OnEndpointConnected(TEndpoint endpoint)
        {
            ThrowIfDisposed();

            if (!connectedEndpoints.TryAdd(endpoint, null))
            {
                throw new InvalidOperationException($"Attempting to add an endpoint that is already listed as connected: {endpoint.ToString()}");
            }
        }

        /// <summary>
        /// Call this method to remove a disconnected endpoint.
        /// </summary>
        /// <param name="endpoint">Endpoint that disconnected.</param>
        protected void OnEndpointDisconnected(TEndpoint endpoint)
        {
            ThrowIfDisposed();

            if (!connectedEndpoints.TryRemove(endpoint, out object _))
            {
                throw new InvalidOperationException($"Attempting to add an endpoint that is not listed as connected: {endpoint.ToString()}");
            }
        }

        private IChannel CreateChannelFor(ChannelMapKey key)
        {
            return ChannelsUtility.GetChannelFactory(ChannelFactoriesMap, key.Type).GetChannel(this, key.ChannelId);
        }
    }
}
