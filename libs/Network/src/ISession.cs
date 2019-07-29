// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.Network;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    public enum SessionState
    {
        Joined,
        Available,
        Disposed
    }
    /// <summary>
    /// Sharing session that this process is a part of.
    /// A sharing session is a collection of participants who can interact with each other and edit a shared state.
    /// </summary>
    public interface ISession<TSessionType> : IDisposable
        where TSessionType : class, ISession<TSessionType>
    {
        SessionState State { get; }

        IEnumerable<IEndpoint<TSessionType>> ConnectedEndpoints { get; }

        Task<IChannel<TSessionType, TMessageType>> GetChannelAsync<TMessageType>(CancellationToken cancellationToken) where TMessageType : IMessage;

        Task<bool> TryReconnectAsync(CancellationToken cancellationToken);
    }
}