﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.Network;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    /// <summary>
    /// Handle to a joined matchmaking room.
    ///
    /// A room is a container for an <see cref="ISession"/>, that can be created, advertised and/or joined through a
    /// matchmaking service. A participant joining/leaving a room will also join/leave the corresponding session.
    ///
    /// A process can use this interface to interact with a joined room and to access the corresponding
    /// <see cref="ISession"/>. Instances of this interface are obtained when joining/creating a matchmaking room
    /// through an <see cref="IMatchmakingService"/> or <see cref="IRoomManager"/>. See <see cref="IRoomInfo"/> for the
    /// interface that wraps non-joined rooms.
    ///
    /// The lifetime of a room and the corresponding session is implementation-dependent.
    /// </summary>
    public interface IRoom : IRoomInfo
    {
        /// <summary>
        /// Current owner of this room. The owner is initially the participant who created the room.
        /// The implementation can choose a new owner if e.g. the current owner is disconnected.
        /// </summary>
        IParticipant Owner { get; }

        /// <summary>
        /// Participants currently in the room.
        /// </summary>
        IEnumerable<IParticipant> Participants { get; }

        /// <summary>
        /// Set some property values on the room.
        /// The method will set the keys contained in the passed dictionary to the passed values.
        /// If the room attributes do not contain some of the keys, those will be added.
        /// The types supported for the property values are defined by each implementation.
        /// </summary>
        /// <param name="attributes"></param>
        Task SetAttributesAsync(Dictionary<string, object> attributes);

        /// <summary>
        /// Leave this room.
        /// Calling this method invalidates this `IRoom` instance - no methods should be called after this.
        /// </summary>
        Task LeaveAsync();

        /// <summary>
        /// Session corresponding to this room.
        /// </summary>
        ISession Session { get; }
    }
}
