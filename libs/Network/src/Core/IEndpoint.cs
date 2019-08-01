// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Sharing
{
    /// <summary>
    /// Represents a connected peer/endpoint/device to the session.
    /// </summary>
    public interface IEndpoint
    {
        /// <summary>
        /// The session this endpoint belongs to.
        /// </summary>
        ISession Session { get; }

        /// <summary>
        /// Gets a channel to communicate directly to the client on the other side of this endpoint.
        /// </summary>
        TChannel GetChannel<TChannel>(string channelId) where TChannel : IChannel;
    }
}
