// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    /// <summary>
    /// Information about a matchmaking room.
    ///
    /// </summary>
    public interface IRoom
    {
        /// <summary>
        /// An implementation specific connection string which can be used to join this room.
        /// </summary>
        string Connection { get; }

        /// <summary>
        /// The time this information was last refreshed.
        /// </summary>
        DateTime LastRefreshed { get; }

        /// <summary>
        /// Dictionary used to store data associated with the room, which can be used to filter and query rooms,
        /// and to store data which can be retrieved by any participant.
        /// </summary>
        IReadOnlyDictionary<string, string> Attributes { get; }
    }
}
