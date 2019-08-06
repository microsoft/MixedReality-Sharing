﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    /// <summary>
    /// Exposes methods to create and list rooms.
    /// </summary>
    public interface IRoomManager
    {
        /// <summary>
        /// Join a room by its unique ID.
        /// </summary>
        /// <returns>
        /// a <see cref="Task"/> containing the joined room if the provided ID is found, otherwise a null room.
        /// </returns>
        Task<IRoom> JoinRoomByIdAsync(string roomId, CancellationToken token = default);

        /// <summary>
        /// Get the list of all rooms with the specified owner.
        /// </summary>
        IRoomList FindRoomsByOwner(IMatchParticipant owner);

        /// <summary>
        /// Get the list of all rooms containing any of the specified participants.
        /// </summary>
        IRoomList FindRoomsByParticipants(IEnumerable<IMatchParticipant> participants);

        /// <summary>
        /// Get the list of all rooms containing all of these attributes with the specified value.
        /// Passing an empty dictionary will list all searchable rooms.
        /// </summary>
        IRoomList FindRoomsByAttributes(Dictionary<string, object> attributes = default);

        /// <summary>
        /// Create a new room and join it.
        /// </summary>
        /// <param name="attributes">Attributes to set on the new room.</param>
        /// <param name="token">
        /// If cancellation is requested, the method should either complete the operation and return a valid
        /// room, or roll back any changes to the system state and return a canceled Task.
        /// </param>
        /// <returns>
        /// The newly created, joined room.
        /// </returns>
        Task<IRoom> CreateRoomAsync(Dictionary<string, object> attributes = null,
            RoomVisibility visibility = RoomVisibility.NotVisible,
            CancellationToken token = default);
    }

    /// <summary>
    /// Subscription to the list of active matchmaking rooms that satisfy certain criteria.
    /// </summary>
    /// <remarks>
    /// <see cref="CurrentRooms"/> is periodically updated as new rooms are discovered, created or closed.
    /// <see cref="Dispose"/> terminates the subscription.
    ///
    /// Note that the implementation might prevent callers from subscribing to more than one room list
    /// at the same time.
    /// </remarks>
    public interface IRoomList : IDisposable
    {
        /// <summary>
        /// Get the currently known active rooms.
        /// The implementation might return only a subset of the currently active rooms
        /// if the full set is too large.
        /// </summary>
        IEnumerable<IRoomInfo> CurrentRooms { get; }

        /// <summary>
        /// Raised when an up-to-date list of rooms is received.
        /// The frequency with which this is fired depends on the implementation.
        /// </summary>
        event EventHandler<IEnumerable<IRoomInfo>> RoomsRefreshed;
    }
}
