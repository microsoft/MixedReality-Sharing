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
        /// There should not be two different rooms with the same ID open at the same time.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// The participant that created this room.
        /// </summary>
        IMatchParticipant Owner { get; }

        RoomProperties Properties { get; }
        Task SetPropertiesAsync(RoomProperties properties);

        /// <summary>
        /// Try to join the room. Gets the current session if already joined.
        /// Some implementation might only allow joining one room (or a limited number) at a time.
        /// </summary>
        Task<ISession> TryJoinAsync(CancellationToken token);
    }

    /// <summary>
    /// Custom properties of a room.
    /// </summary>
    public class RoomProperties : Dictionary<string, object> {}
}
