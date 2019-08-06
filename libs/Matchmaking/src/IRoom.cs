// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    public enum RoomVisibility
    {
        /// <summary>
        /// The room is not visible through search methods. It can only be joined if its ID is known.
        /// </summary>
        NotVisible,

        /// <summary>
        /// The room can only be found by searching for a known participant (if the participant has joined the room or
        /// is its owner).
        /// </summary>
        ByParticipantOnly,

        /// <summary>
        /// The room is publicly available for searching by participants or by attributes.
        /// </summary>
        Searchable,
    }

    /// <summary>
    /// Handle to a joined matchmaking room.
    ///
    /// A room is a container for a Mixed Reality shared session, that can be created, advertised and/or joined through a
    /// matchmaking service. A participant joining/leaving a room will also join/leave the corresponding session.
    ///
    /// A process can use this interface to interact with a joined room and to access the corresponding session state.
    /// Instances of this interface are obtained when joining/creating a matchmaking room
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
        IRoomParticipant Owner { get; }

        /// <summary>
        /// Participants currently in the room.
        /// </summary>
        IEnumerable<IRoomParticipant> Participants { get; }

        /// <summary>
        /// Set some property values on the room.
        /// The method will set the keys contained in the passed dictionary to the passed values.
        /// If the room attributes do not contain some of the keys, those will be added.
        /// The types supported for the property values are defined by each implementation.
        /// </summary>
        /// <param name="attributes"></param>
        Task SetAttributesAsync(Dictionary<string, object> attributes);

        /// <summary>
        /// Triggered when the room attributes are changed, by the local participant or another member of the room.
        /// </summary>
        event EventHandler AttributesChanged;

        /// <summary>
        /// Makes the room visible or not according to the passed value.
        /// </summary>
        Task SetVisibility(RoomVisibility val);

        /// <summary>
        /// Leave this room.
        /// Calling this method invalidates this `IRoom` instance - no methods should be called after this.
        /// </summary>
        Task LeaveAsync();

        /// <summary>
        /// Subscription to the room state.
        /// Can be used to join a <see cref="Session.ISession"/> (see <see cref="Session.ISessionFactory"/>), or
        /// listened to/edited directly.
        /// </summary>
        //StateSync.IStateSubscription State { get; }
    }

    /// Participant in a room.
    public interface IRoomParticipant
    {
        /// Matchmaking participant.
        IMatchParticipant MatchParticipant { get; }

        /// ID of the participant in this room. This is assigned by the matchmaking system, is unique per room,
        /// and can change if a participant leaves and re-joins the room.
        int IdInRoom { get; }
    }
}
