// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.Network;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Session
{
    public interface ISessionParticipant
    {
        /// <summary>
        /// Identifies the participant. Must be unique within a session.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Opens a channel to communicate to the other participant.
        /// </summary>
        ISessionParticipantChannel CreateChannel(Network.IChannelCategory category);
    }

    public interface ISessionParticipantChannel : Network.IChannel
    {
        ISessionParticipant Participant { get; }
    }
}
