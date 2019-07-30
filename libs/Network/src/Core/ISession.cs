// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing
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
    public interface ISession : IDisposable
    {
        SessionState State { get; }

        IEnumerable<IEndpoint> ConnectedEndpoints { get; }

        Task<TChannel> GetChannelAsync<TChannel>(string channelId, CancellationToken cancellationToken) where TChannel : IChannel;

        Task<bool> TryReconnectAsync(CancellationToken cancellationToken);
    }
}