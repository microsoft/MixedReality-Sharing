// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.StateSync;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing
{
    /// <summary>
    /// Sharing session that this process is a part of.
    /// A sharing session is a collection of participants who can interact with each other and edit a shared state.
    /// </summary>
    public interface ISession : IDisposable
    {
        IReadOnlyCollection<IEndpoint> ConnectedEndpoints { get; }

        // Coming in a later PR
        //SynchronizationStore SynchronizationStore { get; }

        Task<TChannel> GetChannelAsync<TChannel>(string channelId, CancellationToken cancellationToken) where TChannel : IChannel;
    }
}