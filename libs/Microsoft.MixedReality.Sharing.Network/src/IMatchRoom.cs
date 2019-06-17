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
    /// Container for contacts intending to interact with each other and for the state shared between them.
    /// Created/managed through an <see cref="IMatchmakingService"/> or <see cref="IMatchRoomManager"/>.
    /// A room can host an <see cref="ISession"/>. A contact joining/leaving the room will also join/leave
    /// the corresponding session.
    /// The session is initiated when a contact first creates/joins the room. The lifetime of the room and
    /// its sessions are implementation-dependent.
    /// </summary>
    public interface IMatchRoom
    {
        /// <summary>
        /// Identifies this room.
        /// The matchmaking implementation must guarantee that this is unique for every new room.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Current owner of this room. The owner is initially the participant who created the room.
        /// The implementation can choose a new owner if e.g. the current owner is disconnected.
        /// </summary>
        IMatchParticipant Owner { get; }

        /// <summary>
        /// Read-only room properties.
        /// </summary>
        /// <seealso cref="SetPropertiesAsync(Dictionary{string, object})"/>
        Dictionary<string, object> Properties { get; }

        /// <summary>
        /// Set some property values on the room.
        /// The method will set the keys contained in the passed dictionary to the passed values.
        /// If the room properties do not contain some of the keys, those will be added.
        /// The types supported for the property values are defined by each implementation.
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        Task SetPropertiesAsync(Dictionary<string, object> properties);

        /// <summary>
        /// Try to join the room. Gets the current session if already joined.
        /// Some implementation might only allow joining one room (or a limited number) at a time.
        /// </summary>
        Task<ISession> TryJoinAsync(CancellationToken token);
    }
}
