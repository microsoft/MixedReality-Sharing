// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    /// <summary>
    /// Sharing session that this process is a part of.
    /// A sharing session is a collection of participants who can interact with each other and edit a shared state.
    /// </summary>
    public interface ISession
    {
        /// <summary>
        /// Participant that currently acts as owner of this session.
        /// It usually is the participant who created the session.
        /// Depending on the implementation, it might change if the original owner is disconnected, or for other reasons.
        /// </summary>
        INetParticipant Owner { get; }

        /// <summary>
        /// List of participants that the local process can currently try to create a channel to.
        /// </summary>
        IEnumerable<INetParticipant> Participants { get; }

        /// <summary>
        /// Raised when a participant joins this session.
        /// </summary>
        event EventHandler<INetParticipant> ParticipantJoined;

        /// <summary>
        /// Raised when a participant leaves this session.
        /// </summary>
        event EventHandler<INetParticipant> ParticipantLeft;

        /// <summary>
        /// Create a channel to this session. Data transmitted on this channel will be received by every other
        /// participant that has opened a channel to the same session.
        /// </summary>
        Task<IChannel> TryCreateChannelAsync(string key, CancellationToken cancellationToken = default);
    }
}
