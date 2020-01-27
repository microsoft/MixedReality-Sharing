// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    /// <summary>
    /// Handle to an ongoing discovery subscription.
    /// </summary>
    /// <remarks>
    /// A subscription is bound to the originating <see cref="IDiscoveryAgent"/>, but its methods/properties
    /// (including `Dispose()`) must be safe to use independently from the state of the agent - even from a
    /// different thread from the one where the agent is used.
    ///
    /// After the agent is disposed, the result of <see cref="Resources"/> becomes undefined, and
    /// <see cref="Updated"/> might stop being raised.
    /// </remarks>
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
