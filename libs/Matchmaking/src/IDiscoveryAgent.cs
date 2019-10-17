// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    /// <summary>
    /// Handle to an ongoing discovery subscription.
    /// </summary>
    public interface IDiscoverySubscription : IDisposable
    {
        /// <summary>
        /// The list of discovered resources, ordered by IDiscoveryResource.UniqueId.
        /// </summary>
        IEnumerable<IDiscoveryResource> Resources { get; }

        /// <summary>
        /// Event raised when the 'Resources' property will return an updated result.
        /// </summary>
        event Action<IDiscoverySubscription> Updated;
    }

    /// <summary>
    /// Entry point for publishing and/or subscribing to matchmaking resources on the network.
    /// </summary>
    public interface IDiscoveryAgent : IDisposable
    {
        /// <summary>
        /// Start discovery of all resources whose category matches the one given.
        /// The returned result will change over time. Use the Updated event to subscribe to changes.
        /// The subscription will update indefinitely until disposed.
        /// </summary>
        IDiscoverySubscription Subscribe(string category);

        /// <summary>
        /// Publish a new resource.
        /// </summary>
        /// <param name="attributes">Attributes to set on the new resource.</param>
        /// <param name="token">
        /// If cancellation is requested, the method should either complete the operation and return a valid
        /// resource, or roll back any changes to the system state and return a canceled Task.
        /// </param>
        /// <returns>
        /// The newly created resource.
        /// </returns>
        Task<IDiscoveryResource> PublishAsync(
            string category,
            string connection,
            IReadOnlyDictionary<string, string> attributes = null,
            CancellationToken token = default);
    }
}
