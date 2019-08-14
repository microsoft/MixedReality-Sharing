// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    public interface IMatchmakingService : IDisposable
    {
        /// <summary>
        /// Gets a read-only collection of rooms that are owened by the current participant.
        /// </summary>
        IReadOnlyCollection<IOwnedRoom> LocallyOwnedRooms { get; }

        /// <summary>
        /// Join a random available existing room.
        /// </summary>
        /// <param name="expectedAttributes">Only consider the rooms that have these attributes.</param>
        Task<ISession> JoinRandomSessionAsync(IReadOnlyDictionary<string, string> expectedAttributes, CancellationToken token);

        /// <summary>
        /// Join a room by its unique ID.
        /// </summary>
        /// <returns>
        /// a <see cref="Task"/> containing the joined room if the provided ID is found, otherwise a null room.
        /// </returns>
        Task<ISession> JoinSessionByIdAsync(string roomId, CancellationToken token);

        /// <summary>
        /// Get the list of all rooms with the specified owner.
        /// </summary>
        Task<IRefreshableCollection<IRoom>> GetRoomsByOwnerAsync(IParticipant owner, CancellationToken token);

        /// <summary>
        /// Get the list of all rooms containing all of these attributes with the specified value.
        /// Passing an empty dictionary will list all searchable rooms.
        /// </summary>
        Task<IRefreshableCollection<IRoom>> GetRoomsByAttributesAsync(IReadOnlyDictionary<string, string> attributes, CancellationToken token);

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
        Task<IOwnedRoom> OpenRoomAsync(IDictionary<string, string> attributes, CancellationToken token);
    }
}
