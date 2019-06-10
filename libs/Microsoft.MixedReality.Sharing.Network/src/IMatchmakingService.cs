// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    public interface IMatchmakingService
    {
        /// <summary>
        /// Join a random available existing room.
        /// </summary>
        /// <param name="expectedProperties">Only consider the rooms that have these properties.</param>
        /// <param name="reservedContacts">
        /// The method will reserve slots for these contacts in the joined room.
        /// Depending on the implementation, the method might return a joined IRoom immediately
        /// or after all contacts have joined.
        /// </param>
        Task<IRoom> JoinRandomRoomAsync(RoomProperties expectedProperties = null, IEnumerable<IContact> reservedContacts = null);

        /// <summary>
        /// Room manager. Can be null if the implementation does not provide room managing services.
        /// </summary>
        IRoomManager RoomManager { get; }
    }
}
