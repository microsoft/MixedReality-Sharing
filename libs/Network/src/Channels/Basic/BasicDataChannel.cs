using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Channels
{
    public abstract class ReliableChannel : BasicDataChannel
    {
        protected ReliableChannel(string id)
            : base(id)
        { }
    }

    public abstract class UnreliableChannel : BasicDataChannel
    {
        protected UnreliableChannel(string id)
            : base(id)
        { }
    }

    public abstract class ReliableOrderedChannel : BasicDataChannel
    {
        protected ReliableOrderedChannel(string id)
            : base(id)
        { }
    }

    public abstract class UnreliableOrderedChannel : BasicDataChannel
    {
        protected UnreliableOrderedChannel(string id)
            : base(id)
        { }
    }

    public abstract class BasicDataChannel : DisposableBase, IChannel
    {
        public event Action<IEndpoint, byte[]> MessageReceived;

        public string Id { get; }

        public virtual int RecommendMessageSize { get; } = 512;

        protected internal BasicDataChannel(string id)
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
