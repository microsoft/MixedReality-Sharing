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
    /// A sharing session is a collection of contacts who joined a room and are interacting with each other.
    /// </summary>
    public interface ISession
    {
        /// <summary>
        /// Identifies this session.
        /// There should not be two sessions with the same ID active at the same time.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Leave this session.
        /// </summary>
        /// <returns></returns>
        Task LeaveAsync();
    }
}
