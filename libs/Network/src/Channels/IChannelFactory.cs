// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Sharing
{
    /// <summary>
    /// The channel factory for a specific type of channel.
    /// </summary>
    /// <typeparam name="TChannel">The type of channel this factory creates.</typeparam>
    public interface IChannelFactory<out TChannel>
        where TChannel : IChannel
    {
        /// <summary>
        /// Opens a new channel for the specified session.
        /// </summary>
        /// <param name="session">The sesson for which the channel should be opened.</param>
        /// <param name="channelId">The id of the channel to open.</param>
        /// <returns>The opened channel.</returns>
        TChannel GetChannel(ISession session, string channelId);

        /// <summary>
        /// Opens a new channel for the specified participant.
        /// </summary>
        /// <param name="endpoint">The endpoint for which the channel should be opened.</param>
        /// <param name="channelId">The id of the channel to open.</param>
        /// <returns>The opened channel.</returns>
        TChannel GetChannel(IEndpoint endpoint, string channelId);
    }
}
