// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Sharing.Network
{
    /// <summary>
    /// Helper extension methods that don't need to be part of main APIs
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Get the default channel for a session of type <see cref="{TChannel}"/>.
        /// </summary>
        /// <typeparam name="TChannel">The type of channel to get.</typeparam>
        /// <param name="session">The session to get channel from.</param>
        /// <returns>The channel instance.</returns>
        public static TChannel GetChannel<TChannel>(this ISession session) where TChannel : IChannel
        {
            return session.GetChannel<TChannel>(null);
        }

        /// <summary>
        /// Get the default channel for an endpoint of type <see cref="{TChannel}"/>.
        /// </summary>
        /// <typeparam name="TChannel">The type of channel to get.</typeparam>
        /// <param name="session">The endpoint to get channel from.</param>
        /// <returns>The channel instance.</returns>
        public static TChannel GetChannel<TChannel>(this IEndpoint endpoint) where TChannel : IChannel
        {
            return endpoint.GetChannel<TChannel>(null);
        }
    }
}
