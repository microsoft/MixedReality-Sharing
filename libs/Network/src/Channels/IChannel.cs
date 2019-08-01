// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing
{
    /// <summary>
    /// Channels represent a method of transmitting data, concrete implementation expose the appropriate API.
    /// </summary>
    public interface IChannel : IDisposable
    {
        /// <summary>
        /// The identifier for this channel.
        /// </summary>
        string Id { get; }
    }
}
