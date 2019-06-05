// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    /// <summary>
    /// Represents a room created in the matchmaking service.
    /// </summary>
    public interface IRoom
    {
        /// <summary>
        /// Identifies this room.
        /// There should not be two different rooms with the same ID open at the same time.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// The user that created this room.
        /// </summary>
        IContact Host { get; }

        RoomProperties Properties { get; }
        Task SetPropertiesAsync(RoomProperties properties);

        /// <summary>
        /// Try to join the room. Gets the current session if already joined.
        /// Should only be called if no room is joined at the moment.
        /// </summary>
        Task<ISession> TryJoinAsync(CancellationToken token);
    }

    /// <summary>
    /// Custom properties of a room.
    /// </summary>
    public class RoomProperties : Dictionary<string, object> {}
}
