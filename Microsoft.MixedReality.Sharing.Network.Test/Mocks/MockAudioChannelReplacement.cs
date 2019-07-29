using Microsoft.MixedReality.Sharing.Utilities;
using MorseCode.ITask;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network.Test.Mocks
{
    public class MockAudioChannelReplacement : DisposableBase, IChannel<MockSession, AudioMessage>
    {
        public ChannelStatus Status => ChannelStatus.Connected;

        public event Action<IEndpoint<MockSession>, AudioMessage> MessageReceived;

        public Task SendMessageAsync(IMessage message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<bool> TryReconnectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }

    public class MockAudioChannelReplacementFactory : IChannelFactory<MockSession, AudioMessage>
    {
        public string Name => "Mock Audio Replacement";

        public async ITask<IChannel<MockSession, AudioMessage>> OpenChannelAsync(MockSession session, CancellationToken cancellationToken)
        {
            return new MockAudioChannelReplacement();
        }

        public async ITask<IChannel<MockSession, AudioMessage>> OpenChannelAsync(IEndpoint<MockSession> endpoint, CancellationToken cancellationToken)
        {
            return new MockAudioChannelReplacement();
        }
    }
}
