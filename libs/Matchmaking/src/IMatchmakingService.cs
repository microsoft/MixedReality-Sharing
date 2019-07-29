// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    public interface IMatchmakingService
    {
        /// <summary>
        /// Join a room by its unique ID.
        /// </summary>
        /// <returns>
        /// a <see cref="Task"/> containing the joined room if the provided ID is found, otherwise a null room.
        /// </returns>
        Task<IRoom> TryGetRoomByIdAsync(string roomId, CancellationToken token);

        /// <summary>
        /// Join a random available existing room.
        /// </summary>
        /// <param name="expectedAttributes">Only consider the rooms that have these attributes.</param>
        Task<IRoom> JoinRandomRoomAsync(IDictionary<string, object> expectedAttributes, CancellationToken token);

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
        Task<IRoom> TryCreateRoomAsync(Dictionary<string, object> attributes, CancellationToken token);
    }
}
