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
}
