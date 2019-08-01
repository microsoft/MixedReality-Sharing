// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Sharing
{
    /// <summary>
    /// Sharing session that this process is a part of.
    /// A sharing session is a collection of participants who can interact with each other and edit a shared state.
    /// </summary>
    public interface ISession : IDisposable
    {
        /// <summary>
        /// A list of connceted endpoints.
        /// </summary>
        IReadOnlyCollection<IEndpoint> ConnectedEndpoints { get; }

        // Coming in a later PR
        //SynchronizationStore SynchronizationStore { get; }

        /// <summary>
        /// Gets a channel to communicate with every <see cref="IEndpoint"/> in the session.
        /// </summary>
        TChannel GetChannel<TChannel>(string channelId) where TChannel : IChannel;
    }
}