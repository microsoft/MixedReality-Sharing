using Microsoft.MixedReality.Sharing.Utilities;
using MorseCode.ITask;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    //public enum SimpleChannelType
    //{
    //    Reliable,
    //    Unreliable,
    //    ReliableOrdered,
    //    UnreliableOrdered
    //}

    //public abstract class SimpleMessageChannelBase<TSessionType> : DisposableBase, IChannel<TSessionType, ReliableMessage>, IChannel<TSessionType, UnreliableMessage>, IChannel<TSessionType, ReliableOrderedMessage>, IChannel<TSessionType, UnreliableOrderedMessage>
    //    where TSessionType : class, ISession<TSessionType>
    //{
    //    private event Action<IEndpoint<TSessionType>, ReliableMessage> ReliableMessageReceived;
    //    private event Action<IEndpoint<TSessionType>, UnreliableMessage> UnreliableMessageReceived;
    //    private event Action<IEndpoint<TSessionType>, ReliableOrderedMessage> ReliableOrderedMessageReceived;
    //    private event Action<IEndpoint<TSessionType>, UnreliableOrderedMessage> UnreliableOrderedMessageReceived;

    //    event Action<IEndpoint<TSessionType>, ReliableMessage> IChannel<TSessionType, ReliableMessage>.MessageReceived
    //    {
    //        add { ReliableMessageReceived += value; }
    //        remove { ReliableMessageReceived -= value; }
    //    }

    //    event Action<IEndpoint<TSessionType>, UnreliableMessage> IChannel<TSessionType, UnreliableMessage>.MessageReceived
    //    {
    //        add { UnreliableMessageReceived += value; }
    //        remove { UnreliableMessageReceived -= value; }
    //    }

    //    event Action<IEndpoint<TSessionType>, ReliableOrderedMessage> IChannel<TSessionType, ReliableOrderedMessage>.MessageReceived
    //    {
    //        add { ReliableOrderedMessageReceived += value; }
    //        remove { ReliableOrderedMessageReceived -= value; }
    //    }

    //    event Action<IEndpoint<TSessionType>, UnreliableOrderedMessage> IChannel<TSessionType, UnreliableOrderedMessage>.MessageReceived
    //    {
    //        add { UnreliableOrderedMessageReceived += value; }
    //        remove { UnreliableOrderedMessageReceived -= value; }
    //    }

    //    protected readonly SimpleChannelType simpleChannelType;
    //    private ChannelStatus status;

    //    public ChannelStatus Status
    //    {
    //        get => status;
    //        protected set
    //        {
    //            ThrowIfDisposed();

    //            lock (LockObject)
    //            {
    //                status = value;
    //            }
    //        }
    //    }

    //    protected SimpleMessageChannelBase(SimpleChannelType simpleChannelType)
    //    {
    //        this.simpleChannelType = simpleChannelType;
    //    }

    //    public async Task SendMessageAsync(IMessage message, CancellationToken cancellationToken)
    //    {
    //        ThrowIfDisposed();

    //        using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisposeCancellationToken))
    //        {
    //            await OnSendMessageAsync(message, cts.Token);
    //        }
    //    }

    //    public async Task<bool> TryReconnectAsync(CancellationToken cancellationToken)
    //    {
    //        ThrowIfDisposed();

    //        using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisposeCancellationToken))
    //        {
    //            return await OnTryReconnectAsync(cts.Token);
    //        }
    //    }

    //    protected abstract Task OnSendMessageAsync(IMessage message, CancellationToken cancellationToken);

    //    protected abstract Task<bool> OnTryReconnectAsync(CancellationToken cancellationToken);

    //    protected void OnMessageReceived(IEndpoint<TSessionType> endpoint, byte[] bytes)
    //    {
    //        OnMessageReceived(endpoint, _ => Task.FromResult<Stream>(new MemoryStream(bytes)));
    //    }

    //    protected void OnMessageReceived(IEndpoint<TSessionType> from, Func<CancellationToken, Task<Stream>> openStreamCallback)
    //    {
    //        switch (simpleChannelType)
    //        {
    //            default:
    //            case SimpleChannelType.Reliable:
    //                ReliableMessageReceived?.Invoke(from, new ReliableMessage(openStreamCallback));
    //                break;
    //            case SimpleChannelType.Unreliable:
    //                UnreliableMessageReceived?.Invoke(from, new UnreliableMessage(openStreamCallback));
    //                break;
    //            case SimpleChannelType.ReliableOrdered:
    //                ReliableOrderedMessageReceived?.Invoke(from, new ReliableOrderedMessage(openStreamCallback));
    //                break;
    //            case SimpleChannelType.UnreliableOrdered:
    //                UnreliableOrderedMessageReceived?.Invoke(from, new UnreliableOrderedMessage(openStreamCallback));
    //                break;
    //        }
    //    }
    //}

    //public abstract class SimpleMessageChannelFactoryBase<TSessionType> : IChannelFactory<TSessionType, ReliableMessage>, IChannelFactory<TSessionType, UnreliableMessage>, IChannelFactory<TSessionType, ReliableOrderedMessage>, IChannelFactory<TSessionType, UnreliableOrderedMessage>
    //    where TSessionType : class, ISession<TSessionType>
    //{
    //    public string Name { get; } = "Factory for Reliable, Unreliable, ReliableOrdered and UnreliableOrdered messages";

    //    async ITask<IChannel<TSessionType, ReliableMessage>> IChannelFactory<TSessionType, ReliableMessage>.OpenChannelAsync(TSessionType session, CancellationToken cancellationToken)
    //    {
    //        return await CreateSimpleChannelAsync(SimpleChannelType.Reliable, session, cancellationToken);
    //    }

    //    async ITask<IChannel<TSessionType, ReliableMessage>> IChannelFactory<TSessionType, ReliableMessage>.OpenChannelAsync(IEndpoint<TSessionType> endpoint, CancellationToken cancellationToken)
    //    {
    //        return await CreateSimpleChannelAsync(SimpleChannelType.Reliable, endpoint, cancellationToken);
    //    }

    //    async ITask<IChannel<TSessionType, UnreliableMessage>> IChannelFactory<TSessionType, UnreliableMessage>.OpenChannelAsync(TSessionType session, CancellationToken cancellationToken)
    //    {
    //        return await CreateSimpleChannelAsync(SimpleChannelType.Unreliable, session, cancellationToken);
    //    }

    //    async ITask<IChannel<TSessionType, UnreliableMessage>> IChannelFactory<TSessionType, UnreliableMessage>.OpenChannelAsync(IEndpoint<TSessionType> endpoint, CancellationToken cancellationToken)
    //    {
    //        return await CreateSimpleChannelAsync(SimpleChannelType.Unreliable, endpoint, cancellationToken);
    //    }

    //    async ITask<IChannel<TSessionType, ReliableOrderedMessage>> IChannelFactory<TSessionType, ReliableOrderedMessage>.OpenChannelAsync(TSessionType session, CancellationToken cancellationToken)
    //    {
    //        return await CreateSimpleChannelAsync(SimpleChannelType.ReliableOrdered, session, cancellationToken);
    //    }

    //    async ITask<IChannel<TSessionType, ReliableOrderedMessage>> IChannelFactory<TSessionType, ReliableOrderedMessage>.OpenChannelAsync(IEndpoint<TSessionType> endpoint, CancellationToken cancellationToken)
    //    {
    //        return await CreateSimpleChannelAsync(SimpleChannelType.ReliableOrdered, endpoint, cancellationToken);
    //    }

    //    async ITask<IChannel<TSessionType, UnreliableOrderedMessage>> IChannelFactory<TSessionType, UnreliableOrderedMessage>.OpenChannelAsync(TSessionType session, CancellationToken cancellationToken)
    //    {
    //        return await CreateSimpleChannelAsync(SimpleChannelType.UnreliableOrdered, session, cancellationToken);
    //    }

    //    async ITask<IChannel<TSessionType, UnreliableOrderedMessage>> IChannelFactory<TSessionType, UnreliableOrderedMessage>.OpenChannelAsync(IEndpoint<TSessionType> endpoint, CancellationToken cancellationToken)
    //    {
    //        return await CreateSimpleChannelAsync(SimpleChannelType.UnreliableOrdered, endpoint, cancellationToken);
    //    }

    //    protected abstract Task<SimpleMessageChannelBase<TSessionType>> CreateSimpleChannelAsync(SimpleChannelType type, TSessionType session, CancellationToken cancellationToken);

    //    protected abstract Task<SimpleMessageChannelBase<TSessionType>> CreateSimpleChannelAsync(SimpleChannelType type, IEndpoint<TSessionType> endpoint, CancellationToken cancellationToken);
    //}
}
