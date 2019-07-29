// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    public enum ChannelStatus
    {
        Connected,
        Disconnected,
        Disposed
    }

    public interface IChannel<TSessionType, out TMessageType> : IDisposable 
        where TSessionType : class, ISession<TSessionType>
        where TMessageType : IMessage
    {
        event Action<IEndpoint<TSessionType>, TMessageType> MessageReceived;

        ChannelStatus Status { get; }

        Task SendMessageAsync(IMessage message, CancellationToken cancellationToken);

        /// <summary>
        /// Try to re-establish the channel. If <see cref="IsOk"/> returns false, this might restore the channel
        /// status and make it available for sending/receiving again.
        /// </summary>
        Task<bool> TryReconnectAsync(CancellationToken cancellationToken);
    }
}
