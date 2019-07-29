using Microsoft.MixedReality.Sharing.Network.Channels;
using MorseCode.ITask;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network.Test.Mocks
{
    public class MockEndpoint : IEndpoint<MockSession>
    {
        private readonly ConcurrentDictionary<Type, ITask<IChannel<MockSession, IMessage>>> openedChannels = new ConcurrentDictionary<Type, ITask<IChannel<MockSession, IMessage>>>();

        public MockSession Session { get; }

        public MockEndpoint(MockSession currentSession)
        {
            Session = currentSession;
        }

        public async Task<IChannel<MockSession, TMessageType>> GetChannelAsync<TMessageType>(CancellationToken cancellationToken) where TMessageType : IMessage
        {
            return (IChannel<MockSession, TMessageType>)await openedChannels.GetOrAdd(typeof(TMessageType), _ => (ITask<IChannel<MockSession, IMessage>>)ChannelsUtility.GetChannelFactory<MockSession, TMessageType>(Session.ChannelFactoriesMap).OpenChannelAsync(this, cancellationToken));
        }
    }
}
