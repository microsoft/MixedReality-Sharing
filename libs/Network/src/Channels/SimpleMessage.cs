using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    //public class ReliableMessage : IMessage
    //{
    //    private readonly Func<CancellationToken, Task<Stream>> openStreamCallback;

    //    public ReliableMessage(byte[] message)
    //        : this((c) => Task.FromResult<Stream>(new MemoryStream(message)))
    //    { }

    //    public ReliableMessage(Func<CancellationToken, Task<Stream>> openStreamCallback)
    //    {
    //        this.openStreamCallback = openStreamCallback;
    //    }

    //    public async Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
    //    {
    //        return await openStreamCallback(cancellationToken);
    //    }
    //}

    //public class UnreliableMessage : IMessage
    //{
    //    private readonly Func<CancellationToken, Task<Stream>> openStreamCallback;

    //    public UnreliableMessage(byte[] message)
    //        : this((c) => Task.FromResult<Stream>(new MemoryStream(message)))
    //    { }

    //    public UnreliableMessage(Func<CancellationToken, Task<Stream>> openStreamCallback)
    //    {
    //        this.openStreamCallback = openStreamCallback;
    //    }

    //    public async Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
    //    {
    //        return await openStreamCallback(cancellationToken);
    //    }
    //}

    //public class ReliableOrderedMessage : IMessage
    //{
    //    private readonly Func<CancellationToken, Task<Stream>> openStreamCallback;

    //    public ReliableOrderedMessage(byte[] message)
    //        : this((c) => Task.FromResult<Stream>(new MemoryStream(message)))
    //    { }

    //    public ReliableOrderedMessage(Func<CancellationToken, Task<Stream>> openStreamCallback)
    //    {
    //        this.openStreamCallback = openStreamCallback;
    //    }

    //    public async Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
    //    {
    //        return await openStreamCallback(cancellationToken);
    //    }
    //}

    //public class UnreliableOrderedMessage : IMessage
    //{
    //    private readonly Func<CancellationToken, Task<Stream>> openStreamCallback;

    //    public UnreliableOrderedMessage(byte[] message)
    //        : this((c) => Task.FromResult<Stream>(new MemoryStream(message)))
    //    { }

    //    public UnreliableOrderedMessage(Func<CancellationToken, Task<Stream>> openStreamCallback)
    //    {
    //        this.openStreamCallback = openStreamCallback;
    //    }

    //    public async Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
    //    {
    //        return await openStreamCallback(cancellationToken);
    //    }
    //}
}
