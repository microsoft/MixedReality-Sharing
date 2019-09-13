// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    /// <summary>
    /// Information about a matchmaking room.
    /// </summary>
    public interface IRoom
    {
        /// <summary>
        /// The category of room. This is an application-defined URI.
        /// </summary>
        string Category { get; }

        /// <summary>
        /// The unique identifier of this room.
        /// </summary>
        Guid UniqueId { get; }

        /// <summary>
        /// An application specific connection string which can be used to join this room.
        /// </summary>
        string Connection { get; }

        /// <summary>
        /// Dictionary used to store data associated with the room, which can be used to filter and query rooms,
        /// and to store data which can be retrieved by any participant.
        /// </summary>
        IReadOnlyDictionary<string, string> Attributes { get; }
    }

    /// <summary>
    /// Information about a locally created matchmaking room.
    /// </summary>
    public interface ILocalRoom : IRoom
    {
        /// <summary>
        /// Add a new key value pair to the attributes.
        /// </summary>
        /// <exception cref="ArgumentException">An attribute with the same key already exists.</exception>
        void AddAttribute(string key, string value);

        /// <summary>
        /// Add or replace a key value pair to the attributes.
        /// </summary>
        void AddOrReplaceAttribute(string key, string value);

        /// <summary>
        /// Return true if the attribute was removed, otherwise false.
        /// </summary>
        bool RemoveAttribute(string key);
    }
}
