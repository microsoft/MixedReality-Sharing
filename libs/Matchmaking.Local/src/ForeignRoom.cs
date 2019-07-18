// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking.Local
{
    /// <summary>
    /// Joined room belonging to a different host.
    /// </summary>
    class ForeignRoom : RoomBase
    {
        public SocketerClient Socket;

        public ForeignRoom(RoomInfo roomInfo, MatchParticipant owner, SocketerClient socket)
            : base(roomInfo, owner)
        {
            Socket = socket;
        }

        public override Task LeaveAsync()
        {
            throw new NotImplementedException();
        }

        public override Task SetAttributesAsync(Dictionary<string, object> attributes)
        {
            throw new NotImplementedException();
        }

        public override Task SetVisibility(RoomVisibility val)
        {
            throw new NotImplementedException();
        }
    }
}
