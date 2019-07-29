using Microsoft.MixedReality.Sharing.Utilities;
using MorseCode.ITask;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    public class AudioMessage : IMessage
    {
        private readonly Func<CancellationToken, Task<Stream>> openStreamCallback;

        public AudioMessage(byte[] message)
            : this((c) => Task.FromResult<Stream>(new MemoryStream(message)))
        { }

        public AudioMessage(Func<CancellationToken, Task<Stream>> openStreamCallback)
        {
            this.openStreamCallback = openStreamCallback;
        }

        public async Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
        {
            return await openStreamCallback(cancellationToken);
        }
    }

    internal abstract class AudioChannelBase<TSessionType> : DisposableBase, IChannel<TSessionType, AudioMessage>
        where TSessionType : class, ISession<TSessionType>
    {
        public ChannelStatus Status => throw new NotImplementedException();

        public event Action<IEndpoint<TSessionType>, AudioMessage> MessageReceived;

        public abstract Task SendMessageAsync(IMessage message, CancellationToken cancellationToken);

        public Task<bool> TryReconnectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

    }

    internal class ReliableAudioChannel<TSessionType> : DisposableBase, IChannel<TSessionType, AudioMessage>
        where TSessionType : class, ISession<TSessionType>
    {
        public event Action<IEndpoint<TSessionType>, AudioMessage> MessageReceived;

        private readonly IChannel<TSessionType, ReliableOrderedMessage> channel;

        public ChannelStatus Status => channel.Status;

        public ReliableAudioChannel(IChannel<TSessionType, ReliableOrderedMessage> channel)
        {
            this.channel = channel;
            this.channel.MessageReceived += OnMessageReceived;
        }

        public async Task SendMessageAsync(IMessage message, CancellationToken cancellationToken)
        {
            await channel.SendMessageAsync(new ReliableMessage(message.OpenReadAsync), cancellationToken);
        }

        public Task<bool> TryReconnectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        private void OnMessageReceived(IEndpoint<TSessionType> endpoint, ReliableOrderedMessage message)
        {
            MessageReceived?.Invoke(endpoint, new AudioMessage(message.OpenReadAsync));
        }
    }

    internal class UnreliableAudioChannel<TSessionType> : DisposableBase, IChannel<TSessionType, AudioMessage>
        where TSessionType : class, ISession<TSessionType>
    {
        public event Action<IEndpoint<TSessionType>, AudioMessage> MessageReceived;

        private readonly IChannel<TSessionType, UnreliableOrderedMessage> channel;

        public ChannelStatus Status => channel.Status;

        public UnreliableAudioChannel(IChannel<TSessionType, UnreliableOrderedMessage> channel)
        {
            this.channel = channel;
            this.channel.MessageReceived += OnMessageReceived;
        }

        public async Task SendMessageAsync(IMessage message, CancellationToken cancellationToken)
        {
            await channel.SendMessageAsync(new ReliableMessage(message.OpenReadAsync), cancellationToken);
        }

        public Task<bool> TryReconnectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        private void OnMessageReceived(IEndpoint<TSessionType> endpoint, UnreliableOrderedMessage message)
        {
            MessageReceived?.Invoke(endpoint, new AudioMessage(message.OpenReadAsync));
        }
    }

    public class AudioChannelFactory<TSessionType> : IChannelFactory<TSessionType, AudioMessage>
        where TSessionType : class, ISession<TSessionType>
    {
        private readonly bool reliableAndOrdered;
        private readonly IChannelFactory<TSessionType, UnreliableOrderedMessage> unreliableOrderedChannelFactory;
        private readonly IChannelFactory<TSessionType, ReliableOrderedMessage> reliableOrderedChannelFactory;

        public string Name => "Audio Channels Factory";

        public AudioChannelFactory(bool reliableAndOrdered, IChannelFactory<TSessionType, ReliableOrderedMessage> reliableOrderedChannelFactory, IChannelFactory<TSessionType, UnreliableOrderedMessage> unreliableOrderedChannelFactory) // otheriwse just ordered
        {
            this.reliableAndOrdered = reliableAndOrdered;

            this.unreliableOrderedChannelFactory = unreliableOrderedChannelFactory;
            this.reliableOrderedChannelFactory = reliableOrderedChannelFactory;
        }

        public async ITask<IChannel<TSessionType, AudioMessage>> OpenChannelAsync(TSessionType session, CancellationToken cancellationToken)
        {
            if (reliableAndOrdered)
            {
                IChannel<TSessionType, ReliableOrderedMessage> channel = await reliableOrderedChannelFactory.OpenChannelAsync(session, cancellationToken);
                return new ReliableAudioChannel<TSessionType>(channel);
            }
            else
            {
                IChannel<TSessionType, UnreliableOrderedMessage> channel = await unreliableOrderedChannelFactory.OpenChannelAsync(session, cancellationToken);
                return new UnreliableAudioChannel<TSessionType>(channel);
            }
        }

        public async ITask<IChannel<TSessionType, AudioMessage>> OpenChannelAsync(IEndpoint<TSessionType> endpoint, CancellationToken cancellationToken)
        {
            if (reliableAndOrdered)
            {
                IChannel<TSessionType, ReliableOrderedMessage> channel = await reliableOrderedChannelFactory.OpenChannelAsync(endpoint, cancellationToken);
                return new ReliableAudioChannel<TSessionType>(channel);
            }
            else
            {
                IChannel<TSessionType, UnreliableOrderedMessage> channel = await unreliableOrderedChannelFactory.OpenChannelAsync(endpoint, cancellationToken);
                return new UnreliableAudioChannel<TSessionType>(channel);
            }
        }
    }
}
