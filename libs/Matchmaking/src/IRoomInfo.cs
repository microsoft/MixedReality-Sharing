// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    /// <summary>
    /// Read-only handle for a room in the matchmaking service.
    /// This interface wraps a generic room that might be joined or not by the local participant. Operations on
    /// joined rooms are exposed by <see cref="IRoom"/>.
    /// </summary>
    public interface IRoomInfo
    {
        /// <summary>
        /// Identifies this room.
        /// The matchmaking implementation must guarantee that this is unique for every new room.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Dictionary used to store data associated with the room, which can be used to filter and query rooms,
        /// and to store data which can be retrieved by any participant.
        /// </summary>
        /// <seealso cref="IRoom.SetAttributesAsync(Dictionary{string, object})"/>
        Dictionary<string, object> Attributes { get; }

        /// <summary>
        /// Try to join this room.
        /// Some implementation might only allow joining only if the local participant is not already in a room.
        /// </summary>
        Task<IRoom> JoinAsync(CancellationToken token = default);
    }
}
