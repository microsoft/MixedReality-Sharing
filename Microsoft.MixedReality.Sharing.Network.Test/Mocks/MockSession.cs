using Microsoft.MixedReality.Sharing.Network.Channels;
using Microsoft.MixedReality.Sharing.Utilities;
using MorseCode.ITask;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network.Test.Mocks
{
    public class MockSession : DisposableBase, ISession<MockSession>
    {
        private readonly ILogger logger;
        // Simple mapping of the endpoint and the other mock session that was created to represent it
        private readonly ReadOnlyCollection<MockEndpoint> readonlyConnectedEndpoints;
        private readonly Dictionary<Type, IChannelFactory<MockSession, IMessage>> channelFactoriesMap;

        private readonly ConcurrentDictionary<Type, ITask<IChannel<MockSession, IMessage>>> openedChannels = new ConcurrentDictionary<Type, ITask<IChannel<MockSession, IMessage>>>();

        internal Dictionary<MockEndpoint, MockSession> ConnectedEndpointsMap { get; } = new Dictionary<MockEndpoint, MockSession>();

        public SessionState State { get; private set; }

        internal IReadOnlyDictionary<Type, IChannelFactory<MockSession, IMessage>> ChannelFactoriesMap { get; }

        public IEnumerable<IEndpoint<MockSession>> ConnectedEndpoints
        {
            get
            {
                ThrowIfDisposed();

                return readonlyConnectedEndpoints;
            }
        }

        public MockSession(ILogger logger, IEnumerable<IChannelFactory<MockSession, IMessage>> channelFactories)
        {
            channelFactoriesMap = new Dictionary<Type, IChannelFactory<MockSession, IMessage>>();
            ChannelFactoriesMap = new ReadOnlyDictionary<Type, IChannelFactory<MockSession, IMessage>>(channelFactoriesMap);
            readonlyConnectedEndpoints = new ReadOnlyCollection<MockEndpoint>(ConnectedEndpointsMap.Keys.ToList());

            State = SessionState.Joined;

            ChannelsUtility.ProcessChannelFactories(channelFactoriesMap, channelFactories, logger);
        }

        private MockSession(MockSession otherSession)
        {
            channelFactoriesMap = otherSession.channelFactoriesMap;
            ChannelFactoriesMap = new ReadOnlyDictionary<Type, IChannelFactory<MockSession, IMessage>>(channelFactoriesMap);
            readonlyConnectedEndpoints = new ReadOnlyCollection<MockEndpoint>(ConnectedEndpointsMap.Keys.ToList());

            State = SessionState.Joined;
        }

        public async Task<IChannel<MockSession, TMessageType>> GetChannelAsync<TMessageType>(CancellationToken cancellationToken) where TMessageType : IMessage
        {
            ThrowIfDisposed();

            return (IChannel<MockSession, TMessageType>)await openedChannels.GetOrAdd(typeof(TMessageType), _ => (ITask<IChannel<MockSession, IMessage>>)ChannelsUtility.GetChannelFactory<MockSession, TMessageType>(ChannelFactoriesMap).OpenChannelAsync(this, cancellationToken));
        }

        protected override void OnManagedDispose()
        {
            base.OnManagedDispose();

            State = SessionState.Disposed;
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

        public async Task<bool> TryReconnectAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (State == SessionState.Joined)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));

            State = SessionState.Joined;
            return true;
        }
    }
}
