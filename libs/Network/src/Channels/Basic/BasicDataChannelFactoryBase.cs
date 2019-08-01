// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Sharing.Channels
{
    /// <summary>
    /// Helper basic class to be used for inheriting from by various implementations.
    /// </summary>
    public abstract class BasicDataChannelFactoryBase : IChannelFactory<BasicDataChannel>
    {
        /// <summary>
        /// Gets the name of this channel factory.
        /// </summary>
        public virtual string Name { get; } = $"Factory for {nameof(BasicDataChannel)}";


        BasicDataChannel IChannelFactory<BasicDataChannel>.GetChannel(ISession session, string channelId)
        {
            return GetChannel(session, channelId);
        }

        BasicDataChannel IChannelFactory<BasicDataChannel>.GetChannel(IEndpoint endpoint, string channelId)
        {
            return GetChannel(endpoint, channelId);
        }

        /// <summary>
        /// Implemented in the inheriting class to get a <see cref="BasicDataChannel"/> for the session.
        /// </summary>
        protected abstract BasicDataChannel GetChannel(ISession session, string channelId);

        /// <summary>
        /// Implemented in the inheriting class to get a <see cref="BasicDataChannel"/> for the endpoint.
        /// </summary>
        protected abstract BasicDataChannel GetChannel(IEndpoint endpoint, string channelId);
    }
}
