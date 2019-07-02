// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Session
{
    /// <summary>
    /// Sharing session that this process is a part of.
    /// A sharing session is a collection of participants who can interact with each other and edit a shared state.
    /// </summary>
    public interface ISession : IDisposable
    {
        /// <summary>
        /// Participant that currently acts as owner of this session.
        /// It usually is the participant who created the session.
        /// Depending on the implementation, it might change if the original owner is disconnected, or for other reasons.
        /// </summary>
        ISessionParticipant Owner { get; }

        /// <summary>
        /// List of participants that the local process can currently try to create a channel to.
        /// </summary>
        IEnumerable<ISessionParticipant> Participants { get; }

        /// <summary>
        /// Create a channel to this session. Data transmitted on this channel will be received by every other
        /// participant that has opened a channel to the same session.
        /// </summary>
        Task<Network.IChannel> CreateChannelAsync(string key, CancellationToken cancellationToken = default);

        // TODO session state? other?
    }

    public interface ISessionFactory
    {
        /// <summary>
        /// Join a session over the passed channel.
        /// May listen/send message on the channel to gather data on the existing session (if any) and notify the
        /// existing participants that a new one has joined.
        /// Depending on the implementation, the new session may or may not keep using <paramref name="baseChannel"/>
        /// after creation.
        /// </summary>
        Task<ISession> JoinSessionAsync(Network.IChannel baseChannel);
    }
}
