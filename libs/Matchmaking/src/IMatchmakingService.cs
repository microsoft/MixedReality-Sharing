// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    /// <summary>
    /// Handle to an ongoing discovery task.
    /// </summary>
    public interface IDiscoveryTask : IDisposable
    {
        /// <summary>
        /// The list of discovered rooms, ordered by IRoom.UniqueId.
        /// </summary>
        IEnumerable<IRoom> Rooms { get; }

        /// <summary>
        /// Event raised when the 'Rooms' property will return an updated result.
        /// </summary>
        event Action<IDiscoveryTask> Updated;
    }

    public interface IMatchmakingService : IDisposable
    {
        /// <summary>
        /// Start discovery of all rooms whose category matches the one given.
        /// The returned result will change over time. Use the Updated event to subscribe to changes.
        /// The collection will update indefinitely until disposed.
        /// </summary>
        IDiscoveryTask StartDiscovery(string category);

        /// <summary>
        /// Create a new room.
        /// </summary>
        /// <param name="attributes">Attributes to set on the new room.</param>
        /// <param name="token">
        /// If cancellation is requested, the method should either complete the operation and return a valid
        /// room, or roll back any changes to the system state and return a canceled Task.
        /// </param>
        /// <returns>
        /// The newly created room.
        /// </returns>
        Task<IRoom> CreateRoomAsync(
            string category,
            string connection,
            IReadOnlyDictionary<string, string> attributes = null,
            CancellationToken token = default);
    }
}
