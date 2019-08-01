// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.Utilities;

namespace Microsoft.MixedReality.Sharing.Channels
{
    /// <summary>
    /// Common base class for channels.
    /// </summary>
    public class ChannelBase : DisposableBase, IChannel
    {
        /// <summary>
        /// Gets the identifier for this channel.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="id">The id of the channel.</param>
        protected internal ChannelBase(string id)
        {
            Id = id;
        }
    }
}
