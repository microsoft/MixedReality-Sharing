﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Channels
{
    /// <summary>
    /// A handler for messages received from the <see cref="BasicDataChannel"/>.
    /// </summary>
    /// <param name="endpoint">The source from which the message came.</param>
    /// <param name="message">The message data.</param>
    public delegate void BasicMessageReceived(IEndpoint endpoint, ReadOnlySpan<byte> message);

    /// <summary>
    /// A simple message based data channel.
    /// </summary>
    public abstract class BasicDataChannel : ChannelBase
    {
        /// <summary>
        /// Occurs when a new message is available on this channel.
        /// </summary>
        public event BasicMessageReceived MessageReceived;

        /// <summary>
        /// The size of underlying packets that the message will be split into.
        /// </summary>
        public virtual int PacketSize { get; } = 512;

        /// <summary>
        /// Creates a new isntance of the <see cref="BasicDataChannel"/>.
        /// </summary>
        /// <param name="id">The identifier for this channe.</param>
        protected BasicDataChannel(string id) : base(id) { }

        /// <summary>
        /// Asynchronously send a <see cref="byte[]"/> message.
        /// </summary>
        /// <param name="data">The bytes to send.</param>
        /// <param name="cancellationToken">The cancellationtoken to interrupt the process early.</param>
        public Task SendMessageAsync(byte[] data, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            return OnSendMessageAsync(data, cancellationToken);
        }

        /// <summary>
        /// Asynchronously send a message by reading from a given stream.
        /// </summary>
        /// <param name="stream">The stream to read data from.</param>
        /// <param name="cancellationToken">The cancellationtoken to interrupt the process early.</param>
        public Task SendMessageAsync(Stream stream, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            return OnSendMessageAsync(stream, cancellationToken);
        }

        /// <summary>
        /// Optionally override in the inheriting class to change how a <see cref="byte[]"/> message is sent.
        /// </summary>
        protected virtual async Task OnSendMessageAsync(byte[] data, CancellationToken cancellationToken)
        {
            using (MemoryStream stream = new MemoryStream(data))
            {
                await SendMessageAsync(stream, cancellationToken);
            }
        }

        /// <summary>
        /// Implement in the inheriting class to send a message by reading from a stream.
        /// </summary>
        protected abstract Task OnSendMessageAsync(Stream stream, CancellationToken cancellationToken);

        /// <summary>
        /// Raise the message received event.
        /// </summary>
        /// <param name="source">The <see cref="IEndpoint"/> representing the source of the message.</param>
        /// <param name="message">The span containing the message bytes.</param>
        protected void RaiseMessageReceived(IEndpoint source, ReadOnlySpan<byte> message)
        {
            MessageReceived?.Invoke(source, message);
        }
    }
}
