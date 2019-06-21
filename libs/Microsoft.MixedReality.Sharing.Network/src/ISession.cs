// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    /// <summary>
    /// Sharing session that this process is a part of.
    /// A sharing session is a collection of participants who can interact with each other and edit a shared state.
    /// </summary>
    public interface ISession
    {
        IEnumerable<IParticipant> Participants { get; }

        // TODO state

        // TODO channels
    }
}
