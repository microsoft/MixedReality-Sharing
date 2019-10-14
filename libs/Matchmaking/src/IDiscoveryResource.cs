// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    /// <summary>
    /// Information about a matchmaking resource advertised on the network.
    /// </summary>
    public interface IDiscoveryResource
    {
        /// <summary>
        /// The category of the resource. This is an application-defined URI.
        /// </summary>
        string Category { get; }

        /// <summary>
        /// The unique identifier of this resource.
        /// </summary>
        Guid UniqueId { get; }

        /// <summary>
        /// An application specific string which can be used to connect to this resource.
        /// </summary>
        string Connection { get; }

        /// <summary>
        /// Dictionary used to store data associated with the resource, which can be used to filter and query resources,
        /// and to store data which can be retrieved by any participant.
        /// </summary>
        IReadOnlyDictionary<string, string> Attributes { get; }

        /// <summary>
        /// If the backend allows it, return an interface to edit this resource. Otherwise return null.
        /// </summary>
        IDiscoveryResourceEditor RequestEdit();
    }

    /// <summary>
    /// Interface to edit a resource.
    /// </summary>
    public interface IDiscoveryResourceEditor
    {
        /// <summary>
        /// Try to commit the edits made through this interface.
        /// </summary>
        Task CommitAsync();

        /// <summary>
        /// Add or replace a key value pair to the attributes.
        /// </summary>
        void PutAttribute(string key, string value);

        /// <summary>
        /// Remove the attribute with the given key.
        /// </summary>
        void RemoveAttribute(string key);
    }
}
