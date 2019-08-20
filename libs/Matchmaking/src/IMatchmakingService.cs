// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    public interface IMatchmakingService : IDisposable
    {
        /// <summary>
        /// Get the list of all rooms containing all of these attributes with the specified value.
        /// Passing a null or empty dictionary will list all searchable rooms.
        /// </summary>
        IRoomList Discover(IReadOnlyDictionary<string, object> query);

        /// <summary>
        /// Create a new room.
        /// </summary>
        /// <param name="attributes">Attributes to set on the new room.</param>
        /// <param name="token">
        /// If cancellation is requested, the method should either complete the operation and return a valid
        /// room, or roll back any changes to the system state and return a canceled Task.
        /// </param>
        /// <returns>
        /// The newly created, joined room.
        /// </returns>
        Task<IRoom> CreateRoomAsync(
            string name,
            string connection,
            Dictionary<string, object> attributes = null,
            CancellationToken token = default);

        /// <summary>
        /// Set some property values on the room.
        /// The method will set the keys contained in the passed dictionary to the passed values.
        /// If the room attributes do not contain some of the keys, those will be added.
        /// The types supported for the property values are defined by each implementation.
        /// </summary>
        /// <param name="attributes"></param>
        Task SetAttributesAsync(IRoom room, Dictionary<string, string> attributes);
    }
}
