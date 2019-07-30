using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Channels
{
    public abstract class ReliableChannel : BasicChannelBase
    {
        protected ReliableChannel(string id)
            : base(id)
        { }
    }

    public abstract class UnreliableChannel : BasicChannelBase
    {
        protected UnreliableChannel(string id)
            : base(id)
        { }
    }

    public abstract class ReliableOrderedChannel : BasicChannelBase
    {
        protected ReliableOrderedChannel(string id)
            : base(id)
        { }
    }

    public abstract class UnreliableOrderedChannel : BasicChannelBase
    {
        protected UnreliableOrderedChannel(string id)
            : base(id)
        { }
    }

    public abstract class BasicChannelBase : DisposableBase, IChannel
    {
        public event Action<IEndpoint, byte[]> MessageReceived;

        public string Id { get; }

        public abstract ChannelStatus Status { get; }

        public virtual int RecommendMessageSize { get; } = 512;

        protected internal BasicChannelBase(string id)
        {
            Id = id;
        }

        public virtual async Task SendMessageAsync(byte[] data, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            using (MemoryStream stream = new MemoryStream(data))
            {
                await SendMessageAsync(stream, cancellationToken);
            }
        }

        public abstract Task SendMessageAsync(Stream stream, CancellationToken cancellationToken);

        protected void RaiseMessageReceived(IEndpoint endpoint, byte[] message)
        {
            MessageReceived?.Invoke(endpoint, message);
        }
    }
}
