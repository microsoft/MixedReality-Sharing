// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    public interface IMatchmakingService : IDisposable
    {
        /// <summary>
        /// Start discovery of all rooms containing all of these attributes with the specified value.
        /// Passing a null or empty dictionary will list all searchable rooms.
        /// The returned collection will change over time. Use the INotifyCollectionChanged.CollectionChanged
        /// event to subscribe to changes.
        /// The collection will update indefinitely until StopDiscovery is called.
        /// </summary>
        ReadOnlyObservableCollection<IRoom> StartDiscovery(IReadOnlyDictionary<string, object> query);

        /// <summary>
        /// Stop an in-progress discovery.
        /// </summary>
        void StopDiscovery(ReadOnlyObservableCollection<IRoom> rooms);

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
    }
}
