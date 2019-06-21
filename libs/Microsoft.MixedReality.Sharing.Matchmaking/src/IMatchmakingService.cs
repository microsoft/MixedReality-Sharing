// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    public interface IMatchmakingService
    {
        /// <summary>
        /// Join a random available existing room.
        /// </summary>
        /// <param name="expectedAttributes">Only consider the rooms that have these attributes.</param>
        Task<IRoom> JoinRandomRoomAsync(Dictionary<string, object> expectedAttributes = null,
            CancellationToken token = default);

        /// <summary>
        /// Rooms currently joined by the local participants.
        /// </summary>
        IEnumerable<IRoom> JoinedRooms { get; }

        /// <summary>
        /// Room manager. Can be null if the implementation does not provide room managing services.
        /// </summary>
        IRoomManager RoomManager { get; }
    }
}
