﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    public interface INetParticipant
    {
        /// <summary>
        /// Identifies the participant. Must be unique within a session.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Opens a channel to communicate to the other participant.
        /// </summary>
        Task<IChannel> TryCreateChannelAsync(string key, CancellationToken cancellationToken);
    }
}
